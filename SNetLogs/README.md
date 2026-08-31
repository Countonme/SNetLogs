# 📦 SNetLogs V6

## Enterprise High-Performance Logging Framework for .NET

**SNetLogs** is a lightweight, asynchronous, high-performance logging framework designed for **industrial applications, MES, PLC, IoT, equipment communication, desktop applications, and backend services**.

It is designed around a simple principle:

> **Business threads should not be blocked by disk I/O.**

SNetLogs uses an asynchronous producer-consumer architecture to move file I/O away from business threads while providing batching, bounded queues, automatic log rotation, retention cleanup, trace tracking, source-code location tracking, and runtime statistics.

---

## ✨ Core Features

* 🚀 Asynchronous, thread-safe logging
* ⚡ High-throughput producer-consumer architecture
* 📦 Bounded log queue to prevent unlimited memory growth
* 🧵 Dedicated background consumer thread
* 📝 Batch file writing
* ⏱️ Time-based automatic Flush
* 📁 Module-based log directories
* 📅 Daily log file rotation
* 🧹 Automatic expired-log cleanup
* 🔍 Automatic process / class / method / line tracking
* 🧵 Thread ID tracking
* 🔗 Trace ID support
* ❌ Exception logging with full stack trace
* 🖥️ Console + File dual output
* 📊 Runtime queue and dropped-log statistics
* 🛡️ Logging failures do not affect business logic
* 🌍 Windows / Linux / Docker compatible
* 📦 Zero third-party dependencies
* 🔧 C# 7.3 compatible
* 🏭 Suitable for high-concurrency industrial systems

---

# 🏗️ Architecture

SNetLogs uses an asynchronous producer-consumer architecture.

```text
┌──────────────────────────────────────────────┐
│              Business Threads               │
│                                              │
│  MES   PLC   MQTT   Kafka   HTTP   UI       │
│    │     │      │      │      │     │       │
└────┼─────┼──────┼──────┼──────┼─────┼───────┘
     │     │      │      │      │     │
     └─────┴──────┴──────┴──────┴─────┘
                       │
                       ▼
              ┌─────────────────┐
              │  BlockingQueue  │
              │   Capacity      │
              │     50,000      │
              └────────┬────────┘
                       │
                       ▼
              ┌─────────────────┐
              │ Consumer Thread │
              │                 │
              │ Batch Buffer    │
              └────────┬────────┘
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
       Batch File I/O        Console Output
             │
             ▼
       Daily Log Files
```

### Design Goals

SNetLogs separates **log production** from **log persistence**.

Business threads perform:

```text
Create LogEvent
      ↓
TryAdd()
      ↓
Return
```

File writing is performed asynchronously:

```text
Queue
 ↓
Consumer
 ↓
Batch Buffer
 ↓
File I/O
```

This significantly reduces the impact of disk I/O on business execution threads.

---

# ⚡ High-Concurrency Design

SNetLogs is specifically designed for applications where many threads may generate logs simultaneously.

### Bounded Queue

The default queue capacity is:

```text
50,000 events
```

This prevents an abnormal logging burst from continuously consuming process memory.

```csharp
private static readonly BlockingCollection<LogEvent> queue =
    new BlockingCollection<LogEvent>(
        new ConcurrentQueue<LogEvent>(),
        50000);
```

When the queue is full, the logger does **not block the business thread indefinitely**.

Instead:

```text
TryAdd()
   │
   ├── Success → Queue
   │
   └── Failed → DroppedLogs++
```

This provides a controlled degradation mechanism under extreme logging pressure.

---

# 📦 Batch Writing

Logs are accumulated in an internal buffer before being written to disk.

Default configuration:

```json
"BatchSize": 200
```

When the buffer reaches 200 entries:

```text
200 logs
   ↓
Flush
   ↓
File
```

However, **logs do not need to reach 200 entries to be written**.

SNetLogs also performs periodic Flush:

```json
"FlushIntervalMs": 2000
```

Therefore:

```text
Example:

10 logs
 ↓
Wait up to 2 seconds
 ↓
Flush
```

and:

```text
200 logs
 ↓
Immediately Flush
```

This provides a balance between:

* Disk I/O efficiency
* Log persistence latency
* Memory usage

---

# ⚙️ Configuration

The logger configuration is stored in:

```text
Common/Logs/logs.json
```

Example:

```json
{
  "Environment": "DEV",
  "EnableConsole": true,
  "EnableFile": true,
  "BatchSize": 200,
  "FlushIntervalMs": 2000,
  "KeepDays": 7,
  "QueueCapacity": 50000,
  "LogDirectory": "Logs",
  "ArchiveDirectory": "Archive"
}
```

## Configuration Reference

