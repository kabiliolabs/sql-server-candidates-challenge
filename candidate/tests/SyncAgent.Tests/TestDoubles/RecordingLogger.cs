using Microsoft.Extensions.Logging;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>
/// Captures formatted log messages so a test can assert on what was -- and was not -- written.
/// Used to prove the API key never reaches a log sink.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public string AllText => string.Join(Environment.NewLine, Entries.Select(e => $"{e.Level} {e.Message} {e.Exception}"));

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
