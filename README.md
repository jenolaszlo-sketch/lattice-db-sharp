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

**0.1.0 is stable.** The binding opens file or memory databases, runs explicit
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
runtimes with their build, patch, and ABI evidence. All 26 expanded native
tests pass on each platform. Other runtime identifiers fail with an actionable
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

The 0.1.0 surface provides synchronous database lifecycle, transactions, graph
values and identifiers, node/edge/property operations, detached edge
traversal, Cypher preparation and execution, parameter binding, result
lifetimes, and structured native/query errors. It also exposes the pinned
engine's single configured vector write per node; vector search, full-text,
index, and stream surfaces follow only after their ownership models are
verified.

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
- [Design review and remediation notes](docs/review.md)

## License and attribution

Only the independently written LatticeDbSharp wrapper source is licensed under
the Apache License 2.0. LatticeDB is copyright © 2025 Jeff Hajewski and is
licensed separately under the MIT License. LatticeDB and all upstream artifacts
retain their original ownership, license, copyright, and attribution terms.
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
