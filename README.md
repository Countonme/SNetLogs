# 📦 SNetLogs

### Enterprise High-Performance Logging Framework for .NET

<p align="center">

**Lightweight · Asynchronous · Thread-Safe · Industrial-Ready**

</p>

<p align="center">

![.NET](https://img.shields.io/badge/.NET-Compatible-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-7.3%2B-239120?style=flat-square&logo=csharp&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Docker-0078D4?style=flat-square)
![Dependencies](https://img.shields.io/badge/Dependencies-Zero-00A98F?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)

</p>

<p align="center">

A lightweight, high-performance asynchronous logging framework designed for  
<strong>MES · PLC · IoT · Industrial Automation · Equipment Communication · Backend Services</strong>

</p>

---

## 📖 Overview

**SNetLogs** is an asynchronous logging framework designed specifically for high-concurrency and industrial applications.

The framework separates **business execution** from **disk I/O** through a producer-consumer architecture.

```text
Business Threads
      │
      ▼
┌──────────────────────┐
│   Async Log Queue    │
│    Bounded Queue     │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Consumer Thread    │
│                      │
│   Batch Buffer       │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│      File I/O        │
│                      │
│  Module + Daily Log  │
└──────────────────────┘
```

The primary design goal is:

> **Logging should never become a bottleneck for business logic.**

---

# ✨ Features

### ⚡ High Performance

- Asynchronous logging
- Thread-safe log queue
- Dedicated background consumer
- Batch file writing
- Time-based automatic Flush
- Bounded queue for memory protection

### 🔍 Diagnostic Information

Automatically records:

- Process name
- Class name
- Method name
- Source line number
- Thread ID
- Module
- Log level
- Trace ID
- Timestamp
- Exception stack trace

### 📂 Log Management

- Module-based log directories
- Daily log files
- Configurable retention period
- Automatic expired-log cleanup
- Automatic directory creation

### 🛡️ Reliability

- Logging failures are isolated from business logic
- File I/O exceptions are internally handled
- Console output is independently synchronized
- Bounded queue prevents unlimited memory growth
- Dropped log statistics are available

### 🌍 Deployment

- Windows
- Linux
- Docker
- x64
- ARM — depending on target .NET runtime

### 📦 Dependencies

**Zero third-party logging dependencies.**

SNetLogs uses standard .NET APIs and can be directly integrated into existing applications.

---

# 🏭 Designed for Industrial Systems

SNetLogs is particularly suitable for systems involving:

```text
MES
PLC
Equipment
MQTT
Kafka
Redis
MySQL
MongoDB
HTTP / HTTPS
Scheduler
Background Worker
IoT Gateway
Industrial PC
```

Typical architecture:

```text
                ┌─────────────┐
                │     MES     │
                └──────┬──────┘
                       │
              ┌────────▼────────┐
              │   Application   │
              └────────┬────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
      PLC            MQTT           Kafka
        │              │              │
        └──────────────┼──────────────┘
                       │
                       ▼
                  SNetLogs
                       │
                       ▼
              ┌────────────────┐
              │ Async Log Queue│
              └───────┬────────┘
                      │
                      ▼
                Daily Log Files
```

This makes SNetLogs suitable for environments where **large numbers of devices, background tasks, network callbacks, and concurrent operations** generate logs simultaneously.

---

# 📁 Project Structure

```text
SNetLogs/
│
├── Common/
│   └── Logs/
│       │
│       ├── Log.cs
│       ├── LogConfig.cs
│       ├── LogLevel.cs
│       ├── logs.json
│       │
│       └── Archive/
│
├── README.md
└── LICENSE
```

| File / Directory | Description |
|---|---|
| `Log.cs` | Core asynchronous logger |
| `LogConfig.cs` | Logger configuration |
| `LogLevel.cs` | Log level definition |
| `logs.json` | Runtime configuration |
| `Archive/` | Reserved archive directory |
| `README.md` | Documentation |

---

# ⚙️ Configuration

Configuration file:

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

| Property | Description | Default |
|---|---|---:|
| `Environment` | Environment identifier | `DEV` |
| `EnableConsole` | Enable console output | `true` |
| `EnableFile` | Enable file output | `true` |
| `BatchSize` | Logs required to trigger immediate Flush | `200` |
| `FlushIntervalMs` | Maximum periodic Flush interval | `2000` |
| `KeepDays` | Log retention period | `7` |
| `QueueCapacity` | Maximum queue capacity | `50000` |
| `LogDirectory` | Log root directory | `Logs` |
| `ArchiveDirectory` | Archive directory | `Archive` |

---

# 🚀 Quick Start

## 1. Add SNetLogs

Copy the logging components into your project:

```text
Common/
└── Logs/
```

No additional logging package is required.

---

## 2. Start Logging

```csharp
Log.Info("System started");
```

The logger initializes automatically when first accessed.

---

# ✍️ Usage

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

Modules allow logs to be separated by business functionality.

```csharp
Log.Info("MES", "MES service started");

Log.Info("PLC", "PLC connected");

Log.Warn("MQTT", "MQTT connection unstable");

Log.Error("Kafka", "Kafka send failed");

Log.Fatal("System", "Critical system failure");
```

Recommended module names:

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

---

# 🔗 Trace ID

SNetLogs supports Trace ID tracking for distributed business operations.

Create a Trace ID:

```csharp
string traceId = Log.NewTraceId();
```

Use the same Trace ID across multiple operations:

```csharp
Log.Info(
    "MES",
    traceId,
    "Start welding request");

Log.Info(
    "PLC",
    traceId,
    "Send welding command");

Log.Info(
    "Equipment",
    traceId,
    "Waiting for welding result");

Log.Info(
    "MES",
    traceId,
    "Welding request completed");
```

Example:

```text
[Trace:8c9c5d1e7f1c4e3f9d8e6b2a1c4d5e6f]
```

This makes it possible to trace a complete transaction:

```text
MES Request
     │
     ▼
API
     │
     ▼
PLC
     │
     ▼
Equipment
     │
     ▼
Result
```

---

# ❌ Exception Logging

Log exceptions directly:

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

Or provide additional context:

```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    Log.Error(
        "MES",
        ex,
        "Failed to process welding result");
}
```

SNetLogs preserves:

```text
Exception Type
Exception Message
Stack Trace
Inner Exception
```

---

# 🔍 Automatic Source Information

SNetLogs automatically captures source information using:

```csharp
[CallerFilePath]
[CallerMemberName]
[CallerLineNumber]
```

For example:

```csharp
Log.Error(
    "PLC",
    "PLC communication timeout");
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

No manual source information is required.

---

# 🧵 Thread Tracking

Every log contains the managed thread ID.

Example:

```text
[T:23]
```

This is useful when troubleshooting:

- `Task`
- `Thread`
- Timers
- Background workers
- PLC callbacks
- MQTT callbacks
- Kafka consumers
- Parallel processing

---

# 📦 Asynchronous Queue

SNetLogs uses a bounded `BlockingCollection` backed by `ConcurrentQueue`.

Default capacity:

```text
50,000
```

The producer uses:

```csharp
queue.TryAdd(item);
```

instead of waiting indefinitely for disk I/O.

When the queue is full:

```text
Log Event
   │
   ▼
TryAdd()
   │
   ├── Success ──► Queue
   │
   └── Failed ───► DroppedLogs++
```

This provides a controlled failure mechanism during extreme logging bursts.

---

# ⚡ Batch + Interval Flush

SNetLogs uses two Flush mechanisms.

### Batch Flush

When the buffer reaches:

```text
BatchSize = 200
```

the logger immediately writes the batch to disk.

```text
200 logs
   │
   ▼
Flush
   │
   ▼
File
```

### Interval Flush

Even if the number of logs is below the batch threshold, the logger periodically Flushes.

Default:

```text
FlushIntervalMs = 2000
```

For example:

```text
10 logs
   │
   ▼
Wait up to 2 seconds
   │
   ▼
Flush
```

Therefore, logs do **not** need to reach `BatchSize` before being written.

---

# 📂 Output Structure

Logs are separated by module and date.

```text
Logs/
│
├── MES/
│   ├── 2026-08-29.log
│   ├── 2026-08-30.log
│   └── 2026-08-31.log
│
├── PLC/
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

---

# 📝 Log Format

SNetLogs records detailed diagnostic information.

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
[2026-08-31 17:20:15.123] [PROD] [EquipmentService] [DeviceService] [ReadStatus] [Line:128] [T:23] [PLC] [ERROR] [Trace:8c9c5d1e7f1c4e3f9d8e6b2a1c4d5e6f] PLC communication timeout
```

This provides enough information to quickly identify:

```text
When did it happen?
Which environment?
Which application?
Which class?
Which method?
Which source line?
Which thread?
Which module?
What severity?
Which transaction?
What happened?
```

---

# 🧹 Log Retention

Expired logs are automatically removed according to:

```json
"KeepDays": 7
```

For example:

```text
KeepDays = 7

Today
 │
 ├── 1 day ago
 ├── 2 days ago
 ├── 3 days ago
 ├── ...
 ├── 7 days ago
 │
 └── Older logs → Delete
```

Cleanup runs:

- Once during startup
- Automatically every hour

---

# 📊 Runtime Statistics

SNetLogs provides runtime monitoring information.

### Queue Count

```csharp
int queueCount = Log.QueueCount;
```

Returns the number of events currently waiting in the queue.

### Queue Capacity

```csharp
int capacity = Log.QueueCapacity;
```

### Dropped Logs

```csharp
long dropped = Log.DroppedLogs;
```

This allows production systems to detect whether the logger is under excessive pressure.

Example:

```csharp
Log.Info(
    "System",
    $"Queue={Log.QueueCount}, Dropped={Log.DroppedLogs}");
```

---

# 🛡️ Failure Isolation

The logging subsystem is designed to avoid affecting the main business process.

```text
                 Business Logic
                       │
                       ▼
                    SNetLogs
                       │
          ┌────────────┼────────────┐
          │            │            │
          ▼            ▼            ▼
        Queue        Console       File
          │
          ▼
       Consumer
          │
          ▼
       Disk I/O
```

Failures inside the logging subsystem are handled internally where possible.

Potential failures include:

```text
File I/O
Directory creation
Configuration
Console output
Log cleanup
Queue pressure
```

The goal is:

> **A logging failure should not become an application failure.**

---

# 📈 Performance Design

SNetLogs is optimized for high-concurrency environments through:

| Component | Strategy |
|---|---|
| Producer | Non-blocking `TryAdd()` |
| Queue | Bounded `BlockingCollection` |
| Backend | `ConcurrentQueue` |
| Consumer | Dedicated background thread |
| Buffer | Batch accumulation |
| File I/O | Batch writing |
| Flush | Size + time based |
| File synchronization | Dedicated lock |
| Console synchronization | Independent lock |
| Memory protection | Bounded queue |
| Failure handling | Exception isolation |

### Performance Note

Actual throughput depends on the deployment environment and workload.

Important factors include:

- CPU
- Storage performance
- File system
- Log message size
- Number of concurrent producers
- Logging frequency
- Console output
- Number of modules

For production capacity planning, benchmark SNetLogs under the target hardware and workload rather than relying on theoretical throughput numbers.

---

# 🖥️ Production Recommendation

For production industrial systems:

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

# 🧩 API Reference

### Basic

```csharp
Log.Debug("message");

Log.Info("message");

Log.Warn("message");

Log.Error("message");

Log.Fatal("message");
```

### Module

```csharp
Log.Debug("MODULE", "message");

Log.Info("MODULE", "message");

Log.Warn("MODULE", "message");

Log.Error("MODULE", "message");

Log.Fatal("MODULE", "message");
```

### Trace

```csharp
string traceId = Log.NewTraceId();

Log.Info(
    "MODULE",
    traceId,
    "message");
```

### Exception

```csharp
Log.Error(
    "MODULE",
    ex,
    "Operation failed");
```

### Statistics

```csharp
Log.QueueCount;

Log.QueueCapacity;

Log.DroppedLogs;
```

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

        Log.Info(
            "PLC",
            traceId,
            $"Send command to {equipmentId}");

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

---

# 🗺️ Roadmap

### Logging

- [ ] Structured JSON logging
- [ ] Configurable log levels
- [ ] Large-file rolling
- [ ] Log compression
- [ ] Log archive management

### Remote Logging

- [ ] Elasticsearch integration
- [ ] Kafka integration
- [ ] MQTT remote logging
- [ ] TCP remote logging
- [ ] Centralized log server

### Observability

- [ ] Web-based log viewer
- [ ] Real-time log monitoring
- [ ] Search and filtering
- [ ] Distributed tracing
- [ ] Performance metrics
- [ ] Health monitoring

### Security

- [ ] Log encryption
- [ ] Sensitive-data masking
- [ ] Access control
- [ ] Audit logging

---

# 🤝 Contributing

Contributions are welcome.

Before submitting a performance-related change, please provide:

```text
.NET Version
Operating System
CPU
Memory
Storage Type
Concurrent Producers
Logs / Second
Average Message Size
```

This helps evaluate changes under realistic workloads.

---

# 📜 License

SNetLogs is released under the **MIT License**.

See [LICENSE](LICENSE) for details.

---

# ⭐ Support

If SNetLogs is useful in your project, consider giving the repository a ⭐ **Star**.

<p align="center">

**SNetLogs**

High Performance Logging for Industrial Systems

</p>
