using Microsoft.Extensions.Logging;
using Platform.Contracts.Admin;

namespace Platform.Infrastructure.Observability;

/// <summary>An <see cref="ILoggerProvider"/> that captures Information+ logs into <see cref="RecentLogStore"/>.</summary>
public sealed class RecentLogProvider : ILoggerProvider
{
    private readonly RecentLogStore _store;

    public RecentLogProvider(RecentLogStore store) => _store = store;

    public ILogger CreateLogger(string categoryName) => new RecentLogger(categoryName, _store);

    public void Dispose()
    {
    }

    private sealed class RecentLogger : ILogger
    {
        private readonly string _category;
        private readonly RecentLogStore _store;

        public RecentLogger(string category, RecentLogStore store)
        {
            _category = category;
            _store = store;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message += " " + exception.Message;
            }

            _store.Add(new LogRecord(DateTimeOffset.UtcNow, logLevel.ToString(), _category, message));
        }
    }
}
