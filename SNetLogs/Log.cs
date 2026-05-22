using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace SNetLogs
{
    public static class Log
    {
        private static readonly object locker = new object();

        private static readonly BlockingCollection<string> logQueue =
            new BlockingCollection<string>();

        private static readonly string BaseDirectory =
            AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string ConfigDirectory =
            Path.Combine(BaseDirectory, "Common", "Logs");

        private static readonly string ConfigPath =
            Path.Combine(ConfigDirectory, "logs.json");

        private static LogConfig config;

        static Log()
        {
            Init();

            Task.Factory.StartNew(
                ProcessQueue,
                TaskCreationOptions.LongRunning);
        }

        public static void Init()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                if (!File.Exists(ConfigPath))
                {
                    config = new LogConfig();

                    var json = JsonSerializer.Serialize(
                        config,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                    File.WriteAllText(
                        ConfigPath,
                        json,
                        Encoding.UTF8);
                }
                else
                {
                    string json =
                        File.ReadAllText(
                            ConfigPath,
                            Encoding.UTF8);

                    config =
                        JsonSerializer.Deserialize<LogConfig>(json);
                }

                if (config == null)
                {
                    config = new LogConfig();
                }

                CreateDirectories();

                ClearExpiredLogs();
            }
            catch
            {
                config = new LogConfig();
            }
        }

        private static void CreateDirectories()
        {
            string logDir =
                Path.Combine(
                    BaseDirectory,
                    config.LogDirectory);

            string archiveDir =
                Path.Combine(
                    logDir,
                    config.ArchiveDirectory);

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            if (!Directory.Exists(archiveDir))
            {
                Directory.CreateDirectory(archiveDir);
            }
        }

        #region Public

        public static void Debug(string message)
        {
            if (!config.EnableDebug)
                return;

            Write(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        public static void Error(Exception ex)
        {
            Write(LogLevel.Error, ex.ToLogString());
        }

        public static void Fatal(string message)
        {
            Write(LogLevel.Fatal, message);
        }

        #endregion Public

        #region Core

        private static void Write(
            LogLevel level,
            string message)
        {
            try
            {
                string log =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"[{config.Environment}] " +
                    $"[{level}] " +
                    $"{message}";

                logQueue.Add(log);

                if (config.EnableConsole)
                {
                    WriteConsole(level, log);
                }
            }
            catch
            {
            }
        }

        private static void ProcessQueue()
        {
            foreach (var log in logQueue.GetConsumingEnumerable())
            {
                try
                {
                    WriteFile(log);
                }
                catch
                {
                }
            }
        }

        private static void WriteConsole(
            LogLevel level,
            string log)
        {
            lock (locker)
            {
                var oldColor =
                    Console.ForegroundColor;

                switch (level)
                {
                    case LogLevel.Debug:
                        Console.ForegroundColor =
                            ConsoleColor.Gray;
                        break;

                    case LogLevel.Info:
                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        break;

                    case LogLevel.Warn:
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        break;

                    case LogLevel.Error:
                        Console.ForegroundColor =
                            ConsoleColor.Red;
                        break;

                    case LogLevel.Fatal:
                        Console.ForegroundColor =
                            ConsoleColor.DarkRed;
                        break;
                }

                Console.WriteLine(log);

                Console.ForegroundColor = oldColor;
            }
        }

        private static void WriteFile(string log)
        {
            if (!config.EnableFile)
                return;

            string logDir =
                Path.Combine(
                    BaseDirectory,
                    config.LogDirectory);

            string fileName =
                $"{DateTime.Now:yyyy-MM-dd}.log";

            string filePath =
                Path.Combine(logDir, fileName);

            ArchiveIfNeeded(filePath);

            lock (locker)
            {
                File.AppendAllText(
                    filePath,
                    log + Environment.NewLine,
                    Encoding.UTF8);
            }
        }

        #endregion Core

        #region Archive

        private static void ArchiveIfNeeded(string file)
        {
            try
            {
                if (!File.Exists(file))
                    return;

                FileInfo fi = new FileInfo(file);

                long max =
                    config.MaxFileSizeMB * 1024L * 1024L;

                if (fi.Length < max)
                    return;

                string archiveDir =
                    Path.Combine(
                        BaseDirectory,
                        config.LogDirectory,
                        config.ArchiveDirectory);

                string archiveName =
                    $"{Path.GetFileNameWithoutExtension(file)}_" +
                    $"{DateTime.Now:HHmmss}.log";

                string archivePath =
                    Path.Combine(
                        archiveDir,
                        archiveName);

                File.Move(file, archivePath);
            }
            catch
            {
            }
        }

        private static void ClearExpiredLogs()
        {
            try
            {
                string logDir =
                    Path.Combine(
                        BaseDirectory,
                        config.LogDirectory);

                if (!Directory.Exists(logDir))
                    return;

                var files =
                    Directory.GetFiles(logDir, "*.log");

                foreach (var file in files)
                {
                    FileInfo fi = new FileInfo(file);

                    if (fi.CreationTime <
                        DateTime.Now.AddDays(
                            -config.KeepDays))
                    {
                        fi.Delete();
                    }
                }
            }
            catch
            {
            }
        }

        #endregion Archive
    }
}