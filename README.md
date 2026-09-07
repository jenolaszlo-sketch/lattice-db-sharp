# LatticeDBSharp

LatticeDBSharp is an independent, safe, idiomatic .NET binding for
[LatticeDB](https://github.com/jeffhajewski/latticedb), an embedded single-file
property-graph database with Cypher, vector search, full-text search, ACID
transactions, and durable streams.

The goal is to make the native C API feel natural in .NET without exposing
P/Invoke, pointers, opaque native handles, or manual memory ownership:

    LatticeDB C API
           |
    verified interop and SafeHandle ownership
           |
    idiomatic synchronous .NET API
           |
    LatticeDBSharp

## Status

The repository is in **Phase 0: native feasibility**. The managed solution,
tests, documentation, package metadata, and CI are scaffolded. The native API
is deliberately not exposed yet: declarations must be derived from and tested
against the pinned lattice.h, not guessed from examples.

The upstream baseline is pinned to LatticeDB v0.10.0, commit
bcf4c553411cb6758b4b1481f1ec0be5e4872af9. No native binaries are currently
vendored or packaged.

## Why it is interesting

LatticeDB combines graph traversal, semantic vector retrieval, BM25 full-text
search, and durable change streams in one embedded database and query layer.
That makes it a compelling candidate for connected local knowledge and code
intelligence workloads without operating a database server.

LatticeDBSharp stays a binding rather than becoming an ORM or Penghou-specific
framework. Hetu may later evaluate it against the current DuckDB-backed design,
but replacement is a benchmark and capability decision after the wrapper's
retrieval features are proven.

## Intended API

The first useful release will provide synchronous database lifecycle,
transactions, graph values and identifiers, node/edge/property operations,
Cypher preparation and execution, parameter binding, result lifetimes, and
structured native/query errors. Vector, full-text, index, and stream surfaces
follow only after the core ownership model is verified.

Fake asynchronous wrappers, LINQ-to-Cypher, object mapping, embedding generation,
and generic database abstractions are non-goals.

## Build

The managed scaffold requires the .NET 10 SDK and targets .NET 8:

    dotnet build LatticeDBSharp.slnx
    dotnet test tests/LatticeDBSharp.Tests/LatticeDBSharp.Tests.csproj

Native integration tests remain disabled until Phase 0 produces a verified
shared library. Platform support will only be advertised after integration,
recovery, locking, and cleanup tests pass on that runtime identifier.

## Documentation

- [Product specification](docs/product-spec.md)
- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Native runtime and packaging](docs/native-runtime.md)
- [Compatibility policy](docs/compatibility.md)

## License and attribution

LatticeDBSharp source is licensed under the Apache License 2.0. LatticeDB and all
upstream artifacts retain their own license and copyright terms. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
