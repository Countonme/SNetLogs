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