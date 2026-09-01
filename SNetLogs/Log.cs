using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SNetLogs
{
    /// <summary>
    /// SNetLogs V6
    ///
    /// 高并发工业日志系统
    ///
    /// 核心设计：By Shengbi Dev
    /// 更多软件定制开发 请联系 Email:yysvent@163.com
    /// 1. 业务线程不执行文件 IO
    /// 2. 业务线程不执行 Console IO
    /// 3. BlockingCollection 有界队列
    /// 4. 单消费者模型
    /// 5. 批量写入
    /// 6. 批量不足时按时间 Flush
    /// 7. 自动记录程序 / 类 / 方法 / 行号 / ThreadId
    /// 8. 支持 TraceId
    /// 9. 支持 Exception
    /// 10. 自动清理过期日志
    /// 11. C# 7.3 Compatible
    /// </summary>
    public static class Log
    {
        #region Constants

        private const string GENERAL = "General";

        private const string DEBUG = "DEBUG";

        private const string INFO = "INFO";

        private const string WARN = "WARN";

        private const string ERROR = "ERROR";

        private const string FATAL = "FATAL";

        #endregion Constants

        #region Fields

        private static volatile LogConfig config;

        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string ConfigDir = Path.Combine(BaseDir, "Common", "Logs");

        private static readonly string ConfigFile = Path.Combine(ConfigDir, "logs.json");

        /// <summary>
        /// 程序名称
        /// </summary>
        private static readonly string ProcessName = GetProcessName();

        /// <summary>
        /// 日志队列
        ///
        /// 注意：
        /// 队列容量在静态构造函数中根据配置创建。
        /// </summary>
        private static BlockingCollection<LogEvent> queue;

        /// <summary>
        /// 消费线程
        /// </summary>
        private static Thread consumerThread;

        /// <summary>
        /// 清理线程
        /// </summary>
        private static Thread cleanThread;

        /// <summary>
        /// 文件锁
        ///
        /// 正常情况下只有 Consumer 使用。
        /// 主要用于防止外部特殊调用导致并发写文件。
        /// </summary>
        private static readonly object fileLock = new object();

        /// <summary>
        /// Console 锁
        /// </summary>
        private static readonly object consoleLock = new object();

        /// <summary>
        /// 是否初始化完成
        /// </summary>
        private static volatile bool initialized;

        /// <summary>
        /// 是否停止
        /// </summary>
        private static volatile bool stopping;

        /// <summary>
        /// 丢弃日志数量
        /// </summary>
        private static long droppedLogs;

        /// <summary>
        /// 已写入日志数量
        /// </summary>
        private static long writtenLogs;

        /// <summary>
        /// 写入失败数量
        /// </summary>
        private static long failedLogs;

        #endregion Fields

        #region Static Constructor

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static Log()
        {
            try
            {
                Init();
                NormalizeConfig();
            }
            catch
            {
                config = DefaultConfig();
                NormalizeConfig();
            }

            try
            {
                queue = new BlockingCollection<LogEvent>(
                    new ConcurrentQueue<LogEvent>(),
                    config.QueueCapacity);

                CreateDirs();

                ClearExpiredLogs();
            }
            catch
            {
                // 即使目录清理等操作失败，也不能影响日志 Consumer
                if (config == null)
                {
                    config = DefaultConfig();
                }

                if (queue == null)
                {
                    queue = new BlockingCollection<LogEvent>(
                        new ConcurrentQueue<LogEvent>(),
                        config.QueueCapacity);
                }
            }

            // ============================================
            // 无论前面是否异常，都必须启动 Consumer
            // ============================================

            initialized = true;

            consumerThread = new Thread(Consume)
            {
                IsBackground = true,
                Name = "SNetLogs-Consumer",
                Priority = ThreadPriority.AboveNormal
            };

            consumerThread.Start();

            // ============================================
            // 清理线程
            // ============================================

            cleanThread = new Thread(TimerClearExpiredLogs)
            {
                IsBackground = true,
                Name = "SNetLogs-Clean"
            };

            cleanThread.Start();
        }

        #endregion Static Constructor

        #region Initialization

        /// <summary>
        /// 初始化日志配置
        /// </summary>
        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);

                if (!File.Exists(ConfigFile))
                {
                    config = DefaultConfig();

                    SafeSaveConfig(config);
                }
                else
                {
                    try
                    {
                        string json = File.ReadAllText(ConfigFile, Encoding.UTF8);

                        config = JsonSerializer.Deserialize<LogConfig>(json);
                    }
                    catch
                    {
                        config = DefaultConfig();

                        SafeSaveConfig(config);
                    }
                }

                if (config == null)
                {
                    config = DefaultConfig();
                }

                NormalizeConfig();
            }
            catch
            {
                config = DefaultConfig();
            }
        }

        /// <summary>
        /// 默认配置
        /// </summary>
        private static LogConfig DefaultConfig()
        {
            return new LogConfig
            {
                Environment = "DEV",

                EnableConsole = true,

                EnableFile = true,

                // 达到 500 条立即写
                BatchSize = 500,

                // 不足 500 条最多等待 2 秒
                FlushIntervalMs = 2000,

                // 保存 7 天
                KeepDays = 7,

                // 最大队列
                QueueCapacity = 50000,

                LogDirectory = "Logs",

                ArchiveDirectory = "Archive"
            };
        }

        /// <summary>
        /// 配置规范化
        /// </summary>
        private static void NormalizeConfig()
        {
            if (config == null)
            {
                config = DefaultConfig();

                return;
            }

            if (string.IsNullOrWhiteSpace(config.Environment))
            {
                config.Environment = "DEV";
            }

            if (config.BatchSize <= 0)
            {
                config.BatchSize = 500;
            }

            if (config.FlushIntervalMs <= 0)
            {
                config.FlushIntervalMs = 2000;
            }

            if (config.KeepDays <= 0)
            {
                config.KeepDays = 7;
            }

            if (config.QueueCapacity <= 0)
            {
                config.QueueCapacity = 50000;
            }

            if (string.IsNullOrWhiteSpace(config.LogDirectory))
            {
                config.LogDirectory = "Logs";
            }

            if (string.IsNullOrWhiteSpace(config.ArchiveDirectory))
            {
                config.ArchiveDirectory = "Archive";
            }

            // 防止配置异常导致内存压力
            if (config.QueueCapacity < 1000)
            {
                config.QueueCapacity = 1000;
            }

            if (config.BatchSize > config.QueueCapacity)
            {
                config.BatchSize = Math.Min(500, config.QueueCapacity);
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private static void SafeSaveConfig(
            LogConfig cfg)
        {
            try
            {
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, new JsonSerializerOptions
                {
                    WriteIndented = true
                }), Encoding.UTF8);
            }
            catch
            {
                // 日志系统不能影响业务
            }
        }

        #endregion Initialization

        #region Simple API

        /// <summary>
        /// 简单日志 API
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Debug(string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(GENERAL, DEBUG, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 简单日志 API
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Info(string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(GENERAL, INFO, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 简单日志 API
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Warn(string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(GENERAL, WARN, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 简单日志 API
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Error(string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(GENERAL, ERROR, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 简单日志 API
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Fatal(string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(GENERAL, FATAL, msg, null, filePath, memberName, lineNumber);
        }

        #endregion Simple API

        #region Module API

        /// <summary>
        /// 模块日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Debug(string module, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, DEBUG, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 模块日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Info(string module, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, INFO, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 模块日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Warn(string module, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, WARN, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 模块日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Error(string module, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, ERROR, msg, null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// 模块日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Fatal(string module, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, FATAL, msg, null, filePath, memberName, lineNumber);
        }

        #endregion Module API

        #region Trace API

        /// <summary>
        /// Trace 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="traceId"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Debug(string module, string traceId, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, DEBUG, msg, traceId, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Trace 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="traceId"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Info(string module, string traceId, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, INFO, msg, traceId, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Trace 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="traceId"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Warn(string module, string traceId, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, WARN, msg, traceId, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Trace 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="traceId"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Error(string module, string traceId, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, ERROR, msg, traceId, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Trace 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="traceId"></param>
        /// <param name="msg"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Fatal(string module, string traceId, string msg, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, FATAL, msg, traceId, filePath, memberName, lineNumber);
        }

        #endregion Trace API

        #region Exception API

        /// <summary>
        /// Exception 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Error(string module, Exception ex, string message = null, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, ERROR, BuildExceptionMessage(message, ex), null, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Exception 日志 API
        /// </summary>
        /// <param name="module"></param>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <param name="filePath"></param>
        /// <param name="memberName"></param>
        /// <param name="lineNumber"></param>
        public static void Fatal(string module, Exception ex, string message = null, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Write(module, FATAL, BuildExceptionMessage(message, ex), null, filePath, memberName, lineNumber);
        }

        #endregion Exception API

        #region Core Write

        /// <summary>
        /// 核心日志入口
        ///
        /// 注意：
        /// 这里绝对不进行：
        ///
        /// Directory.CreateDirectory
        /// File.Write
        /// StreamWriter
        /// Console.WriteLine
        ///
        /// 所有 IO 都交给 Consumer。
        /// </summary>
        private static void Write(string module, string level, string message, string traceId, string filePath, string memberName, int lineNumber)
        {
            if (!initialized)
                return;

            if (queue == null)
                return;

            try
            {
                // 如果文件和 Console 都关闭，
                // 根本没有必要进入队列。
                if (!config.EnableFile && !config.EnableConsole)
                {
                    return;
                }

                LogEvent item = new LogEvent
                {
                    Module = string.IsNullOrWhiteSpace(module) ? GENERAL : module,

                    Level = string.IsNullOrWhiteSpace(level) ? INFO : level,

                    Message = message ?? string.Empty,

                    TraceId = traceId,

                    ProcessName = ProcessName,

                    ClassName = GetClassName(filePath),

                    MethodName = string.IsNullOrWhiteSpace(memberName) ? "Unknown" : memberName,

                    LineNumber = lineNumber,

                    ThreadId = Thread.CurrentThread.ManagedThreadId,

                    Time = DateTime.Now
                };

                // ============================================
                // 非阻塞入队
                // ============================================

                if (!queue.TryAdd(item))
                {
                    Interlocked.Increment(ref droppedLogs);
                }
            }
            catch
            {
                // 日志系统绝对不能影响业务系统
            }
        }

        #endregion Core Write

        #region Consumer

        /// <summary>
        /// 单消费者
        ///
        /// 逻辑：
        ///
        /// 1. 等第一条日志
        /// 2. 收集更多日志
        /// 3. 达到 BatchSize 立即 Flush
        /// 4. 如果达不到，最多等待 FlushIntervalMs
        ///
        /// 因此：
        ///
        /// BatchSize = 500
        /// FlushIntervalMs = 2000
        ///
        /// 不是说必须有 500 条才写。
        ///
        /// 例如只有 10 条：
        ///
        /// 10 条
        /// ↓
        /// 最多等待 2 秒
        /// ↓
        /// 写入
        /// </summary>
        private static void Consume()
        {
            while (!stopping)
            {
                try
                {
                    List<LogEvent> batch = new List<LogEvent>(config.BatchSize);

                    LogEvent first;

                    // ========================================
                    // 等待第一条日志
                    // ========================================

                    if (!queue.TryTake(out first, config.FlushIntervalMs))
                    {
                        continue;
                    }

                    batch.Add(first);

                    // ========================================
                    // 尽可能继续拿
                    //
                    // 注意：
                    // TryTake(..., 0)
                    // 不会阻塞
                    // ========================================

                    while (batch.Count < config.BatchSize && queue.TryTake(out LogEvent item, 0))
                    {
                        batch.Add(item);
                    }

                    // ========================================
                    // 批量写入
                    // ========================================

                    Flush(batch);
                }
                catch
                {
                    // Consumer 不允许因为单次异常退出
                }
            }
        }

        #endregion Consumer

        #region Flush

        /// <summary>
        /// 批量 Flush
        /// </summary>
        private static void Flush(List<LogEvent> logs)
        {
            if (logs == null || logs.Count == 0)
            {
                return;
            }

            // ============================================
            // 文件
            // ============================================

            if (config.EnableFile)
            {
                WriteFiles(logs);
            }

            // ============================================
            // Console
            // ============================================

            if (config.EnableConsole)
            {
                WriteConsoleBatch(logs);
            }
        }

        /// <summary>
        /// 批量写文件
        /// </summary>
        private static void WriteFiles(List<LogEvent> logs)
        {
            try
            {
                lock (fileLock)
                {
                    // ========================================
                    // 手动分组
                    //
                    // 避免 LINQ GroupBy / OrderBy
                    // ========================================

                    Dictionary<string, List<LogEvent>> groups = new Dictionary<string, List<LogEvent>>(StringComparer.OrdinalIgnoreCase);

                    foreach (LogEvent log in logs)
                    {
                        string date = log.Time.ToString("yyyy-MM-dd");

                        string module = SanitizeFileName(log.Module);

                        string key = module + "|" + date;

                        List<LogEvent> list;

                        if (!groups.TryGetValue(key, out list))
                        {
                            list = new List<LogEvent>();

                            groups.Add(key, list);
                        }

                        list.Add(log);
                    }

                    // ========================================
                    // 写入
                    // ========================================

                    foreach (KeyValuePair<string, List<LogEvent>> group in groups)
                    {
                        try
                        {
                            List<LogEvent> list = group.Value;

                            if (list.Count == 0)
                                continue;

                            LogEvent first = list[0];

                            string module = SanitizeFileName(first.Module);

                            string date = first.Time.ToString("yyyy-MM-dd");

                            string dir = Path.Combine(BaseDir, config.LogDirectory, module);

                            Directory.CreateDirectory(dir);

                            string file = Path.Combine(dir, date + ".log");

                            using (StreamWriter sw = new StreamWriter(file, true, new UTF8Encoding(false), 65536))
                            {
                                foreach (LogEvent log in list)
                                {
                                    sw.WriteLine(Format(log));
                                }
                            }
                            Interlocked.Add(ref writtenLogs, list.Count);
                        }
                        catch
                        {
                            Interlocked.Add(ref failedLogs, group.Value.Count);
                        }
                    }
                }
            }
            catch
            {
                Interlocked.Add(ref failedLogs, logs.Count);
            }
        }

        #endregion Flush

        #region Console

        /// <summary>
        /// 批量 Console 输出
        ///
        /// 重点：
        /// Console 已经不在业务线程执行。
        /// </summary>
        private static void WriteConsoleBatch(List<LogEvent> logs)
        {
            try
            {
                lock (consoleLock)
                {
                    foreach (LogEvent e in logs)
                    {
                        ConsoleColor color = GetConsoleColor(e.Level);

                        Console.ForegroundColor = color;

                        Console.WriteLine(Format(e));

                        Console.ResetColor();
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 根据日志级别获取 ConsoleColor
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private static ConsoleColor GetConsoleColor(string level)
        {
            switch (level)
            {
                case DEBUG:
                    return ConsoleColor.Gray;

                case INFO:
                    return ConsoleColor.Green;

                case WARN:
                    return ConsoleColor.Yellow;

                case ERROR:
                    return ConsoleColor.Red;

                case FATAL:
                    return ConsoleColor.DarkRed;

                default:
                    return ConsoleColor.White;
            }
        }

        #endregion Console

        #region Format

        /// <summary>
        /// 格式化日志
        /// </summary>
        private static string Format(LogEvent e)
        {
            StringBuilder sb = new StringBuilder(512);

            sb.Append('[');

            sb.Append(e.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            sb.Append("] ");

            sb.Append('[');
            sb.Append(e.Level);
            sb.Append("] ");

            sb.Append('[');
            sb.Append(config.Environment);
            sb.Append("] ");

            sb.Append('[');
            sb.Append(e.ProcessName);
            sb.Append("] ");

            sb.Append('[');
            sb.Append(e.ClassName);
            sb.Append("] ");

            sb.Append('[');
            sb.Append(e.MethodName);
            sb.Append("] ");

            sb.Append("[Line:");
            sb.Append(e.LineNumber);
            sb.Append("] ");

            sb.Append("[T:");
            sb.Append(e.ThreadId);
            sb.Append("] ");

            sb.Append('[');
            sb.Append(e.Module);
            sb.Append("] ");

            if (!string.IsNullOrWhiteSpace(e.TraceId))
            {
                sb.Append("[Trace:");
                sb.Append(e.TraceId);
                sb.Append("] ");
            }

            sb.Append(e.Message);

            return sb.ToString();
        }

        #endregion Format

        #region Exception

        /// <summary>
        /// 构建异常日志信息
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static string BuildExceptionMessage(string message, Exception ex)
        {
            if (ex == null)
            {
                return string.IsNullOrWhiteSpace(message) ? "Exception == null" : message;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return ex.ToString();
            }

            return message + Environment.NewLine + ex;
        }

        #endregion Exception

        #region Helpers

        /// <summary>
        /// 获取当前进程名称
        /// </summary>
        /// <returns></returns>
        private static string GetProcessName()
        {
            try
            {
                string name = AppDomain.CurrentDomain.FriendlyName;

                if (string.IsNullOrWhiteSpace(name))
                {
                    return "UnknownProcess";
                }

                return Path.GetFileNameWithoutExtension(name);
            }
            catch
            {
                return "UnknownProcess";
            }
        }

        /// <summary>
        /// 获取类名
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static string GetClassName(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return "UnknownClass";
                }

                return Path.GetFileNameWithoutExtension(filePath);
            }
            catch
            {
                return "UnknownClass";
            }
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return GENERAL;
            }

            char[] invalid = Path.GetInvalidFileNameChars();

            StringBuilder sb = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 创建日志目录
        /// </summary>
        private static void CreateDirs()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(BaseDir, config.LogDirectory));
            }
            catch
            {
            }
        }

        #endregion Helpers

        #region Clear Expired Logs

        /// <summary>
        /// 定时清理过期日志
        /// </summary>
        private static void TimerClearExpiredLogs()
        {
            while (!stopping)
            {
                try
                {
                    Thread.Sleep(TimeSpan.FromHours(1));

                    if (!stopping)
                    {
                        ClearExpiredLogs();
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 清理过期日志
        /// </summary>
        private static void ClearExpiredLogs()
        {
            try
            {
                if (config == null)
                    return;

                string root = Path.Combine(BaseDir, config.LogDirectory);

                if (!Directory.Exists(root))
                    return;

                DateTime expireTime = DateTime.Now.AddDays(-config.KeepDays);

                string[] files = Directory.GetFiles(root, "*.log", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    try
                    {
                        DateTime lastWrite = File.GetLastWriteTime(file);

                        if (lastWrite < expireTime)
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

        #endregion Clear Expired Logs

        #region Statistics

        /// <summary>
        /// 当前日志队列数量
        /// </summary>
        public static int QueueCount
        {
            get
            {
                return queue == null ? 0 : queue.Count;
            }
        }

        /// <summary>
        /// 当前队列使用率
        /// </summary>
        public static double QueueUsage
        {
            get
            {
                if (queue == null || config == null || config.QueueCapacity <= 0)
                {
                    return 0;
                }

                return (double)queue.Count / config.QueueCapacity;
            }
        }

        /// <summary>
        /// 丢弃日志数量
        /// </summary>
        public static long DroppedLogs
        {
            get
            {
                return Interlocked.Read(ref droppedLogs);
            }
        }

        /// <summary>
        /// 成功写入日志数量
        /// </summary>
        public static long WrittenLogs
        {
            get
            {
                return Interlocked.Read(ref writtenLogs);
            }
        }

        /// <summary>
        /// 写入失败数量
        /// </summary>
        public static long FailedLogs
        {
            get
            {
                return Interlocked.Read(ref failedLogs);
            }
        }

        /// <summary>
        /// 队列容量
        /// </summary>
        public static int QueueCapacity
        {
            get
            {
                return config == null ? 50000 : config.QueueCapacity;
            }
        }

        #endregion Statistics

        #region Shutdown

        /// <summary>
        /// 停止日志系统
        ///
        /// 如果是 WinForms / Windows Service，
        /// 程序退出之前建议调用。
        /// </summary>
        public static void Shutdown()
        {
            if (!initialized)
                return;

            if (stopping)
                return;

            stopping = true;

            try
            {
                if (queue != null && !queue.IsAddingCompleted)
                {
                    queue.CompleteAdding();
                }
            }
            catch
            {
            }

            try
            {
                if (consumerThread != null && consumerThread.IsAlive)
                {
                    consumerThread.Join(5000);
                }
            }
            catch
            {
            }
        }

        #endregion Shutdown
    }
}