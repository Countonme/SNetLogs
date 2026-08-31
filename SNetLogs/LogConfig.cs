using System;
using System.Collections.Generic;
using System.Text;

namespace SNetLogs
{
    public class LogConfig
    {
        public string Environment { get; set; }

        public bool EnableConsole { get; set; }

        public bool EnableFile { get; set; }

        public int BatchSize { get; set; }

        public int FlushIntervalMs { get; set; }

        public int KeepDays { get; set; }

        public int QueueCapacity { get; set; }

        public string LogDirectory { get; set; }

        public string ArchiveDirectory { get; set; }
    }
}