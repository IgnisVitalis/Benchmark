using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XUnitLogger(ITestOutputHelper output) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        output.WriteLine(formatter(state, exception));

        if (exception is not null)
            output.WriteLine(exception.ToString());
    }
}
