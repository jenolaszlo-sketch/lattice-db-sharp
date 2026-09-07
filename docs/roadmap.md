# LatticeDBSharp roadmap

## Vision

Provide a safe, predictable .NET binding for the official LatticeDB C API while
preserving the native engine's capabilities and semantics. The binding remains
independent of Penghou. A later Hetu spike decides whether LatticeDB improves
code-graph and retrieval quality enough to replace or complement DuckDB.

## Phase 0 — prove the native boundary

- [x] Scaffold the managed solution, unit/integration test split, sample, CI,
  package metadata, architecture, compatibility policy, and native provenance.
- [x] Pin LatticeDB v0.10.0 at
  bcf4c553411cb6758b4b1481f1ec0be5e4872af9.
- [ ] Archive and review the pinned lattice.h; inventory every owned, borrowed,
  allocated, and freed native type.
- [ ] Reproduce the shared build with Zig 0.16.0 on Linux x64 and macOS ARM64.
- [ ] Attempt Windows x64 without forking upstream; document any isolated build
  changes required.
- [ ] Generate or mechanically verify LibraryImport declarations, enum values,
  structure sizes/offsets, packing, Boolean representation, UTF-8, size_t, and
  pointer ownership.
- [ ] Implement internal library resolution and minimal database, transaction,
  and result SafeHandle types.
- [ ] Prove open/create, two nodes and an edge, commit, Cypher query,
  close/reopen persistence, rollback, and deterministic cleanup.
- [ ] Enable the native integration-test matrix only for platforms that pass.

Exit: the pinned ABI is loadable and ownership-safe on at least Linux x64 and
macOS ARM64; Windows support is either proven or explicitly deferred.

## Phase 1 — core binding (0.1.0)

- [ ] Database options, file and in-memory lifecycle, read-only mode, and runtime
  version diagnostics.
- [ ] Read/write transaction state machine, commit, rollback, disposal, and
  explicit single-writer behavior.
- [ ] Typed node/edge identifiers and non-lossy LatticeValue.
- [ ] Node, edge, label, property, and traversal APIs.
- [ ] Prepared Cypher queries, parameter binding, transaction-mode inspection,
  rows/results, and deterministic result lifetime.
- [ ] Native exception hierarchy and query stage/location diagnostics.
- [ ] Functional, recovery, lock-contention, malformed-input, and repeated
  create/query/dispose tests.
- [ ] Working quick-start application and API documentation.

Exit: the 0.1.0 acceptance criteria in the product specification are covered by
executable tests without public pointers or manual native ownership.

## Phase 2 — retrieval (0.2.0)

- [ ] Vector storage, search, bulk insertion, dimension validation, and result
  ownership.
- [ ] Per-property node/edge FTS indexes, BM25 results, and fuzzy search.
- [ ] Property-index lifecycle and indexed lookup without silent scan fallback.
- [ ] Retrieval correctness and wrapper-overhead benchmarks.

Exit: graph traversal, vector similarity, and text retrieval can be combined
through the wrapper with verified ownership and error behavior.

## Phase 3 — durable events (0.3.0)

- [ ] Named stream publication/read, sequence numbers, offsets, and trimming.
- [ ] Built-in graph changefeed.
- [ ] Transactional relationship between graph mutations and stream records.
- [ ] Restart, replay, and offset tests.

LatticeDBSharp exposes these as local durable logs, not as a queue, lease,
retry, or distributed messaging framework.

## Phase 4 — hardening

- [ ] Reproducible RID-native packaging and package-content audit.
- [ ] Native compatibility matrix and upgrade protocol.
- [ ] WAL recovery, corruption, checksum, read-only, full-database, and lock
  failure tests.
- [ ] Memory stress/leak checks for every result and buffer owner.
- [ ] BenchmarkDotNet project measuring wrapper overhead separately.
- [ ] Public API review, XML documentation, release checklist, and NuGet
  automation.

Stable 1.0 waits for real consumer use and at least one upstream ABI upgrade.

## Hetu validation spike

After Phase 2, build an experimental Hetu provider outside this repository.
Compare the current implementation with LatticeDB using the same representative
repositories and questions:

- semantic code lookup;
- dependency traversal;
- test-to-code impact;
- architecture/document association;
- minimal context-set construction.

Measure retrieval relevance, latency, index cost, persistence/recovery,
operational complexity, and migration cost. LatticeDB replaces or complements
DuckDB only if the evidence is materially better.

## Non-goals

- ORM, object persistence, or LINQ-to-Cypher.
- Generic database abstractions shared with SQLite or DuckDB.
- Penghou, Hetu, Cangjie, Baize, RAG, or embedding-provider concepts.
- Fake asynchronous APIs or unsupported cancellation claims.
- Hidden writer serialization or silent index-to-scan fallback.
- Distributed database, broker, lease, retry, or dead-letter semantics.
- Public APIs copied from documentation before the pinned header is verified.

## Resume point

Start with the pinned-header ownership inventory and ABI conformance harness.
Do not design the broad public API first. The first code checkpoint should load
the native library, report its version, and prove database, transaction, and
result cleanup against the exact pinned build.
