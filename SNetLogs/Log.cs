using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SNetLogs
{
    /// <summary>
    /// SNetLogs V5 - Enterprise Industrial Logger
    /// C# 7.3 Compatible
    /// </summary>
    public static class Log
    {
        #region Fields

        private static LogConfig config;

        private static readonly string BaseDir =
            AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string ConfigDir =
            Path.Combine(BaseDir, "Common", "Logs");

        private static readonly string ConfigFile =
            Path.Combine(ConfigDir, "logs.json");

        // 日志队列
        private static readonly BlockingCollection<LogEvent> queue =
            new BlockingCollection<LogEvent>(
                new ConcurrentQueue<LogEvent>());

        // 缓冲区
        private static readonly List<LogEvent> pendingBuffer =
            new List<LogEvent>();

        private static readonly object bufferLock =
            new object();

        private static readonly object fileLock =
            new object();

        #endregion Fields

        #region Init

        static Log()
        {
            Init();

            // 消费线程
            Thread worker = new Thread(Consume);
            worker.IsBackground = true;
            worker.Start();

            // 定时Flush
            Thread flushTimer = new Thread(TimerFlush);
            flushTimer.IsBackground = true;
            flushTimer.Start();

            // 自动清理过期日志
            Thread cleanTimer = new Thread(TimerClearExpiredLogs);
            cleanTimer.IsBackground = true;
            cleanTimer.Start();
        }

        #endregion Init

        #region Config Init

        public static void Init()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                if (!File.Exists(ConfigFile))
                {
                    config = DefaultConfig();
                    SaveConfig(config);
                }
                else
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<LogConfig>(
                            File.ReadAllText(
                                ConfigFile,
                                Encoding.UTF8));
                    }
                    catch
                    {
                        config = DefaultConfig();
                        SaveConfig(config);
                    }
                }

                if (config == null)
                    config = DefaultConfig();

                CreateDirs();

                // 启动时先清理一次
                ClearExpiredLogs();
            }
            catch
            {
                config = DefaultConfig();
            }
        }

        private static LogConfig DefaultConfig()
        {
            return new LogConfig
            {
                Environment = "DEV",
                EnableConsole = true,
                EnableFile = true,
                BatchSize = 80,
                KeepDays = 7,
                LogDirectory = "Logs",
                ArchiveDirectory = "Archive"
            };
        }

        private static void SaveConfig(LogConfig cfg)
        {
            File.WriteAllText(
                ConfigFile,
                JsonSerializer.Serialize(
                    cfg,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                Encoding.UTF8);
        }

        #endregion Config Init

        #region Public API

        public static string NewTraceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        // ===== Simple =====

        public static void Info(string msg)
            => Write("GENERAL", "INFO", msg, null);

        public static void Error(string msg)
            => Write("GENERAL", "ERROR", msg, null);

        public static void Fatal(string msg)
            => Write("GENERAL", "FATAL", msg, null);

        // ===== Module =====

        public static void Info(string module, string msg)
            => Write(module, "INFO", msg, null);

        public static void Error(string module, string msg)
            => Write(module, "ERROR", msg, null);

        public static void Fatal(string module, string msg)
            => Write(module, "FATAL", msg, null);

        // ===== Trace =====

        public static void Info(
            string module,
            string traceId,
            string msg)
            => Write(module, "INFO", msg, traceId);

        public static void Error(
            string module,
            string traceId,
            string msg)
            => Write(module, "ERROR", msg, traceId);

        public static void Fatal(
            string module,
            string traceId,
            string msg)
            => Write(module, "FATAL", msg, traceId);

        #endregion Public API

        #region Core Write

        private static void Write(
            string module,
            string level,
            string message,
            string traceId)
        {
            if (config == null)
                return;

            queue.Add(new LogEvent
            {
                Module = module ?? "GENERAL",
                Level = level,
                Message = message,
                TraceId = traceId,
                Time = DateTime.Now
            });

            if (config.EnableConsole)
                WriteConsole(
                    module,
                    level,
                    message,
                    traceId);
        }

        #endregion Core Write

        #region Consume Worker

        private static void Consume()
        {
            foreach (var item in queue.GetConsumingEnumerable())
            {
                lock (bufferLock)
                {
                    pendingBuffer.Add(item);

                    // 达到批量阈值立即写入
                    if (pendingBuffer.Count >= config.BatchSize)
                    {
                        FlushInternal(
                            new List<LogEvent>(pendingBuffer));

                        pendingBuffer.Clear();
                    }
                }
            }
        }

        #endregion Consume Worker

        #region Timer Flush

        private static void TimerFlush()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);

                    lock (bufferLock)
                    {
                        if (pendingBuffer.Count > 0)
                        {
                            FlushInternal(
                                new List<LogEvent>(pendingBuffer));

                            pendingBuffer.Clear();
                        }
                    }
                }
                catch
                {
                }
            }
        }

        #endregion Timer Flush

        #region Auto Clear Logs

        private static void TimerClearExpiredLogs()
        {
            while (true)
            {
                try
                {
                    // 每1小时扫描一次
                    Thread.Sleep(TimeSpan.FromHours(1));

                    ClearExpiredLogs();
                }
                catch
                {
                }
            }
        }

        #endregion Auto Clear Logs

        #region Flush Engine

        private static void FlushInternal(
            List<LogEvent> logs)
        {
            if (!config.EnableFile)
                return;

            if (logs == null || logs.Count == 0)
                return;

            lock (fileLock)
            {
                var grouped = Group(logs);

                foreach (var kv in grouped)
                {
                    try
                    {
                        string dir = Path.Combine(
                            BaseDir,
                            config.LogDirectory,
                            kv.Key);

                        Directory.CreateDirectory(dir);

                        string file = Path.Combine(
                            dir,
                            DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                        using (var sw = new StreamWriter(
                            file,
                            true,
                            Encoding.UTF8))
                        {
                            foreach (var log in kv.Value)
                            {
                                sw.WriteLine(Format(log));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        #endregion Flush Engine

        #region Console

        private static void WriteConsole(
            string module,
            string level,
            string msg,
            string traceId)
        {
            lock (fileLock)
            {
                ConsoleColor color = ConsoleColor.White;

                if (level == "INFO")
                    color = ConsoleColor.Green;
                else if (level == "ERROR")
                    color = ConsoleColor.Red;
                else if (level == "FATAL")
                    color = ConsoleColor.DarkRed;
                else if (level == "DEBUG")
                    color = ConsoleColor.Gray;

                Console.ForegroundColor = color;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    $"[{config.Environment}] " +
                    $"[{module}] " +
                    $"[{level}] " +
                    (traceId != null
                        ? $"[Trace:{traceId}] "
                        : "") +
                    msg);

                Console.ResetColor();
            }
        }

        #endregion Console

        #region Helpers

        private static string Format(LogEvent e)
        {
            return
                $"[{e.Time:yyyy-MM-dd HH:mm:ss.fff}] " +
                $"[{config.Environment}] " +
                $"[{e.Module}] " +
                $"[{e.Level}] " +
                (e.TraceId != null
                    ? $"[Trace:{e.TraceId}] "
                    : "") +
                e.Message;
        }

        private static Dictionary<string, List<LogEvent>> Group(
            List<LogEvent> logs)
        {
            var dict =
                new Dictionary<string, List<LogEvent>>();

            foreach (var l in logs)
            {
                if (!dict.ContainsKey(l.Module))
                    dict[l.Module] =
                        new List<LogEvent>();

                dict[l.Module].Add(l);
            }

            return dict;
        }

        private static void CreateDirs()
        {
            Directory.CreateDirectory(
                Path.Combine(
                    BaseDir,
                    config.LogDirectory));
        }

        private static void ClearExpiredLogs()
        {
            try
            {
                string root = Path.Combine(
                    BaseDir,
                    config.LogDirectory);

                if (!Directory.Exists(root))
                    return;

                foreach (var file in Directory.GetFiles(
                    root,
                    "*.log",
                    SearchOption.AllDirectories))
                {
                    try
                    {
                        DateTime createTime =
                            File.GetCreationTime(file);

                        if (createTime <
                            DateTime.Now.AddDays(-config.KeepDays))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        #endregion Helpers

        #region Models

        private class LogEvent
        {
            public string Module;
            public string Level;
            public string Message;
            public string TraceId;
            public DateTime Time;
        }

        public class LogConfig
        {
            public string Environment { get; set; }

            public bool EnableConsole { get; set; }

            public bool EnableFile { get; set; }

            public int BatchSize { get; set; }

            public int KeepDays { get; set; }

            public string LogDirectory { get; set; }

            public string ArchiveDirectory { get; set; }
        }

        #endregion Models
    }
}