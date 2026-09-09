using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Dispatch
{
    /// <summary>
    /// Indexes the registered handlers by task type and forwards each task to its handler.
    /// </summary>
    /// <remarks>
    /// The dispatcher depends on the <see cref="ISyncTaskHandler"/> abstraction and on
    /// nothing else, so adding, removing or reordering handlers never touches this class.
    /// The container injects every registered implementation; this type does not know which
    /// ones exist, only how to reach them.
    /// </remarks>
    public sealed class SyncTaskDispatcher : ISyncTaskDispatcher
    {
        private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlersByTaskType;

        public SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> handlers)
        {
            if (handlers is null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            var index = new Dictionary<string, ISyncTaskHandler>(StringComparer.OrdinalIgnoreCase);

            foreach (var handler in handlers)
            {
                if (string.IsNullOrWhiteSpace(handler.TaskType))
                {
                    throw new InvalidOperationException(
                        $"Handler {handler.GetType().FullName} does not declare a task type.");
                }

                // A duplicate registration means two handlers would answer the same task type
                // and which one wins would depend on container registration order. Failing at
                // composition time turns a silent, environment-dependent routing bug into a
                // startup crash with a name in it.
                if (index.ContainsKey(handler.TaskType))
                {
                    throw new InvalidOperationException(
                        $"More than one handler is registered for task type '{handler.TaskType}'.");
                }

                index.Add(handler.TaskType, handler);
            }

            _handlersByTaskType = index;
            SupportedTaskTypes = index.Keys.OrderBy(type => type, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyCollection<string> SupportedTaskTypes { get; }

        public Task<SyncPayload> DispatchAsync(SyncTaskRequest task, CancellationToken cancellationToken)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (!_handlersByTaskType.TryGetValue(task.TaskType, out var handler))
            {
                // The requested type is deliberately kept out of the exception message: it is
                // attacker-controlled text and the message is sent back over the wire. The
                // full value is logged locally by the caller instead.
                throw new UnknownTaskTypeException();
            }

            return handler.ExecuteAsync(task, cancellationToken);
        }
    }
}
