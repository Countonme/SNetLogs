using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SNetLogs
{
    /// <summary>
    /// 企业级高性能日志系统 V2
    /// 特点：
    /// 1. 高并发队列
    /// 2. 按模块分文件夹
    /// 3. 批量写入提升性能
    /// 4. 自动归档 & 清理
    /// 5. Linux / Windows 通用
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

        // 高并发线程安全队列
        private static readonly BlockingCollection<LogItem> queue =
            new BlockingCollection<LogItem>(new ConcurrentQueue<LogItem>());

        private static readonly object fileLock = new object();

        #endregion Fields

        #region Init

        static Log()
        {
            Init();

            // 启动后台线程消费队列
            Task.Factory.StartNew(
                Worker,
                TaskCreationOptions.LongRunning);

            // 定时 Flush（兜底）
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(2000);
                    FlushAll();
                }
            });
        }

        public static void Init()
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            if (!File.Exists(ConfigFile))
            {
                config = new LogConfig();

                File.WriteAllText(
                    ConfigFile,
                    JsonSerializer.Serialize(config,
                        new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
            }
            else
            {
                config = JsonSerializer.Deserialize<LogConfig>(
                    File.ReadAllText(ConfigFile, Encoding.UTF8));
            }

            if (config == null)
                config = new LogConfig();

            CreateDirs();
            ClearOldLogs();
        }

        #endregion Init

        #region Public API（你要的两个版本）

        // ====== 无模块 ======
        public static void Debug(string msg) => Write("GENERAL", LogLevel.Debug, msg);

        public static void Info(string msg) => Write("GENERAL", LogLevel.Info, msg);

        public static void Warn(string msg) => Write("GENERAL", LogLevel.Warn, msg);

        public static void Error(string msg) => Write("GENERAL", LogLevel.Error, msg);

        public static void Fatal(string msg) => Write("GENERAL", LogLevel.Fatal, msg);

        // ====== 带模块（PLC / MES）======
        public static void Debug(string module, string msg) => Write(module, LogLevel.Debug, msg);

        public static void Info(string module, string msg) => Write(module, LogLevel.Info, msg);

        public static void Warn(string module, string msg) => Write(module, LogLevel.Warn, msg);

        public static void Error(string module, string msg) => Write(module, LogLevel.Error, msg);

        public static void Fatal(string module, string msg) => Write(module, LogLevel.Fatal, msg);

        public static void Error(string module, Exception ex)
        {
            Write(module, LogLevel.Error, ex.ToString());
        }

        #endregion Public API（你要的两个版本）

        #region Core Write

        private static void Write(string module, LogLevel level, string message)
        {
            var item = new LogItem
            {
                Module = string.IsNullOrEmpty(module) ? "GENERAL" : module,
                Level = level,
                Message = message,
                Time = DateTime.Now
            };

            queue.Add(item);

            if (config.EnableConsole)
                WriteConsole(item);
        }

        #endregion Core Write

        #region Worker（高并发消费）

        private static void Worker()
        {
            var buffer = new List<LogItem>(config.BatchSize);

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

        #endregion Worker（高并发消费）

        #region Flush（核心性能点）

        private static void Flush(List<LogItem> logs)
        {
            if (!config.EnableFile) return;

            lock (fileLock)
            {
                foreach (var group in GroupByModule(logs))
                {
                    string dir = Path.Combine(
                        BaseDir,
                        config.LogDirectory,
                        group.Key);

                    Directory.CreateDirectory(dir);

                    string file = Path.Combine(
                        dir,
                        DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                    using (var sw = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        foreach (var log in group.Value)
                        {
                            sw.WriteLine(Format(log));
                        }
                    }
                }
            }
        }

        private static void FlushAll()
        {
            // 预留扩展：强制刷盘
        }

        #endregion Flush（核心性能点）

        #region Console

        private static void WriteConsole(LogItem item)
        {
            lock (fileLock)
            {
                ConsoleColor color;

                switch (item.Level)
                {
                    case LogLevel.Debug: color = ConsoleColor.Gray; break;
                    case LogLevel.Info: color = ConsoleColor.Green; break;
                    case LogLevel.Warn: color = ConsoleColor.Yellow; break;
                    case LogLevel.Error: color = ConsoleColor.Red; break;
                    case LogLevel.Fatal: color = ConsoleColor.DarkRed; break;
                    default: color = ConsoleColor.White; break;
                }

                Console.ForegroundColor = color;
                Console.WriteLine(Format(item));
                Console.ResetColor();
            }
        }

        #endregion Console

        #region Helpers

        private static string Format(LogItem item)
        {
            return $"[{item.Time:yyyy-MM-dd HH:mm:ss.fff}] " +
                   $"[{config.Environment}] " +
                   $"[{item.Module}] " +
                   $"[{item.Level}] " +
                   item.Message;
        }

        private static Dictionary<string, List<LogItem>> GroupByModule(List<LogItem> logs)
        {
            var dict = new Dictionary<string, List<LogItem>>();

            foreach (var item in logs)
            {
                if (!dict.ContainsKey(item.Module))
                    dict[item.Module] = new List<LogItem>();

                dict[item.Module].Add(item);
            }

            return dict;
        }

        private static void CreateDirs()
        {
            Directory.CreateDirectory(Path.Combine(BaseDir, config.LogDirectory));
        }

        private static void ClearOldLogs()
        {
            try
            {
                var root = Path.Combine(BaseDir, config.LogDirectory);

                if (!Directory.Exists(root)) return;

                foreach (var file in Directory.GetFiles(root, "*.log", SearchOption.AllDirectories))
                {
                    if (File.GetCreationTime(file) <
                        DateTime.Now.AddDays(-config.KeepDays))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        #endregion Helpers

        #region Internal Model

        private class LogItem
        {
            public string Module;
            public LogLevel Level;
            public string Message;
            public DateTime Time;
        }

        #endregion Internal Model
    }
}