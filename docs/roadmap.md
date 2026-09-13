# LatticeDbSharp roadmap

## Vision

Provide a safe, predictable .NET binding for the official LatticeDB C API while
preserving the native engine's capabilities and semantics. The binding remains
independent of Penghou. A later Hetu spike decides whether LatticeDB improves
code-graph and retrieval quality enough to replace or complement DuckDB.

## Phase 0 — prove the native boundary

- [x] Scaffold the managed solution, unit/integration test split, sample, CI,
  package metadata, architecture, compatibility policy, and native provenance.
- [x] Pin LatticeDB v0.15.0 at
  9800159e22e200f2b6888c7c6be1810adb695506.
- [x] Archive the exact pinned `lattice.h`, verify its content hash in CI, and
  record a machine-readable capability matrix.
- [x] Enforce native provenance through build/package metadata and package the
  upstream license, manifest, and capability matrix.
- [x] Harden the Phase 0 package audit and test that misplaced/versioned native
  binaries and duplicate nuspecs are rejected.
- [x] Add managed CI and tag/manual NuGet publishing automation using Node
  24-capable actions and NuGet trusted publishing.
- [x] Complete the ownership inventory for every owned, borrowed, allocated,
  and freed native type, including consuming error outcomes.
- [x] Reproduce the shared build with hash-pinned Zig 0.16.0 on Linux x64;
  verify exports, the v4-options ABI, and managed loading against the real engine.
- [x] Reproduce and execute the equivalent native gate on macOS ARM64
  (native macOS runner build, ad-hoc signing, ABI probe, staged dylib).
- [x] Build and execute the equivalent native gate on Windows x64 from the
  pinned base plus the disclosed patch set; stage `lattice.dll` with ABI,
  patch, and export evidence.
- [x] Mechanically verify the currently bound enums, open options, recursive
  value/container sizes and offsets, Boolean representation, UTF-8, size_t,
  and pointer ownership. Repeat this gate for every newly bound ABI surface.
- [x] Implement deterministic native library resolution, version compatibility
  diagnostics, and an executable v4-options ABI layout probe.
- [x] Implement and native-test the checked database owner and minimal
  transaction handle, including parent retention, rejected close, abandoned
  child finalization, and consuming commit/rollback ownership transitions.
- [x] Implement checked prepared-query and result handles with distinct borrowed
  native lifetimes and detached managed row/value snapshots.
- [x] Prove open/create, two nodes and an edge, commit, close/reopen persistence,
  rollback, single-writer rejection/recovery, and deterministic transaction
  cleanup on Linux x64.
- [x] Prove parameterized Cypher, nested list/map round trips, structured query
  diagnostics, deterministic result cleanup, and scoped transaction helpers.
- [x] Enable native integration tests in the verified Linux x64, Windows x64,
  and macOS ARM64 gates.

- [x] Resolve the durable-preview blocker with a minimal, hash-verified native
  patch: persist new tree roots before transactions can commit, repair only an
  incomplete final WAL frame, and continue rejecting checksum or mid-log
  corruption. All 22 expanded native tests pass on Linux x64, Windows x64, and
  macOS ARM64.

Exit: the pinned ABI is loadable and ownership-safe on Linux x64, Windows
x64, and macOS ARM64.

## Phase 1 — core binding (0.1.0)

- [x] Database options, file and in-memory lifecycle, read-only mode, and runtime
  version diagnostics.
- [x] Read/write transaction state machine, commit, rollback, disposal, and
  explicit single-writer behavior.
- [x] Typed node/edge identifiers and bounded, detached, non-lossy
  `LatticeValue` for every pinned recursive value shape.
- [x] Node creation/deletion, labels, properties, and configured vector writes.
- [x] Edge, traversal, and remaining graph APIs: edge deletion by
  endpoints/type, edge properties, and outgoing/incoming/by-type traversal
  returning detached identities. Index-backed find and admin scan stay in
  Phase 2 retrieval.
- [x] Prepared Cypher queries, parameter binding, rows/results, and deterministic
  result lifetime are complete.
- [x] Expose and test advisory transaction-mode inspection through
  `LatticeQuery.MayWrite`.
- [x] Native exception hierarchy and query stage/location diagnostics for the
  currently exposed surface.
- [x] The native package verifies exact Linux and Windows RID/path/hash contents,
  patch provenance, and runs from clean package-only consumers on both OSes.
- [x] Functional, recovery, lock-contention, malformed-input, and repeated
  create/query/dispose tests: edge round-trips, concurrent reads on one
  transaction, 25-cycle create/query/dispose stability, and malformed edge
  input rejection alongside the existing recovery suite.
