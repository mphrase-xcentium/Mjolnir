﻿using System;
using System.Collections.Generic;

namespace Hudl.Mjolnir.Breaker
{
    /// <summary>
    /// Ignored exception types won't count toward breakers tripping or other error counters.
    /// Useful for things like validation, where the system isn't having any problems and the
    /// caller needs to validate before invoking.
    /// 
    /// Note that ignored exceptions will still be thrown back through Execute/ExecuteAsync.
    /// They simply won't count toward circuit breaker errors.
    /// </summary>
    public interface IBreakerExceptionHandler
    {
        /// <summary>
        /// Returns true if the exception should be ignored by circuit breakers when counting
        /// errors. Useful for excluding things like ArgumentExceptions, where the error is likely
        /// not a downstream system error and instead more likely an error/bug on the calling side.
        /// </summary>
        bool IsExceptionIgnored(Type type);
    }

    /// <summary>
    /// Default implementation for IBreakerExceptionHandler that uses a set of ignored
    /// Exception Types.
    /// </summary>
    public class BreakerExceptionHandler : IBreakerExceptionHandler
    {
        private readonly HashSet<Type> _ignored;
        
        public BreakerExceptionHandler(HashSet<Type> ignored)
        {
            // Defensive copy to avoid caller modifying the set after passing.
            _ignored = (ignored == null ? new HashSet<Type>() : new HashSet<Type>(ignored));
        }

        public bool IsExceptionIgnored(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return _ignored.Contains(type);
        }
    }
}