| Property           | Description                               |   Default |
| ------------------ | ----------------------------------------- | --------: |
| `Environment`      | Runtime environment name                  |     `DEV` |
| `EnableConsole`    | Enable console logging                    |    `true` |
| `EnableFile`       | Enable file logging                       |    `true` |
| `BatchSize`        | Number of logs triggering immediate Flush |     `200` |
| `FlushIntervalMs`  | Maximum periodic Flush interval           |    `2000` |
| `KeepDays`         | Log retention period                      |       `7` |
| `QueueCapacity`    | Maximum asynchronous queue size           |   `50000` |
| `LogDirectory`     | Log root directory                        |    `Logs` |
| `ArchiveDirectory` | Reserved archive directory                | `Archive` |

---

# 🚀 Quick Start

## 1. Add SNetLogs

Copy the logging source into your project:

```text
Common/
└── Logs/
    └── Log.cs
```

SNetLogs has no external NuGet dependency.

---

## 2. Start Logging

```csharp
Log.Info("System started");
```

No additional logger initialization is required.

The static logger automatically initializes itself when first accessed.

---

# ✍️ Logging API

## Basic Logging

```csharp
Log.Debug("Debug message");

Log.Info("System started");

Log.Warn("Connection is unstable");

Log.Error("Database connection failed");

Log.Fatal("Critical system failure");
```

Supported levels:

```text
DEBUG
INFO
WARN
ERROR
FATAL
```

---

# 🧩 Module Logging

For industrial applications, it is recommended to classify logs by module.

```csharp
Log.Info("MES", "MES service started");

Log.Info("PLC", "PLC connected");

Log.Warn("MQTT", "MQTT connection unstable");

Log.Error("Kafka", "Kafka send failed");

Log.Fatal("System", "Critical service failure");
```

The module is used as the log directory.

Example:

```text
Logs/
├── MES/
├── PLC/
├── MQTT/
├── Kafka/
└── System/
```

This makes troubleshooting large industrial systems significantly easier.

---

# 🔗 Trace ID

SNetLogs supports Trace ID tracking.

Create a Trace ID:

```csharp
string traceId = Log.NewTraceId();
```

Use the same Trace ID across multiple operations:

```csharp
Log.Info("MES", traceId, "Start welding request");

Log.Info("PLC", traceId, "Send welding command");

Log.Info("HTTP", traceId, "Request completed");
```

Example:

```text
[Trace:8c9c5d1e7f1c4e3f9d8e6b2a1c4d5e6f]
```

This is particularly useful for tracking a complete business transaction across:

```text
MES
 ↓
API
 ↓
PLC
 ↓
Equipment
 ↓
Result
```

---

# ❌ Exception Logging

SNetLogs supports direct exception logging.

```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    Log.Error("MES", ex);
}
```

You can also provide a custom message:

```csharp
catch (Exception ex)
{
    Log.Error(
        "MES",
        ex,
        "Failed to process welding result");
}
```

The complete exception information is preserved using:

```csharp
ex.ToString()
```

including:

* Exception type
* Exception message
* Stack trace
* Inner exception information

---

# 🔍 Automatic Source Information

SNetLogs uses C# Caller Information attributes:

```csharp
[CallerFilePath]
[CallerMemberName]
[CallerLineNumber]
```

Therefore, callers do not need to manually provide source information.

Example:

```csharp
Log.Error("PLC", "PLC communication timeout");
```

The logger automatically records:

```text
ClassName
MethodName
LineNumber
```

Example:

```text
[DeviceService]
[ReadStatus]
[Line:128]
```

This makes production troubleshooting much faster.

---

# 🧵 Thread Tracking

Every log records the managed thread ID:

```text
[T:23]
```

This is especially useful for:

* Task
* Thread
* BackgroundWorker
* Timer
* PLC communication
* MQTT callbacks
* Kafka consumers
* Parallel processing

Example:

```text
[2026-08-31 17:20:15.123]
[PROD]
[MyService]
[DeviceService]
[ReadStatus]
[Line:128]
[T:23]
[PLC]
[ERROR]
PLC communication timeout
```

---

# 📝 Log Format

Current log format:

```text
[Time]
[Environment]
[Process]
[Class]
[Method]
[Line]
[Thread]
[Module]
[Level]
[TraceId]
Message
```

Example:

```text
[2026-08-31 17:20:15.123]
[PROD]
[EquipmentService]
[DeviceService]
[ReadStatus]
[Line:128]
[T:23]
[PLC]
[ERROR]
[Trace:8c9c5d1e7f1c4e3f9d8e6b2a1c4d5e6f]
PLC communication timeout
```

Single-line output:

```text
[2026-08-31 17:20:15.123] [PROD] [EquipmentService] [DeviceService] [ReadStatus] [Line:128] [T:23] [PLC] [ERROR] [Trace:8c9c5d1e7f1c4e3f9d8e6b2a1c4d5e6f] PLC communication timeout
```

