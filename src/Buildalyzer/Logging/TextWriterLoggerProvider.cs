using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Logging
{
    public class TextWriterLoggerProvider : ILoggerProvider
    {
        private readonly TextWriter _textWriter;

        public TextWriterLoggerProvider(TextWriter textWriter)
        {
            _textWriter = textWriter;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => new TextWriterLogger(_textWriter);
    }
}