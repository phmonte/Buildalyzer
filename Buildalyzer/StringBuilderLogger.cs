using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    internal class StringBuilderLogger : ILogger
    {
        private readonly StringBuilder _stringBuilder;

        public StringBuilderLogger(StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            _stringBuilder.AppendLine(formatter(state, exception));

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => new EmptyDisposable();
    }
}