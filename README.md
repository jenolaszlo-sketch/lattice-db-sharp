# LatticeDbSharp

[![NuGet](https://img.shields.io/nuget/v/LatticeDbSharp)](https://www.nuget.org/packages/LatticeDbSharp)
[![CI](https://github.com/jenolaszlo-sketch/lattice-db-sharp/actions/workflows/ci.yml/badge.svg)](https://github.com/jenolaszlo-sketch/lattice-db-sharp/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/jenolaszlo-sketch/lattice-db-sharp)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

LatticeDbSharp is an independent, safe, idiomatic .NET binding for
[LatticeDB](https://github.com/jeffhajewski/latticedb), an embedded single-file
property-graph database with Cypher, vector search, full-text search, ACID
transactions, and durable streams.

LatticeDbSharp is an **unofficial community wrapper**. It is not affiliated
with, sponsored by, endorsed by, or maintained by the LatticeDB project or its
authors.

LatticeDB was created by
[Jeff Hajewski](https://github.com/jeffhajewski) and is developed by the
[LatticeDB contributors](https://github.com/jeffhajewski/latticedb/graphs/contributors).
They receive full credit for the database engine, C API, file format,
documentation, and upstream language bindings. LatticeDbSharp claims no
ownership of LatticeDB, its source code, native binaries, documentation,
project name, or trademarks.

The goal is to make the native C API feel natural in .NET without exposing
P/Invoke, pointers, opaque native handles, or manual memory ownership:

    LatticeDB C API
           |
    verified interop and SafeHandle ownership
           |
    idiomatic synchronous .NET API
           |
    LatticeDbSharp

## Status

**The current source targets 0.3.0.** The binding opens file or memory databases, runs explicit
read/write transactions, and covers node/edge creation, edge deletion,
properties, detached traversal, commit, rollback, single-writer behavior,
finalizer cleanup, close/reopen persistence, cross-process locking, and
hard-kill recovery on Linux x64, Windows x64, and macOS ARM64. The managed
assembly is trim-clean and NativeAOT-compatible, proven by a gated smoke test.

The upstream baseline is pinned to LatticeDB v0.15.0, commit
9800159e22e200f2b6888c7c6be1810adb695506. Its exact public header and a
machine-readable capability matrix are archived and hash-verified in CI. Native
assets are built through a pinned, traceable process from that exact base plus a small, disclosed,
hash-verified durable-recovery patch carried in this repository. The staging and
package validation carry verified Linux x64, Windows x64, and macOS ARM64
runtimes with their build, patch, and ABI evidence. The 43-test native suite
runs for each supported platform in CI. Other runtime identifiers fail with an actionable
diagnostic rather than loading an arbitrary system library. The macOS asset is
built natively on the macOS CI runner and ad-hoc code-signed so its pages map
on Apple Silicon.

## Why it is interesting

LatticeDB combines graph traversal, semantic vector retrieval, BM25 full-text
search, and durable change streams in one embedded database and query layer.
That makes it a compelling candidate for connected local knowledge and code
intelligence workloads without operating a database server.

LatticeDbSharp stays a binding rather than becoming an ORM or Penghou-specific
framework. Hetu may later evaluate it against the current DuckDB-backed design,
but replacement is a benchmark and capability decision after the wrapper's
retrieval features are proven.

## API

The 0.3.0 source surface provides synchronous database lifecycle, transactions, graph
values and identifiers, node/edge/property operations, detached edge
traversal, Cypher preparation and execution, parameter binding, result
lifetimes, structured native/query errors, property indexes, vector writes and
search, and BM25/fuzzy full-text indexes and search. Batch vector ingestion
reports native progress and requires rollback after a partial failure. Values
are detached and support structural equality, including nested lists and maps.

The native v0.15.0 vector setter accepts a `key` parameter but ignores it and
stores one vector per node, so LatticeDbSharp intentionally omits that key from
the managed API rather than implying multiple named vectors.

Fake asynchronous wrappers, LINQ-to-Cypher, object mapping, embedding generation,
and generic database abstractions are non-goals.

## Quick start

The package contains verified Linux x64, Windows x64, and macOS ARM64 native assets:

```shell
dotnet add package LatticeDbSharp
```

```csharp
using LatticeDbSharp;

using var database = LatticeDatabase.OpenMemory();
LatticeNodeId alice;

using (var write = database.BeginWriteTransaction())
{
    alice = write.CreateNode("Person");
    var bob = write.CreateNode("Person");
    _ = write.CreateEdge(alice, bob, "KNOWS");
    write.Commit();
}

using var read = database.BeginReadTransaction();
Console.WriteLine(read.NodeExists(alice));
foreach (var edge in read.GetOutgoingEdges(alice))
    Console.WriteLine($"{edge.Type} -> {edge.Target.Value}");
read.Commit();
```

Vector and full-text retrieval use the same explicit transaction model:

```csharp
using var vectorDb = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
{
    EnableVector = true,
    VectorDimensions = 3,
});
vectorDb.ExecuteWrite(write => write.BatchInsertNodes([
    new("Article", new float[] { 1, 0, 0 }),
    new("Article", new float[] { 0, 1, 0 }),
]));
var nearest = vectorDb.VectorSearch(new float[] { 1, 0, 0 }, count: 2);

vectorDb.ExecuteWrite(write =>
{
    var article = write.CreateNode("Article");
    write.SetProperty(article, "text", LatticeValue.From("quick brown fox"));
});
vectorDb.CreateNodeFtsIndex("Article", "text");
var matches = vectorDb.FtsSearch("Article", "text", "quick", limit: 10);
```

If a batch fails, catch `LatticeBatchInsertException` to inspect
`CompletedCount`, then roll back before retrying:

```csharp
using var write = database.BeginWriteTransaction();
try
{
    write.BatchInsertNodes(nodes);
    write.Commit();
}
catch (LatticeBatchInsertException error)
{
    Console.WriteLine($"Native progress: {error.CompletedCount}/{error.RequestedCount}");
    write.Rollback();
}
```

Databases can be snapshotted and restored without exposing native pointers:

```csharp
byte[] snapshot = database.Serialize();
using var restored = LatticeDatabase.Deserialize(snapshot);
```

Prepared queries support an atomic parameter-dictionary execution path plus
strict single-row and scalar helpers:

```csharp
using var query = database.Prepare(
    "MATCH (n:Person) WHERE n.name = $name RETURN n.name AS name");
using var read = database.BeginReadTransaction();
var name = query.ExecuteScalar(read, new Dictionary<string, LatticeValue>
{
    ["name"] = LatticeValue.From("Ada"),
}).AsString();
read.Commit();
```

Individual query operations are synchronized, but a sequence of separate
`Bind` calls followed by `Execute` belongs to one caller. Use the dictionary
overloads when a query is shared; every dictionary execution on that prepared
query must provide the same parameter names because the native API cannot clear
old bindings. Consume each result cursor from one caller. `ReadAll` holds the
cursor lock for the complete materialization, and retained rows are detached.

Transactions, prepared queries, and results are native owners. Dispose them in
reverse nesting order; a database intentionally refuses to close while a child
is active. An uncommitted transaction rolls back when disposed.

LatticeDbSharp transactions are explicit and do not currently enlist in
`System.Transactions.Transaction.Current`. The native API has no prepare/2PC
contract, so ambient enlistment would otherwise imply guarantees it cannot make.

## Build

The managed scaffold requires the .NET 10 SDK and targets .NET 8:

    dotnet build LatticeDbSharp.slnx
    dotnet test tests/LatticeDbSharp.Tests/LatticeDbSharp.Tests.csproj

Native integration tests are skipped by default for managed-only development.
The Linux x64, Windows x64, and macOS ARM64 CI gates build the exact pinned
base plus the verified patch set, check ABI evidence, and enable native tests
against the generated libraries. A NativeAOT smoke test publishes and runs a
trimmed binary against the staged engine on Linux. Additional platforms will
only be advertised after their own lifecycle, recovery, locking, and cleanup
gates pass.

## Documentation

- [Product specification](docs/product-spec.md)
- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Native runtime and packaging](docs/native-runtime.md)
- [Native ownership contract](docs/native-ownership.md)
- [Compatibility policy](docs/compatibility.md)

## License and attribution

Only the independently written LatticeDbSharp wrapper source is licensed under
the Apache License 2.0. LatticeDB is copyright © 2025 Jeff Hajewski and is
licensed separately under the MIT License. LatticeDB and all upstream artifacts
retain their original ownership, license, copyright, and attribution terms.
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
