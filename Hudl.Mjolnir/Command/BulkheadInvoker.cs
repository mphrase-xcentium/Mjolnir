﻿using Hudl.Config;
using Hudl.Mjolnir.Bulkhead;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hudl.Mjolnir.Command
{
    internal interface IBulkheadInvoker
    {
        Task<TResult> ExecuteWithBulkheadAsync<TResult>(AsyncCommand<TResult> command, CancellationToken ct);
        TResult ExecuteWithBulkhead<TResult>(SyncCommand<TResult> command, CancellationToken ct);
        Task ExecuteWithBulkheadAsync(AsyncCommand command, CancellationToken ct);
        void ExecuteWithBulkhead(SyncCommand command, CancellationToken ct);
    }

    internal class BulkheadInvoker : IBulkheadInvoker
    {
        private readonly IBreakerInvoker _breakerInvoker;
        private readonly ICommandContext _context;
        private readonly IConfigurableValue<bool> _useCircuitBreakers; 

        public BulkheadInvoker(IBreakerInvoker breakerInvoker, ICommandContext context, IConfigurableValue<bool> useCircuitBreakers = null)
        {
            if (breakerInvoker == null)
            {
                throw new ArgumentNullException("breakerInvoker");
            }

            _breakerInvoker = breakerInvoker;
            _context = context ?? CommandContext.Current;
            _useCircuitBreakers = useCircuitBreakers ?? new ConfigurableValue<bool>("mjolnir.useCircuitBreakers", true);
        }

        // Note: Bulkhead rejections shouldn't count as failures to the breaker. If a downstream
        // dependency is slow, the pool will fill up, but the breaker + timeouts will already be
        // providing protection against that. If the bulkhead is filling up because of a surge of
        // requests, the rejections will just be a way of shedding load - the breaker and
        // downstream dependency may be just fine, and we want to keep them that way.

        // We'll neither mark these as success *nor* failure, since they really didn't even execute
        // as far as the breaker and downstream dependencies are concerned.

        public async Task<TResult> ExecuteWithBulkheadAsync<TResult>(AsyncCommand<TResult> command, CancellationToken ct)
        {
            var bulkhead = _context.GetBulkhead(command.BulkheadKey);

            if (!bulkhead.TryEnter())
            {
                _context.MetricEvents.RejectedByBulkhead(bulkhead.Name, command.Name);
                throw new BulkheadRejectedException();
            }

            _context.MetricEvents.EnterBulkhead(bulkhead.Name, command.Name);

            // This stopwatch should begin stopped (hence the constructor instead of the usual
            // Stopwatch.StartNew(). We'll only use it if we aren't using circuit breakers.
            var stopwatch = new Stopwatch();
            var executedHere = false;
            try
            {
                if (_useCircuitBreakers.Value)
                {
                    executedHere = false;
                    return await _breakerInvoker.ExecuteWithBreakerAsync(command, ct).ConfigureAwait(false);
                }

                executedHere = true;
                stopwatch.Start();
                return await command.ExecuteAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                bulkhead.Release();

                _context.MetricEvents.LeaveBulkhead(bulkhead.Name, command.Name);

                if (executedHere)
                {
                    stopwatch.Stop();
                    command.ExecutionTimeMillis = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }

        public TResult ExecuteWithBulkhead<TResult>(SyncCommand<TResult> command, CancellationToken ct)
        {
            var bulkhead = _context.GetBulkhead(command.BulkheadKey);

            if (!bulkhead.TryEnter())
            {
                _context.MetricEvents.RejectedByBulkhead(bulkhead.Name, command.Name);
                throw new BulkheadRejectedException();
            }

            _context.MetricEvents.EnterBulkhead(bulkhead.Name, command.Name);

            // This stopwatch should begin stopped (hence the constructor instead of the usual
            // Stopwatch.StartNew(). We'll only use it if we aren't using circuit breakers.
            var stopwatch = new Stopwatch();
            var executedHere = false;
            try
            {
                if (_useCircuitBreakers.Value)
                {
                    executedHere = false;
                    return _breakerInvoker.ExecuteWithBreaker(command, ct);
                }

                executedHere = true;
                stopwatch.Start();
                return command.Execute(ct);
            }
            finally
            {
                bulkhead.Release();

                _context.MetricEvents.LeaveBulkhead(bulkhead.Name, command.Name);

                if (executedHere)
                {
                    stopwatch.Stop();
                    command.ExecutionTimeMillis = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }

        public async Task ExecuteWithBulkheadAsync(AsyncCommand command, CancellationToken ct)
        {
            var bulkhead = _context.GetBulkhead(command.BulkheadKey);

            if (!bulkhead.TryEnter())
            {
                _context.MetricEvents.RejectedByBulkhead(bulkhead.Name, command.Name);
                throw new BulkheadRejectedException();
            }

            _context.MetricEvents.EnterBulkhead(bulkhead.Name, command.Name);

            // This stopwatch should begin stopped (hence the constructor instead of the usual
            // Stopwatch.StartNew(). We'll only use it if we aren't using circuit breakers.
            var stopwatch = new Stopwatch();
            var executedHere = false;
            try
            {
                if (_useCircuitBreakers.Value)
                {
                    executedHere = false;
                    await _breakerInvoker.ExecuteWithBreakerAsync(command, ct).ConfigureAwait(false);
                    return;
                }

                executedHere = true;
                stopwatch.Start();
                await command.ExecuteAsync(ct).ConfigureAwait(false);
                return;
            }
            finally
            {
                bulkhead.Release();

                _context.MetricEvents.LeaveBulkhead(bulkhead.Name, command.Name);

                if (executedHere)
                {
                    stopwatch.Stop();
                    command.ExecutionTimeMillis = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }

        public void ExecuteWithBulkhead(SyncCommand command, CancellationToken ct)
        {
            var bulkhead = _context.GetBulkhead(command.BulkheadKey);

            if (!bulkhead.TryEnter())
            {
                _context.MetricEvents.RejectedByBulkhead(bulkhead.Name, command.Name);
                throw new BulkheadRejectedException();
            }

            _context.MetricEvents.EnterBulkhead(bulkhead.Name, command.Name);

            // This stopwatch should begin stopped (hence the constructor instead of the usual
            // Stopwatch.StartNew(). We'll only use it if we aren't using circuit breakers.
            var stopwatch = new Stopwatch();
            var executedHere = false;
            try
            {
                if (_useCircuitBreakers.Value)
                {
                    executedHere = false;
                    _breakerInvoker.ExecuteWithBreaker(command, ct);
                    return;
                }

                executedHere = true;
                stopwatch.Start();
                command.Execute(ct);
                return;
            }
            finally
            {
                bulkhead.Release();

                _context.MetricEvents.LeaveBulkhead(bulkhead.Name, command.Name);

                if (executedHere)
                {
                    stopwatch.Stop();
                    command.ExecutionTimeMillis = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }
    }
}