- [x] Working Linux x64 quick-start application and initial API documentation.

Exit: the 0.1.0 acceptance criteria in the product specification are covered by
executable tests without public pointers or manual native ownership.

## Phase 2 — retrieval (0.2.0)

- [x] Vector storage, search, bulk insertion, dimension validation, and result
  ownership: `BatchInsertNodes`, database- and transaction-scoped top-k
  search with detached hits, and vector query-parameter binding.
- [x] Per-property node/edge FTS indexes, BM25 results, and fuzzy search,
  with index-existence checks and explicit failure without a declared index.
- [x] Property-index lifecycle and indexed lookup without silent scan
  fallback: create/drop/exists plus indexed find with mandatory positive
  limits (the engine rejects zero).
- [x] Retrieval correctness and wrapper-overhead benchmarks: 29 integration
  tests plus the `benchmarks/LatticeDbSharp.Benchmarks` harness with the
  flat 100-to-1000 baseline in BENCHMARKS.md.

Exit: graph traversal, vector similarity, and text retrieval can be combined
through the wrapper with verified ownership and error behavior.

## Phase 2 — follow-ups (0.2.x)

### Deliberately unbound native surface

These stay out of the wrapper unless a concrete consumer requires them:

- Admin `lattice_edge_scan` (the header marks it unsuitable for hot-path expansion).
- `lattice_deserialize_borrowed` (pinning caller memory for a database lifetime is GC-hostile; use copying `Deserialize`).
- `lattice_query_cache_clear` / `lattice_query_cache_stats` (diagnostic admin surface, no consumer yet).
- `lattice_node_remove_property` does not exist in the pinned header; node property removal is Cypher-only by engine design.
- The native HTTP embedding client (`lattice_embedding_client_*`) stays unbound: model calls belong behind the host's model gateway (Baize in Penghou), which owns credentials, retry, usage, and provenance. Binding it would duplicate that governance inside the database wrapper.

### Embedding provider package

Ship text-to-vector convenience in a separate `LatticeDbSharp.Extensions.AI`-style
package, never in core. Core keeps only vector storage and search; the package
adapts embedding stacks without taking a dependency on any of them:

- [ ] Investigate `Microsoft.Extensions.AI` first: adopt `IEmbeddingGenerator<string,
  Embedding<float>>` if it fits (note its batch shape needs a single-text
  adapter); introduce a minimal local interface only if it does not.
- [ ] Transaction-scoped helpers on the real API shapes (`LatticeTransaction`
  owns `SetVector`; results are `LatticeVectorHit`): embed-and-store plus
  embed-query-then-top-k, with cancellation flowing into the provider only
  (native vector ops follow current ABI governability, documented as such).
- [ ] Dimension handling without invented checks: no managed accessor exists
  for configured dimensions, so rely on native rejection and document it.
- [ ] Include a `LatticeHashEmbeddingProvider` over the already-bound
  `lattice_hash_embed` as the no-dependency offline example, documented as
  lexical similarity rather than semantic evidence.
- [ ] Provider errors propagate unchanged (never converted to database
  errors); no provenance persistence in the wrapper.
- [ ] Integration tests with fake providers (native engine required, so these
  live in the integration suite): single invocation, vector passthrough,
  cancellation forwarding, dimension errors, top-k forwarding, metadata
  neutrality, and no native-HTTP-client use.

## Extensions.AI package (proposed)

The full proposal lives in [extensions-ai-proposal.md](extensions-ai-proposal.md):
a `LatticeDbSharp.Extensions.AI` package adapting Microsoft's `VectorStore`
contract to LatticeDB, with `IEmbeddingGenerator<string, Embedding<float>>`
for model-backed embeddings and Baize as an external adapter, never a
dependency. It supersedes the earlier custom-`IEmbeddingProvider` sketch:
no new embedding interface is introduced.

Review notes recorded against the proposal:

- Attribute and contract names (`VectorStoreKey/Data/Vector`,
  `GetCollection`, `GetDynamicCollection`, `GetService`,
  `ListCollectionNamesAsync`) verified against current
  Microsoft.Extensions.VectorData; add the missing
  `CollectionExistsAsync` to the contract list.
- Microsoft marks typed `GetCollection<TKey, TRecord>` itself
  `RequiresUnreferencedCode`/`RequiresDynamicCode`: the package's AOT story
  routes through `GetDynamicCollection` plus explicit definitions, which
  promotes dynamic models from milestone-1.1 nice-to-have to AOT-critical.
