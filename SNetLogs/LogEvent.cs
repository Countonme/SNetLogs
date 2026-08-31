using System;
using System.Collections.Generic;
using System.Text;

namespace SNetLogs
{
    public sealed class LogEvent
    {
        public string Module;

        public string Level;

        public string Message;

        public string TraceId;

        public string ProcessName;

        public string ClassName;

        public string MethodName;

        public int LineNumber;

        public int ThreadId;

        public DateTime Time;
    }
}