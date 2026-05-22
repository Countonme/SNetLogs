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
    /// SNetLogs V2 - Enterprise Industrial Logger
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

        // 高性能队列
        private static readonly BlockingCollection<LogEvent> queue =
            new BlockingCollection<LogEvent>(new ConcurrentQueue<LogEvent>());

        private static readonly object fileLock = new object();

        #endregion Fields

        #region Init

        static Log()
        {
            Init();

            Thread worker = new Thread(Consume);
            worker.IsBackground = true;
            worker.Start();

            Thread flushTimer = new Thread(TimerFlush);
            flushTimer.IsBackground = true;
            flushTimer.Start();
        }

        #endregion Init

        #region Config Init（自动生成 + 修复）

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
                            File.ReadAllText(ConfigFile, Encoding.UTF8));
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
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions
                {
                    WriteIndented = true
                }),
                Encoding.UTF8);
        }

        #endregion Config Init（自动生成 + 修复）

        #region Public API（V4增强）

        // ===== 简单模式 =====
        public static string NewTraceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static void Info(string msg)
            => Write("GENERAL", "INFO", msg, null);

        public static void Error(string msg)
            => Write("GENERAL", "ERROR", msg, null);

        public static void Fatal(string msg)
            => Write("GENERAL", "FATAL", msg, null);

        // ===== 模块模式 =====
        public static void Info(string module, string msg)
            => Write(module, "INFO", msg, null);

        public static void Error(string module, string msg)
            => Write(module, "ERROR", msg, null);

        public static void Fatal(string module, string msg)
            => Write(module, "FATAL", msg, null);

        // ===== Trace链路模式（V4核心）=====
        public static void Info(string module, string traceId, string msg)
            => Write(module, "INFO", msg, traceId);

        public static void Error(string module, string traceId, string msg)
            => Write(module, "ERROR", msg, traceId);

        public static void Fatal(string module, string traceId, string msg)
            => Write(module, "FATAL", msg, traceId);

        #endregion Public API（V4增强）

        #region Core Write

        private static void Write(string module, string level, string message, string traceId)
        {
            if (config == null) return;

            queue.Add(new LogEvent
            {
                Module = module ?? "GENERAL",
                Level = level,
                Message = message,
                TraceId = traceId,
                Time = DateTime.Now
            });

            if (config.EnableConsole)
                WriteConsole(module, level, message, traceId);
        }

        #endregion Core Write

        #region Worker

        private static void Consume()
        {
            var buffer = new List<LogEvent>(config.BatchSize);

            foreach (var item in queue.GetConsumingEnumerable())
            {
                buffer.Add(item);

                if (buffer.Count >= config.BatchSize)
                {
                    Flush(buffer);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
                Flush(buffer);
        }

        #endregion Worker

        #region Timer Flush

        private static void TimerFlush()
        {
            while (true)
            {
                Thread.Sleep(2000);
                Flush(new List<LogEvent>());
            }
        }

        #endregion Timer Flush

        #region Flush Engine（V4优化）

        private static void Flush(List<LogEvent> logs)
        {
            if (!config.EnableFile) return;
            if (logs == null || logs.Count == 0) return;

            lock (fileLock)
            {
                var grouped = Group(logs);

                foreach (var kv in grouped)
                {
                    string dir = Path.Combine(BaseDir, config.LogDirectory, kv.Key);
                    Directory.CreateDirectory(dir);

                    string file = Path.Combine(
                        dir,
                        DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                    using (var sw = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        foreach (var log in kv.Value)
                        {
                            sw.WriteLine(Format(log));
                        }
                    }
                }
            }
        }

        #endregion Flush Engine（V4优化）

        #region Console

        private static void WriteConsole(string module, string level, string msg, string traceId)
        {
            lock (fileLock)
            {
                ConsoleColor color = ConsoleColor.White;

                if (level == "INFO") color = ConsoleColor.Green;
                else if (level == "ERROR") color = ConsoleColor.Red;
                else if (level == "FATAL") color = ConsoleColor.DarkRed;
                else if (level == "DEBUG") color = ConsoleColor.Gray;

                Console.ForegroundColor = color;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] [{config.Environment}] [{module}] [{level}] " +
                    (traceId != null ? $"[Trace:{traceId}] " : "") +
                    msg);

                Console.ResetColor();
            }
        }

        #endregion Console

        #region Helpers

        private static string Format(LogEvent e)
        {
            return $"[{e.Time:yyyy-MM-dd HH:mm:ss.fff}] " +
                   $"[{config.Environment}] " +
                   $"[{e.Module}] " +
                   $"[{e.Level}] " +
                   (e.TraceId != null ? $"[Trace:{e.TraceId}] " : "") +
                   e.Message;
        }

        private static Dictionary<string, List<LogEvent>> Group(List<LogEvent> logs)
        {
            var dict = new Dictionary<string, List<LogEvent>>();

            foreach (var l in logs)
            {
                if (!dict.ContainsKey(l.Module))
                    dict[l.Module] = new List<LogEvent>();

                dict[l.Module].Add(l);
            }

            return dict;
        }

        private static void CreateDirs()
        {
            Directory.CreateDirectory(Path.Combine(BaseDir, config.LogDirectory));
        }

        private static void ClearExpiredLogs()
        {
            try
            {
                string root = Path.Combine(BaseDir, config.LogDirectory);

                if (!Directory.Exists(root)) return;

                foreach (var f in Directory.GetFiles(root, "*.log", SearchOption.AllDirectories))
                {
                    if (File.GetCreationTime(f) < DateTime.Now.AddDays(-config.KeepDays))
                        File.Delete(f);
                }
            }
            catch { }
        }

        #endregion Helpers

        #region Model

        private class LogEvent
        {
            public string Module;
            public string Level;
            public string Message;
            public string TraceId;
            public DateTime Time;
        }

        #endregion Model
    }
}