---

# 📂 Log Directory Structure

Logs are separated by module and date.

```text
Logs/
├── MES/
│   ├── 2026-08-29.log
│   ├── 2026-08-30.log
│   └── 2026-08-31.log
│
├── PLC/
│   ├── 2026-08-29.log
│   ├── 2026-08-30.log
│   └── 2026-08-31.log
│
├── MQTT/
│   └── 2026-08-31.log
│
├── Kafka/
│   └── 2026-08-31.log
│
└── System/
    └── 2026-08-31.log
```

Daily files are automatically created according to:

```text
yyyy-MM-dd.log
```

---

# 🧹 Log Retention

SNetLogs automatically removes expired log files.

Example:

```json
"KeepDays": 7
```

Means:

```text
Today
 │
 ├── -1 day
 ├── -2 days
 ├── ...
 ├── -7 days
 │
 └── Older logs → Automatically deleted
```

Cleanup is performed:

* Once during startup
* Every hour while running

The cleanup process does not block normal log production.

---

# 📊 Runtime Statistics

SNetLogs exposes runtime statistics.

## Queue Count

```csharp
int count = Log.QueueCount;
```

Returns the number of logs currently waiting in the asynchronous queue.

---

## Queue Capacity

```csharp
int capacity = Log.QueueCapacity;
```

Default:

```text
50000
```

---

## Dropped Logs

```csharp
long dropped = Log.DroppedLogs;
```

This value records the number of logs that could not be queued because the queue reached its capacity.

For production monitoring, it is recommended to periodically monitor:

```csharp
Log.QueueCount
Log.DroppedLogs
```

Example:

```csharp
Log.Info(
    "System",
    $"Queue={Log.QueueCount}, Dropped={Log.DroppedLogs}");
```

---

# 🛡️ Failure Isolation

Logging must never become the reason that an industrial application crashes.

Therefore SNetLogs follows:

```text
Business System
      │
      ▼
    Logger
      │
      ├── Queue Failure
      ├── File Failure
      ├── Directory Failure
      ├── Console Failure
      └── Configuration Failure
             │
             ▼
       Do NOT propagate
       to business code
```

Most internal logging operations are protected by exception handling.

A failure in:

```text
Log file
Disk
Directory
Console
Configuration
```

should not directly terminate the business application.

---

# ⚡ Performance Characteristics

SNetLogs is optimized for high-frequency logging scenarios.

| Area              | Design                       |
| ----------------- | ---------------------------- |
| Producer          | Non-blocking `TryAdd()`      |
| Queue             | Bounded `BlockingCollection` |
| Queue backend     | `ConcurrentQueue`            |
| Consumer          | Dedicated background thread  |
| File writing      | Batch I/O                    |
| Flush             | Size + time based            |
| File lock         | Dedicated file lock          |
| Console lock      | Independent console lock     |
| Memory protection | Bounded queue                |
| Failure isolation | Internal exception handling  |

### Important

Actual throughput depends on:

* CPU
* Disk type
* Disk throughput
* Number of modules
* Log message size
* File system
* Number of concurrent producers
* Console output configuration

Therefore, benchmark results should be measured under the target production environment rather than assuming a fixed logs/sec value.

---

# 🏭 Recommended Industrial Usage

For MES / PLC / IoT applications, modules can be organized as:

```text
MES
PLC
Equipment
MQTT
Kafka
Redis
MySQL
MongoDB
HTTP
API
Scheduler
Worker
System
UI
```

Example:

```csharp
Log.Info("PLC", "Connecting to PLC");

Log.Info("MQTT", "MQTT message received");

Log.Info("Kafka", "Kafka message published");

Log.Info("MES", "MES production order created");

Log.Error("MySQL", ex, "Database operation failed");
```

This results in an easily searchable structure:

```text
Logs/
├── PLC/
├── MQTT/
├── Kafka/
├── MES/
└── MySQL/
```

---

# 🔧 Recommended Production Configuration

For industrial production environments:

```json
{
  "Environment": "PROD",
  "EnableConsole": false,
  "EnableFile": true,
  "BatchSize": 200,
  "FlushIntervalMs": 2000,
  "KeepDays": 30,
  "QueueCapacity": 50000,
  "LogDirectory": "Logs",
  "ArchiveDirectory": "Archive"
}
```

For development:

```json
{
  "Environment": "DEV",
  "EnableConsole": true,
  "EnableFile": true,
  "BatchSize": 50,
  "FlushIntervalMs": 1000,
  "KeepDays": 7,
  "QueueCapacity": 10000,
  "LogDirectory": "Logs",
  "ArchiveDirectory": "Archive"
}
```

---

# 📦 Project Structure

Recommended project structure:

