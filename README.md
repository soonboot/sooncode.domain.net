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
<PackageReference Include="Sooncode.Domain.Infrastructure" Version="1.0.0" />
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

## License

[Apache License 2.0](LICENSE) © 2026 soonboot