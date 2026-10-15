# sooncode.domain.net

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![MongoDB](https://img.shields.io/badge/MongoDB-4.4+-green.svg)](https://mongodb.com)

> A .NET DDD + Event Sourcing framework backed by MongoDB.
>
> The .NET sibling of [sooncode.domain](https://github.com/soonboot/sooncode.domain).

---

## Features

- Domain-Driven Design — Entity, ValueObject, DomainModel base classes
- Event Sourcing — Append-only event streams, replay, snapshot support
- Optimistic Concurrency — Version-based concurrency control
- Fluent Query API — Finder with filtering, pagination, sorting, aggregation
- Event & Entity Monitoring — Pub/sub for domain events and mutations
- Auto-Denormalization — Cross-entity field propagation
- Session / Unit of Work — Batch persistence with commit/rollback
- Validation Framework — Declarative validation
- MongoDB Native — Direct MongoDB driver, no ORM overhead

---

## Install

```xml
<PackageReference Include="Sooncode.Domain.Infrastructure" Version="1.1.0" />
```

---

## Quick Start

### 1. Configure MongoDB

```csharp
var mongoClient = new MongoClient("mongodb://localhost:27017");
var database = mongoClient.GetDatabase("myDatabase");
```

### 2. Define Domain Model

```csharp
public class User : DomainModel<User>
{
    public string Name { get; private set; }
    public int Age { get; private set; }

    public void Create(string name, int age)
    {
        Name = name;
        Age = age;
        Add();
    }
}
```

### 3. Use Repository

```csharp
var repo = new DomainRepository(database);
var user = new User();
user.Create("Alice", 28);
repo.Add(user);

var loaded = repo.FindByID<User>(user.Id);
```

---

## Core Concepts

### DomainModel

All aggregate roots extend `DomainModel<T>`. It manages event registration, version tracking, and snapshot support.

### DomainEvent

Immutable facts annotated with attributes. Built-in events: `BasicAddEvent`, `BasicModifyEvent`, `BasicDeleteEvent`.

### Finder

Fluent query API:

```csharp
var result = new Finder<User>(database)
    .ByField("age", 18, OType.Gte)
    .And("status", "active")
    .List(Sort.Desc("createdAt"));
```

---

## Java ↔ .NET 对齐映射（domain.infrastructrue → Domain.Infrastructure.net）

| Java（`com.sooncode.project.core.*`） | .NET（`Domain.Infrastructure.*`） | 说明 |
|---|---|---|
| `utils.ReflectUtils` | `Utils.ReflectionHelper`（`Utils/ReflectionHelper.cs`；历史名 `ReflectUtils` 保留为 `[Obsolete]` 转发壳） | .NET 规范命名；`PropertyDescriptor` + 构造缓存；`EntityConvert` 已切到新名 |
| `utils.*`（散落工具） | `Utils.EntityConvert` / `Utils.BaseTypeConvert` / `Utils.JsonUtil` / `Utils.MapBuilder` | 按职责拆分为独立静态类，不再是一个 `Utils` 大类 |
| `lookup.LookupHandler` | `Lookup.LookupHandler`（命名空间 `Domain.Infrastructure.Lookup`）；`Model/LookupHandler.cs` 仅为 `[Obsolete]` 兼容壳 | 与 Java `lookup` 包对齐；正本支持 `bulkBatchSize`（`InfraConfig`）分批 + `bulkWriter` 失败逐条回退 + `Shutdown()` 取消后台任务 |
| `batcher.Batcher<?>`（跨聚合） | `Batcher.MixedBatcher` + `Batcher.MixedBatchRepository` + `Batcher.IMixedBatcher` | `Batcher` 已是静态拦截入口类名，故跨聚合实例命名 `MixedBatcher` 避冲突；实体须实现 `Model.IDomainModel` 非泛型持久化切面 |
| `batcher.Batcher<T>`（单类型） | `Batcher.Batcher<T>` + `Batcher.BatchRepository` | `PersistOneByOne` 优先走 `DomainRepository.AddDirect/SaveDirect/DeleteDirect` 直写内核，避免二次提交与重复 `MarkStored` |
| `batcher.BatchRepository` | `Batcher.BatchRepository`；Mongo 位点 `Repository.Mongo.MongoBatchRepository` | 对应 Java `Monitor.getBatchRepository()` 装配位点；.NET 为 `Monitor.ConfigBatchRepository()/GetBatchRepository()` 单例缓存 + `ConfigDBConnection` 按 `IBatchRepositoryProvider` 检测装配 |
| `monitor.Monitor` / `monitor.StoreNotice` | `Monitor.Monitor`（`AsyncLocal` 会话/批量亲和） | Java `ThreadLocal` → .NET `AsyncLocal`；`GetBatchRepository()` 单例缓存；`RegisterLookupModel` 返回 `Lookup.LookupHandler`；另提供非泛型 `Notice(Entity,…)/Notice(DomainEvent,Entity)` 重载供跨聚合路径使用 |
| `session.DomainSession` | `Session.DomainSession` | 去反射：`IsStored/GetPendingEvents/MarkCommitted` 走 `IDomainModel` 强类型；`InvokeRepository(Entity, repo, OperationType)` 直写内核优先 + `MethodInfo` 缓存，`TargetInvocationException` 拆内层异常 |
| `model.FunctionModel` vs `EntityFunctionModel<T>` | `Monitor/FunctionModel.cs`（非泛型载荷） vs `Monitor/EntityFunctionModel.cs` / `EventFunctionModel.cs`（泛型） | 二者**不合并**：非泛型承载跨聚合/序列化载荷，泛型承载强类型回调；`IEntityFunction` / `IEventFunction` 已拆为独立文件（同命名空间，源码兼容） |
| `repository.mongo.SortEnum` | `Repository.Mongo.SortEnum`（`ASC=1/DESC=-1`） | 与查询 DSL `Finder.Sort/SortType` **不是重复**：前者是 Mongo 驱动持久化值，后者是领域查询意图，经 `FindBuild.Sort` 显式映射，请勿合并删除 |
| `repository.mongo.BatchRepository` | `Repository.Mongo.MongoBatchRepository` | 见上：`BatchPlan` → 快照集合 bulk 写入；`atomic=true` 需副本集/分片集群，否则回退逐批写入并警告 |

### 线兼容（wire-compat）保留项

- `FuncType` 保持**小写**（`add/modify/delete`），与 Java 序列化/事件流一致。
- 快照字段保持**双下划线**：`__stored__` / `__startVersion__`。
- `BatchOperation` 保留 **7 参构造**；`DomainSession.Complete` 保留 `[Obsolete]`；`SetSessionFunction(Action)` 重载保留。
- `DomainModel<T>` 新增 `[IgnoreField]` 属性视图，不进快照；持久化切面统一为 `Model.IDomainModel`（`StoredFlag/StartVersion/GetVersion/GetEvents/GetPendingEvents/PreparePendingEventSnapshots/IsStored/MarkStored/MarkEventsPersisted`）。

### Batcher / Session / InfraConfig 用法

```csharp
// 单类型批量（拦截器仍可用 BatcherCapture）
var batcher = new Batcher<Order>(monitor, repository);
var result = batcher.Run(o => o.Confirm(), orders, new BatchOptions { Atomic = true, FailureMode = FailureMode.Continue });

// 跨聚合批量（对齐 Java Batcher<?>）
IMixedBatcher mixed = new MixedBatcher(monitor, new MixedBatchRepository(monitor));
var mixedResult = mixed.Run(new Dictionary<Entity, Action<Entity>>
{
    [order] = _ => order.Confirm(),
    [coupon] = _ => coupon.Lock(),
});

// 会话 / Unit of Work（与 Batcher 混用会抛错，二选一）
using var session = SessionManager.Start(monitor);
order.AppendEvent(new OrderConfirmed(...));
session.Commit();

// 批量大小 / 刷新 BulkWriter（如 MongoLookupBulkWriter）
InfraConfig.BulkBatchSize = 500;
lookupHandler.RefreshConfig();
```

---

## License

[Apache License 2.0](LICENSE) © 2026 soonboot