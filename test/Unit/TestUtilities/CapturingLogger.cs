using Microsoft.Extensions.Logging;

namespace Unit.TestUtilities;

/// <summary>
/// An <see cref="ILogger"/> that records every entry so tests can assert on the
/// event id, level, and rendered message of the library's diagnostic output.
/// </summary>
internal class CapturingLogger : ILogger
{
    internal sealed record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new Entry(logLevel, eventId, formatter(state, exception), exception));
}

/// <summary>
/// The generic-category counterpart of <see cref="CapturingLogger"/>.
/// </summary>
internal sealed class CapturingLogger<T> : CapturingLogger, ILogger<T>;
