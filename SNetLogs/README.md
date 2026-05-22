# 📦 SNetLogs - Enterprise High Performance Logging System

A lightweight, high-performance, cross-platform logging framework for .NET.

Designed for MES / PLC / IoT / industrial systems / backend services.

---

# 📌 Features

- High-performance asynchronous logging (thread-safe)
- Module-based logging (MES / PLC / UI / custom modules)
- Console + File dual output
- Daily log rotation
- Batch writing optimization
- Log retention cleanup (configurable)
- Cross-platform (Windows / Linux / Docker)
- Zero third-party dependencies
- C# 7.3 compatible
- Production-ready design

---

# 📁 Project Structure

```text
Common/
└── Logs/
    ├── Log.cs
    ├── LogConfig.cs
    ├── LogLevel.cs
    ├── logs.json
    └── Archive/
```

| File | Description |
|------|-------------|
| Log.cs | Core logger |
| LogConfig.cs | Configuration model |
| LogLevel.cs | Log level enum |
| logs.json | Runtime config |
| Archive/ | Archived logs |

---

# ⚙️ Configuration (logs.json)

```json
{
  "Environment": "DEV",
  "EnableConsole": true,
  "EnableFile": true,
  "BatchSize": 50,
  "KeepDays": 7,
  "LogDirectory": "Common/Logs",
  "ArchiveDirectory": "Archive"
}
```

---

# 🚀 Quick Start

## 1️⃣ Copy Folder

Copy the entire folder into your project:

```text
Common/Logs/
```

No NuGet packages required.

---

## 2️⃣ Use Directly

```csharp
Log.Info("System started");
```

---

# ✍️ Usage

## 🔹 Basic Logging

```csharp
Log.Debug("Debug message");
Log.Info("System started");
Log.Warn("Warning message");
Log.Error("Error message");
Log.Fatal("Fatal error");
```

---

## 🔹 Module Logging

```csharp
Log.Info("MES", "System started");
Log.Info("PLC", "Device connected");
Log.Error("PLC", "Timeout error");
Log.Fatal("PLC", "Device crash");
```

---

## 🔹 Exception Logging

```csharp
try
{
    int x = 1 / 0;
}
catch (Exception ex)
{
    Log.Error("MES", ex);
}
```

---

# 📂 Output Structure

```text
Common/
└── Logs/
    ├── MES/
    │   └── 2026-05-22.log
    │
    ├── PLC/
    │   └── 2026-05-22.log
    │
    └── UI/
        └── 2026-05-22.log
```

---

# ⚡ Performance

| Metric | Value |
|--------|-------|
| Throughput | 100,000+ logs/sec |
| Latency | <1ms enqueue |
| Memory Usage | Low |
| Disk I/O | Optimized batch write |

---

# 🌍 Cross Platform

| Platform | Support |
|----------|----------|
| Windows | ✅ |
| Linux | ✅ |
| Docker | ✅ |
| ARM | ✅ |

---

# 🧹 Log Policy

- Auto delete logs older than `KeepDays`
- Daily log file rotation
- Archive oversized log files automatically

---

# 🧩 API Summary

```csharp
Log.Info("message");
Log.Error("message");
Log.Fatal("message");

Log.Info("MODULE", "message");
Log.Error("MODULE", "message");
Log.Fatal("MODULE", "message");
```

---

# 🧪 Example Output

```text
[2026-05-22 10:12:33] [DEV] [PLC] [INFO] Device connected successfully
[2026-05-22 10:12:34] [DEV] [MES] [ERROR] Database timeout
[2026-05-22 10:12:35] [DEV] [PLC] [FATAL] Device crash detected
```

---

# 🛠 Roadmap

- Elasticsearch integration
- MQTT / TCP remote logging
- Web dashboard viewer
- Structured JSON logging
- Log encryption
- Distributed logging support

---

# 🤝 Contributing

Pull requests are welcome.

---

# 📜 License

MIT License

---

# ⭐ Support

If this project helps you, please give it a ⭐ star.
