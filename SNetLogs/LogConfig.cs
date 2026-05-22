using System;
using System.Collections.Generic;
using System.Text;

namespace SNetLogs
{
    public class LogConfig
    {
        /// <summary>
        /// 日志目录
        /// </summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// 归档目录
        /// </summary>
        public string ArchiveDirectory { get; set; } = "Archive";

        /// <summary>
        /// 保存天数
        /// </summary>
        public int KeepDays { get; set; } = 30;

        /// <summary>
        /// 最大文件MB
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 50;

        /// <summary>
        /// 是否控制台输出
        /// </summary>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// 是否写文件
        /// </summary>
        public bool EnableFile { get; set; } = true;

        /// <summary>
        /// 是否Debug模式
        /// </summary>
        public bool EnableDebug { get; set; } = true;

        /// <summary>
        /// 当前环境
        /// DEBUG DEV RUN PROD
        /// </summary>
        public string Environment { get; set; } = "RUN";

        /// <summary>
        /// 批量写关键参数
        /// </summary>
        public int BatchSize { get; set; } = 50;
    }
}