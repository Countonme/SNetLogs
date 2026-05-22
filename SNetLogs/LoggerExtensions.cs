using System;
using System.Collections.Generic;
using System.Text;

namespace SNetLogs
{
    public static class LoggerExtensions
    {
        public static string ToLogString(this Exception ex)
        {
            return
                $"Message:{ex.Message}\n" +
                $"StackTrace:{ex.StackTrace}\n" +
                $"Inner:{ex.InnerException}";
        }
    }
}