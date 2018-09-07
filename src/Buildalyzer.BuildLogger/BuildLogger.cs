using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Buildalyzer.BuildLogger
{
    public class BuildLogger : Logger
    {
        private PipeStream _pipe;

        public override void Initialize(IEventSource eventSource)
        {
            // Get the pipe handle
            string[] parameters = Parameters.Split(';');
            if(parameters.Length != 1)
            {
                throw new LoggerException("Unexpected number of parameters");
            }
            string handle = parameters[0].Trim().Trim('"').Trim();

            // Open the pipe
            _pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);

            eventSource.MessageRaised += EventSource_MessageRaised;
        }

        private void EventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            using (StreamWriter writer = new StreamWriter(_pipe))
            {
                writer.AutoFlush = true;
                writer.WriteLine(e.Message);
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _pipe.WaitForPipeDrain();
            _pipe.Dispose();
        }
    }
}
