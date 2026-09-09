using System;
using System.Threading;

namespace SyncAgent.Host
{
    /// <summary>
    /// Ensures only one sync agent runs on the machine at a time.
    /// </summary>
    /// <remarks>
    /// This matters more here than the usual "don't run twice" tidiness. The platform's
    /// dequeue is destructive and unsynchronised, so two agents polling the same queue would
    /// each claim a share of the tasks and each report only its own — the tasks would not be
    /// duplicated, they would be silently split between two log files and two connection
    /// pools. The most likely way to end up there is exactly the one this catches: someone
    /// launching the console build to test something while the service is already running.
    ///
    /// The mutex is in the Global namespace so it spans terminal-services sessions; a service
    /// runs in session 0 and an interactive console does not, and a session-local mutex would
    /// not see the conflict.
    /// </remarks>
    public sealed class SingleInstanceGuard : IDisposable
    {
        private const string MutexName = @"Global\Kabilio.SyncAgent.SingleInstance";

        private readonly Mutex? _mutex;

        private SingleInstanceGuard(Mutex? mutex, bool acquired)
        {
            _mutex = mutex;
            Acquired = acquired;
        }

        public bool Acquired { get; }

        public static SingleInstanceGuard TryAcquire()
        {
            Mutex? mutex = null;

            try
            {
                mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);

                if (!createdNew)
                {
                    mutex.Dispose();
                    return new SingleInstanceGuard(null, acquired: false);
                }

                return new SingleInstanceGuard(mutex, acquired: true);
            }
            catch (UnauthorizedAccessException)
            {
                // The mutex exists but was created by another account — typically the service
                // account, while this process is an interactive user. That is still another
                // instance holding it, so the answer is the same.
                mutex?.Dispose();
                return new SingleInstanceGuard(null, acquired: false);
            }
        }

        public void Dispose()
        {
            if (_mutex is null)
            {
                return;
            }

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not the owning thread. Nothing to release, and nothing to do about it.
            }

            _mutex.Dispose();
        }
    }
}
