# LatticeDbSharp Benchmarks

The reproducible BenchmarkDotNet harness lives in
`benchmarks/LatticeDbSharp.Benchmarks`. It exercises indexed node lookup,
top-10 vector search, full-text search, edge traversal, and parameterized
Cypher execution over a chain graph. The native library under test comes from
`LATTICEDBSHARP_NATIVE_LIBRARY`, exactly like the integration tests.

Run the smoke matrix in Release mode:

```powershell
$env:LATTICEDBSHARP_NATIVE_LIBRARY = "<repo>/native/staging/win-x64/lattice.dll"
dotnet run -c Release --project benchmarks/LatticeDbSharp.Benchmarks -- --job short --filter *RetrievalBenchmarks*
```

## Retrieval baseline (current)

Measured on 2026-09-13 using LatticeDB v0.15.0 plus the durable-recovery
patch, .NET 8.0.30, Windows 11, and an Intel Core Ultra 5 125H, with a short
smoke job (`--job short`). Results are local engineering baselines, not
portable performance guarantees.

| Operation | 100 nodes | 1,000 nodes |
|---|---:|---:|
| Indexed node find | 52.9 µs | 53.2 µs |
| Vector search, top 10 | 63.2 µs | 60.4 µs |
| Full-text search | 64.7 µs | 64.9 µs |
| Edge traversal | 40.3 µs | 40.5 µs |
| Cypher execute-all | 106.2 µs | 106.1 µs |

The fixture chains every node to the next with one typed edge. Indexed node
lookup uses a label/property index, vector search runs HNSW top-10 over
8 dimensions, full-text search hits a declared label/property index, and
traversal expands from the chain end. Every operation stays flat from 100 to
1,000 nodes, confirming index-backed rather than scan-backed paths.

Benchmark output belongs under `BenchmarkDotNet.Artifacts`, which is ignored
by Git. Re-run the matrix when changing retrieval shapes, index behavior, or
result materialization.
