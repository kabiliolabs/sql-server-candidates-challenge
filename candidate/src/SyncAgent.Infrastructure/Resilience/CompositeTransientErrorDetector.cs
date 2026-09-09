using System;
using System.Collections.Generic;
using System.Linq;
using SyncAgent.Core.Abstractions;

namespace SyncAgent.Infrastructure.Resilience
{
    /// <summary>
    /// Treats a failure as transient if any registered detector says so.
    /// </summary>
    /// <remarks>
    /// Supporting a new dependency means registering another detector, not editing a growing
    /// switch statement inside the retry policy. Inner exceptions are walked as well, because
    /// the interesting exception is routinely wrapped: a SqlException arrives inside an
    /// AggregateException, and an HTTP failure inside a TaskCanceledException.
    /// </remarks>
    public sealed class CompositeTransientErrorDetector : ITransientErrorDetector
    {
        /// <summary>
        /// Depth limit on the inner-exception walk. Exception chains are not guaranteed
        /// acyclic once custom exception types are involved, and an unbounded walk of one is
        /// an infinite loop inside an error handler.
        /// </summary>
        private const int MaxInnerExceptionDepth = 8;

        private readonly IReadOnlyList<ITransientErrorDetector> _detectors;

        public CompositeTransientErrorDetector(IEnumerable<ITransientErrorDetector> detectors)
        {
            if (detectors is null)
            {
                throw new ArgumentNullException(nameof(detectors));
            }

            _detectors = detectors.ToArray();
        }

        public bool IsTransient(Exception exception)
        {
            var current = exception;

            for (var depth = 0; current != null && depth < MaxInnerExceptionDepth; depth++)
            {
                foreach (var detector in _detectors)
                {
                    if (detector.IsTransient(current))
                    {
                        return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
