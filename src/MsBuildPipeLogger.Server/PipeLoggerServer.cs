using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;

namespace MsBuildPipeLogger.Server
{
    public class PipeLoggerServer : EventArgsDispatcher, IDisposable
    {
        // This comes from https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/src/StructuredLogger/BinaryLogger/BinaryLogger.cs
        // It should match the version of the files that were copied into MsBuildPipeLogger.Logger from MSBuildStructuredLog
        private const int FileFormatVersion = 7;

        private readonly AnonymousPipeServerStream _pipe;
        private readonly BinaryReader _binaryReader;
        private readonly Func<BuildEventArgs> _read;

        private string _clientHandle;

        public PipeLoggerServer()
        {
            _pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            _binaryReader = new BinaryReader(_pipe);

            object argsReader;
            Type buildEventArgsReader = typeof(BinaryLogger).GetTypeInfo().Assembly.GetType("Microsoft.Build.Logging.BuildEventArgsReader");
            ConstructorInfo readerCtor = buildEventArgsReader.GetConstructor(new[] { typeof(BinaryReader) });
            if(readerCtor != null)
            {
                argsReader = readerCtor.Invoke(new[] { _binaryReader });
            }
            else
            {
                readerCtor = buildEventArgsReader.GetConstructor(new[] { typeof(BinaryReader), typeof(int) });
                argsReader = readerCtor.Invoke(new object[] { _binaryReader, 7 });
            }
            MethodInfo readMethod = buildEventArgsReader.GetMethod("Read");
            _read = (Func<BuildEventArgs>)readMethod.CreateDelegate(typeof(Func<BuildEventArgs>), argsReader);
        }

        public string GetClientHandle() => _clientHandle ?? (_clientHandle = _pipe.GetClientHandleAsString());

        public bool Read()
        {
            // First dispose the client handle if we asked for one
            // If we don't do this we won't get notified when the stream closes, see https://stackoverflow.com/q/39682602/807064
            if (_clientHandle != null)
            {
                _pipe.DisposeLocalCopyOfClientHandle();
                _clientHandle = null;                
            }

            // Now read one message from the stream
            try
            {
                BuildEventArgs args = _read();
                if (args != null)
                {
                    Dispatch(args);
                    return true;
                }
            }
            catch(EndOfStreamException)
            {
                // Nothing else to read
            }
            return false;
        }

        public void Dispose()
        {
            _binaryReader.Dispose();
            _pipe.Dispose();
        }
    }
}
