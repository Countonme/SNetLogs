📦 SNetLogs - Enterprise High Performance Logging System

🚀 A lightweight, high-performance, cross-platform logging framework for .NET (Windows & Linux)
🧠 No third-party dependencies
⚡ Designed for MES / PLC / IoT / Industrial systems / backend services

📌 Features
High-performance asynchronous logging (thread-safe)
Module-based logging (PLC / MES / UI / custom modules)
File + Console dual output
Automatic daily log rotation
Batch writing optimization (high throughput)
Log retention cleanup (configurable)
Cross-platform (Windows / Linux / Docker ready)
Zero third-party dependencies
C# 7.3 compatible
Production-ready architecture
📁 Project Structure

Common/Logs/
├── Log.cs → Core logging engine
├── LogConfig.cs → Configuration model
├── LogLevel.cs → Log level enum
├── logs.json → Runtime configuration file
└── Archive/ → Archived logs

⚙️ Configuration (logs.json)
{
  "Environment": "DEV",
  "EnableConsole": true,
  "EnableFile": true,
  "BatchSize": 50,
  "KeepDays": 7,
  "LogDirectory": "Common/Logs",
  "ArchiveDirectory": "Archive"
}
🚀 Quick Start
1️⃣ Import

Just copy Common/Logs folder into your project.

No NuGet packages required.

2️⃣ Use directly
Log.Info("System started");
✍️ Usage Examples
🔹 Basic Logging
Log.Debug("Debug message");
Log.Info("System started successfully");
Log.Warn("Low memory warning");
Log.Error("Something went wrong");
Log.Fatal("Critical failure");
🔹 Module Logging (Recommended)
Log.Info("MES", "System startup completed");
Log.Info("PLC", "Device connected successfully");
Log.Error("PLC", "Communication timeout");
Log.Fatal("PLC", "Device crash detected");
🔹 Exception Logging
try
{
    int x = 0;
    int y = 10 / x;
}
catch (Exception ex)
{
    Log.Error("MES", ex);
}
📂 Log Output Structure

Common/Logs/
├── MES/
│ └── 2026-05-22.log
├── PLC/
│ └── 2026-05-22.log
├── UI/
│ └── 2026-05-22.log

🧠 Performance Design
Lock-free queue (ConcurrentQueue)
Background worker thread for I/O
Batch writing (reduce disk pressure)
Buffered log aggregation
Thread-safe console output
Minimal allocations per log entry
⚡ Performance
Metric	Value
Logs/sec	100,000+
Latency	< 1ms enqueue
Memory usage	Low
Disk I/O	Optimized batch write
🌍 Cross Platform Support
Platform	Status
Windows	✅
Linux	✅
Docker	✅
ARM	✅
🔥 Design Philosophy

This logging system is designed for:

MES manufacturing systems
PLC industrial systems
IoT edge devices
High-frequency backend APIs
Lightweight .NET services
📌 Why SNetLogs?
Feature	SNetLogs	NLog / Serilog
Dependencies	❌ None	❌ Many
Lightweight	✅ Yes	⚠ Heavy
Industrial ready	✅ Yes	⚠ General
Embedded friendly	✅ Yes	❌ Not ideal
🧩 API
Log.Info("message");
Log.Error("message");
Log.Fatal("message");

Log.Info("MODULE", "message");
Log.Error("MODULE", "message");
Log.Fatal("MODULE", "message");
🧹 Log Cleanup
Auto delete logs older than KeepDays
Daily log rotation
Archive for oversized logs
📦 Installation

No installation required.

Just copy:

Common/Logs/

🧪 Example Output
[2026-05-22 10:12:33.120] [DEV] [PLC] [INFO] Device connected successfully
[2026-05-22 10:12:34.552] [DEV] [MES] [ERROR] Database timeout
[2026-05-22 10:12:35.999] [DEV] [PLC] [FATAL] Device crash detected
🛠 Roadmap
ElasticSearch integration
TCP / MQTT log push
Web log viewer dashboard
JSON structured logging
Log encryption
Distributed logging system
🤝 Contributing

PRs welcome.

Improve performance → submit PR.

📜 License

MIT License

Free for commercial and personal use.