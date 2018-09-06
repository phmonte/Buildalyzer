using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Buildalyzer.Logging
{
    internal class BinaryLogReader
    {
        public BinaryLogReader()
        {

        }

        public List<AnalyzerResult> Results { get; } = new List<AnalyzerResult>();

        public void Read(string logFile)
        {
            BinaryLogReplayEventSource binLogReader = new BinaryLogReplayEventSource();
            binLogReader.AnyEventRaised += BinLogReader_AnyEventRaised;
            binLogReader.BuildFinished += BinLogReader_BuildFinished;
            binLogReader.Replay(logFile);
        }

        private void BinLogReader_AnyEventRaised(object sender, BuildEventArgs e)
        {
            if(e.Message.Contains("Csc"))
            {
                int test = 0;
            }
        }

        private void BinLogReader_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            bool succeeded = e.Succeeded;
        }
    }
}