```text
SNetLogs/
│
├── Common/
│   └── Logs/
│       ├── Log.cs
│       ├── logs.json
│       └── Archive/
│
├── README.md
└── LICENSE
```

If the project is later split into multiple files:

```text
SNetLogs/
├── Log.cs
├── LogConfig.cs
├── LogEvent.cs
├── LogLevel.cs
├── LogFormatter.cs
└── README.md
```

---

# 🔌 Dependency

SNetLogs intentionally avoids external logging dependencies.

```text
External NuGet Packages
        ↓
       None
```

It relies primarily on the .NET runtime:

```text
System
System.Collections.Concurrent
System.Diagnostics
System.IO
System.Text
System.Text.Json
System.Threading
```

This makes deployment particularly suitable for:

* Industrial PCs
* Offline environments
* OT networks
* Edge devices
* Docker containers
* Restricted production environments

---

# 🌍 Platform Compatibility

| Platform       | Support                   |
| -------------- | ------------------------- |
| Windows        | ✅                         |
| Linux          | ✅                         |
| Docker         | ✅                         |
| .NET Framework | ✅                         |
| .NET           | ✅                         |
| C# 7.3         | ✅                         |
| x64            | ✅                         |
| ARM            | Depends on target runtime |

---

# 🧪 Example

```csharp
public void ProcessEquipment(string equipmentId)
{
    string traceId = Log.NewTraceId();

    try
    {
        Log.Info(
            "Equipment",
            traceId,
            $"Start processing equipment: {equipmentId}");

        // PLC communication
        Log.Info(
            "PLC",
            traceId,
            $"Send command to {equipmentId}");

        // MES communication
        Log.Info(
            "MES",
            traceId,
            $"MES request completed: {equipmentId}");

        Log.Info(
            "Equipment",
            traceId,
            $"Processing completed: {equipmentId}");
    }
    catch (Exception ex)
    {
        Log.Error(
            "Equipment",
            ex,
            $"Equipment processing failed: {equipmentId}");
    }
}
```

Example output:

```text
[2026-08-31 17:20:15.123] [PROD] [EquipmentService] [Equipment] [ProcessEquipment] [Line:42] [T:18] [Equipment] [INFO] [Trace:abc123...] Start processing equipment: EQ001

[2026-08-31 17:20:15.128] [PROD] [EquipmentService] [PLC] [ProcessEquipment] [Line:47] [T:18] [PLC] [INFO] [Trace:abc123...] Send command to EQ001

[2026-08-31 17:20:15.245] [PROD] [EquipmentService] [MES] [ProcessEquipment] [Line:52] [T:18] [MES] [INFO] [Trace:abc123...] MES request completed: EQ001

[2026-08-31 17:20:15.250] [PROD] [EquipmentService] [Equipment] [ProcessEquipment] [Line:57] [T:18] [Equipment] [INFO] [Trace:abc123...] Processing completed: EQ001
```

---

# 📈 Monitoring Recommendations

For high-concurrency production systems, it is recommended to monitor:

```csharp
Log.QueueCount
Log.QueueCapacity
Log.DroppedLogs
```

A typical monitoring strategy:

```text
Queue utilization
       │
       ├── 0 ~ 50%
       │      Normal
       │
       ├── 50 ~ 80%
       │      Increased logging load
       │
       ├── 80 ~ 95%
       │      Investigate disk I/O
       │
       └── > 95%
              Risk of dropped logs
```

If:

```csharp
Log.DroppedLogs > 0
```

the application should investigate:

* Disk performance
* Excessive logging
* Large log messages
* Logging bursts
* Slow file system
* Insufficient batch size
* Excessive console output

---

# 🛣️ Roadmap

Planned enhancements may include:

* [ ] Structured JSON logging
* [ ] Elasticsearch integration
* [ ] Kafka remote logging
* [ ] MQTT remote logging
* [ ] TCP remote logging
* [ ] Centralized log server
* [ ] Web-based log viewer
* [ ] Log search and filtering
* [ ] Log compression
* [ ] Large-file rolling
* [ ] Log encryption
* [ ] Distributed Trace ID
* [ ] Async multi-file writer
* [ ] Performance metrics
* [ ] Log level runtime configuration

---

# 🤝 Contributing

Contributions, bug reports, performance improvements, and feature requests are welcome.

When submitting performance-related changes, please provide:

```text
Environment
.NET version
OS
CPU
Memory
Disk type
Concurrent producers
Logs/sec
Average message size
```

This helps ensure that performance changes are evaluated under realistic workloads.

---

# 📜 License

MIT License

Copyright © SNetLogs Contributors

---

# ⭐ Support

If SNetLogs is useful in your MES, PLC, IoT, industrial automation, or backend projects, consider giving the project a ⭐ Star.

**SNetLogs — Fast, Reliable, and Built for Industrial Systems.**
