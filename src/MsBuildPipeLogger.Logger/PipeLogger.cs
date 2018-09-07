using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;

namespace MsBuildPipeLogger.Logger
{
    /// <summary>
    /// Logger to send messages from the MSBuild logging system over an anonymous pipe.
    /// </summary>
    /// <remarks>
    /// Heavily based on the work of Kirill Osenkov and the MSBuildStructuredLog project.
    /// </remarks>
    public class PipeLogger : Microsoft.Build.Utilities.Logger
    {
        private PipeStream _pipe;
        private BinaryWriter _binaryWriter;

        public override void Initialize(IEventSource eventSource)
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");
            Environment.SetEnvironmentVariable("MSBUILDLOGIMPORTS", "1");

            // Open the pipe and writer
            _pipe = new AnonymousPipeClientStream(PipeDirection.Out, GetPipeHandleFromParameters());
            _binaryWriter = new BinaryWriter(_pipe);
            BuildEventArgsWriter argsWriter = new BuildEventArgsWriter(_binaryWriter);

            // Register the any event to capture all logger outputs
            eventSource.AnyEventRaised += (_, e) => argsWriter.Write(e);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            try
            {
                _pipe.WaitForPipeDrain();
                _binaryWriter.Dispose();
                _pipe.Dispose();
            }
            catch { }
        }

        private string GetPipeHandleFromParameters()
        {
            string[] parameters = Parameters.Split(';');
            if (parameters.Length != 1)
            {
                throw new LoggerException("Unexpected number of parameters");
            }
            return parameters[0].Trim().Trim('"').Trim();
        }
    }
}
