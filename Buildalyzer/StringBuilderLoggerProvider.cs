using System.Text;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class StringBuilderLoggerProvider : ILoggerProvider
    {
        private readonly StringBuilder _stringBuilder;

        public StringBuilderLoggerProvider(StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => new StringBuilderLogger(_stringBuilder);
    }
}