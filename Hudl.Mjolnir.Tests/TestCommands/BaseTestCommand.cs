﻿using System;
using Hudl.Mjolnir.Command;
using Hudl.Mjolnir.Tests.Helper;
using Hudl.Riemann;

namespace Hudl.Mjolnir.Tests.TestCommands
{
    internal abstract class BaseTestCommand<TResult> : Command<TResult>
    {
        internal BaseTestCommand() : this(TimeSpan.FromMilliseconds(10000)) { }
        internal BaseTestCommand(TimeSpan? timeout) : this("test", "test", timeout) {}
        internal BaseTestCommand(string group, string isolationKey, TimeSpan? timeout)
            : base(group, isolationKey, isolationKey, timeout ?? TimeSpan.FromMilliseconds(10000))
        {
            Riemann = new IgnoringRiemannStats();
            CircuitBreaker = new AlwaysSuccessfulCircuitBreaker();
            ThreadPool = new AlwaysSuccessfulIsolationThreadPool();
            FallbackSemaphore = new AlwaysSuccessfulIsolationSemaphore();
        }
    }
}
