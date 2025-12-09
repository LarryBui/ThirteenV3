using System;
using Microsoft.Extensions.Logging;

namespace TienLen.Unity.Infrastructure.Logging
{
    // Custom generic logger wrapper to bridge Serilog/Microsoft.Extensions.Logging with VContainer injection
    public class GenericLogger<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        public GenericLogger(ILoggerFactory factory)
        {
            _logger = factory.CreateLogger(typeof(T).FullName);
        }

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
