﻿using System;
using System.Diagnostics;
using Amib.Threading;
using Hudl.Config;
using Hudl.Mjolnir.External;
using Hudl.Mjolnir.Key;
using Hudl.Mjolnir.Util;

namespace Hudl.Mjolnir.ThreadPool
{
    /// <summary>
    /// IIsolationThreadPool that uses a backing SmartThreadPool.
    /// </summary>
    internal class StpIsolationThreadPool : IIsolationThreadPool
    {
        private static readonly IConfigurableValue<long> ConfigGaugeIntervalMillis = new ConfigurableValue<long>("mjolnir.bulkheadConfigGaugeIntervalMillis", 60000);

        private readonly GroupKey _key;
        private readonly SmartThreadPool _pool;
        private readonly IStats _stats;
        private readonly IMetricEvents _metricEvents;

        private readonly IConfigurableValue<int> _threadCount;
        private readonly IConfigurableValue<int> _queueLength;

        // Eventually the _statsTimer here will go away. IStats are deprecated and IMetrics are the
        // way of the future.
        // ReSharper disable NotAccessedField.Local
        // Don't let these get garbage collected.
        private readonly GaugeTimer _statsTimer;
        private readonly GaugeTimer _metricsTimer;
        // ReSharper restore NotAccessedField.Local

        internal StpIsolationThreadPool(GroupKey key, IConfigurableValue<int> threadCount, IConfigurableValue<int> queueLength, IStats stats, IMetricEvents metricEvents, IConfigurableValue<long> gaugeIntervalMillisOverride = null)
        {
            _key = key;
            _threadCount = threadCount;
            _queueLength = queueLength;

            if (stats == null)
            {
                throw new ArgumentNullException("stats");
            }
            _stats = stats;

            if (metricEvents == null)
            {
                throw new ArgumentNullException("metricEvents");
            }
            _metricEvents = metricEvents;

            var count = _threadCount.Value;
            var info = new STPStartInfo
            {
                ThreadPoolName = _key.Name,
                MinWorkerThreads = count,
                MaxWorkerThreads = count,
                MaxQueueLength = queueLength.Value,
                AreThreadsBackground = true,
                UseCallerExecutionContext = true,
                UseCallerHttpContext = true
            };

            _pool = new SmartThreadPool(info);

            // Old gauge, will be phased out in v3.0 when IStats are removed.
            _statsTimer = new GaugeTimer((source, args) =>
            {
                _stats.Gauge(StatsPrefix + " activeThreads", null, _pool.ActiveThreads);
                _stats.Gauge(StatsPrefix + " inUseThreads", null, _pool.InUseThreads);

                // Note: Don't use _pool.WaitingCallbacks. It has the potential to get locked out by
                // queue/dequeue operations, and may block here if the pool's getting queued into heavily.
                _stats.Gauge(StatsPrefix + " pendingCompletion", null, _pool.CurrentWorkItemsCount);
            }, gaugeIntervalMillisOverride);

            _metricsTimer = new GaugeTimer((source, args) =>
            {
                _metricEvents.BulkheadConfigGauge(Name, "pool", queueLength.Value + threadCount.Value);
            }, ConfigGaugeIntervalMillis);

            _pool.OnThreadInitialization += () => _stats.Event(StatsPrefix + " thread", "Initialized", null);
            _pool.OnThreadTermination += () => _stats.Event(StatsPrefix + " thread", "Terminated", null);

            _threadCount.AddChangeHandler(UpdateThreadCount);
            _queueLength.AddChangeHandler(UpdateQueueLength);
        }

        private string StatsPrefix
        {
            get { return "mjolnir pool " + _key; }
        }

        public string Name { get { return _key.Name; } }

        public void Start()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _pool.Start();
            }
            finally
            {
                _stats.Elapsed(StatsPrefix + " Start", null, stopwatch.Elapsed);
            }
        }

        public IWorkItem<TResult> Enqueue<TResult>(System.Func<TResult> func)
        {
            var stopwatch = Stopwatch.StartNew();
            var state = "Enqueued";
            try
            {
                var workItem = _pool.QueueWorkItem(new Amib.Threading.Func<TResult>(func));
                return new StpWorkItem<TResult>(workItem);
            }
            catch (QueueRejectedException)
            {
                state = "Rejected";
                throw new IsolationThreadPoolRejectedException();
            }
            finally
            {
                _stats.Elapsed(StatsPrefix + " Enqueue", state, stopwatch.Elapsed);
            }
        }

        private void UpdateThreadCount(int threadCount)
        {
            _pool.MaxThreads = threadCount;
        }

        private void UpdateQueueLength(int queueLength)
        {
            _pool.MaxQueueLength = queueLength;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                _pool?.Dispose();
                _statsTimer?.Dispose();
                _metricsTimer?.Dispose();
            }
            _disposed = true;
        }
    }
}