using System;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Buildalyzer.Logging
{
    internal class TextWriterLogger : ILogger
    {
        private readonly TextWriter _textWriter;

        public TextWriterLogger(TextWriter textWriter)
        {
            _textWriter = textWriter;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            _textWriter.Write(formatter(state, exception));

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => new EmptyDisposable();
    }
}