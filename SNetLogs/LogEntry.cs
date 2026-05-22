using System;

namespace SNetLogs
{
    /// <summary>
    /// 日志实体（用于队列传输）
    /// </summary>
    public class LogEntry
    {
        public DateTime Time { get; set; }

        public string Level { get; set; }

        public string Module { get; set; }

        public string Message { get; set; }

        public string Environment { get; set; }
    }
}