- The engine stores one vector per node, so multi-vector schemas must be
  rejected rather than investigated further.
- Microsoft expects `VectorStore` implementations to be thread-safe; the
  adapter must document and hold that guarantee.
- Distance-function mapping, filter-expression coverage, and score
  semantics remain genuine investigations (Milestone 0), as does whether
  DataIngestion/Agent Framework compat emerges without adapters.
- New package starts at `0.1.0-preview.1` following repository versioning.

## Phase 3 — durable events (0.4.0)

- [ ] Named stream publication/read, sequence numbers, offsets, and trimming.
- [ ] Built-in graph changefeed.
- [ ] Transactional relationship between graph mutations and stream records.
- [ ] Restart, replay, and offset tests.

LatticeDbSharp exposes these as local durable logs, not as a queue, lease,
retry, or distributed messaging framework.

## Phase 4 — hardening

- [x] Reproducible multi-RID native packaging after the minimum Phase 1 package
  gate has proven one runtime identifier: Linux x64, Windows x64, and macOS
  ARM64 each build, gate, and pack independently.
- [ ] Native compatibility matrix and upgrade protocol.
- [x] WAL recovery, corruption, checksum, read-only, full-database, and lock
  failure tests: widespread file corruption fails at open while a healthy
  database stays usable, truncated tails repair or reject without hanging,
  full-graph serialize/restore preserves 100 nodes and 99 edges, second
  opens of locked files fail fast, and single-byte tolerance in unchecked
  space is documented rather than asserted.
- [x] Memory stress/leak checks for every result and buffer owner: 100
  create/query/dispose lifecycles stay within a 64 MiB managed-growth bound.
- [ ] BenchmarkDotNet project measuring wrapper overhead separately.
- [x] Public API review, XML documentation, and stable-release checklist:
  analyzer-enforced API declarations shipped for 0.1.0, full XML surface docs,
  and the package verifier contract covering all three RIDs.

Stable 1.0 waits for real consumer use and at least one upstream ABI upgrade.

## Hetu validation spike

Before starting this spike, complete the remaining idiomatic-.NET API review.
The preview already has deterministic `using`, non-throwing rollback disposal,
scoped `ExecuteRead`/`ExecuteWrite`, ordinal/name row access, and eager detached
query materialization. Still evaluate enumeration shapes, conversion and
nullable/Try patterns, and additional concise query conveniences. Ambient
`System.Transactions` enlistment is deferred: the pinned engine exposes no
prepare/2PC contract, so any future opt-in enlistment must reject promotion and
must not imply distributed atomicity.

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

## Preview API ergonomics decisions

- [x] Keep database, transaction, query, and result owners explicitly
  `IDisposable`; deterministic `using` is the supported lifetime model.
- [x] Keep callback transactions synchronous and eager: `ExecuteRead` and
  `ExecuteWrite` commit on success, roll back on failure, and reject native
  owners escaping the callback. Query cursors are not exposed as lazy
  `IEnumerable` values; use `ExecuteAll` or `ReadAll` for detached rows.
- [x] Prefer ordinal/name row indexers, `TryGetProperty`, `GetProperty`, and
  defensive-copy value accessors over implicit conversions or lossy coercion.
- [x] Keep the preview vector surface limited to the pinned engine's configured
  per-node vector. The native `key` parameter is passed as null for ABI
  compatibility but is not a multi-vector namespace; retrieval/vector search
  remains later.
- [x] Defer `System.Transactions`, async/cancellation overloads, LINQ-to-Cypher,
  and broad convenience overloads until the native transaction and workload
  semantics justify them. In particular, the pinned engine has no prepare/2PC
  contract, so ambient enlistment must not imply distributed atomicity.
- [ ] Revisit enumeration, nullable/Try patterns, query conveniences, and
  vector/property convenience overloads after edge/traversal and retrieval
  APIs have real consumer usage.

## Resume point

The Linux x64, Windows x64, and macOS ARM64 wrapper slices are implemented:
checked database, transaction, query, result, and recursive-value ownership;
scoped transaction conveniences; verified native packaging; and clean
package-only consumers. Advisory query write classification, node and edge
creation/deletion, labels, properties, edge properties, detached traversal,
and configured vector writes are now complete. The real-engine suite passes
the 43-test native suite on each OS, including child-process hard-kill recovery,
resolver pinning, corruption deadlines, and process-memory evidence.
The current 0.3.0 source surface completes graph and retrieval work; durable
streams are the next slice.
The preview intentionally keeps the API synchronous, deterministic, detached,
and non-lossy; revisit the deferred ergonomics only after those native paths
have real consumer evidence.
