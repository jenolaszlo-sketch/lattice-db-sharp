# LatticeDBSharp design and implementation review

Reviewed 2026-09-07 against local commit `60258c0` and the exact upstream
[v0.10.0 header](https://github.com/jeffhajewski/latticedb/blob/bcf4c553411cb6758b4b1481f1ec0be5e4872af9/include/lattice.h).

## Assessment and scope

The scaffold has a sensible boundary: a synchronous, independent .NET binding,
with internal interop and explicit native ownership. Its documentation is clear
that it cannot open a database yet. Keep those choices.

There is no implemented database, transaction, value, query, or native loader to
review for runtime correctness. The only managed test checks assembly identity;
the integration test is an empty, permanently skipped placeholder. Findings below
separate concrete scaffold/specification defects from proposed implementation
contracts. They are recommendations, not claims that the contracts already exist.

This review inspected source, project configuration, package verification, CI,
all architecture documents, and the submitted product specification. It read the
header directly from the pinned revision and verified the remote release tag.
It did not build the native library or independently verify Zig implementation,
Windows support, recovery, or performance. No runtime code was changed.

Priority: P1 must be addressed before the affected feature is implemented or
released; P2 should be resolved while shaping the core API; P3 is a later improvement.

## P1 — concrete specification and delivery defects

### R01. The product specification exceeds the pinned C API

Evidence: [product-spec.md](product-spec.md), [compatibility.md](compatibility.md),
[upstream.json](../native/upstream.json), and the pinned header linked above.

| Proposed capability | Pinned v0.10.0 header | Required decision |
| --- | --- | --- |
| Open options v4 and `Lock` | Options end at v3; no lock option | Use v3 or deliberately update the upstream pin |
| Automatic query read/write classification | No `lattice_query_writes` declaration | Require explicit transaction mode until a supported API exists |
| Per-property FTS creation/deletion | General FTS indexing/search exists; proposed creation/deletion functions do not | Document the supported FTS model or change the pin |
| Separate database-locked native error | Error enum ends at `VALUE_TOO_LARGE`; no database-locked entry | Do not invent a native error mapping |
| Edge deletion by stable ID | `lattice_edge_delete` accepts source, target, and type | Expose actual semantics and investigate parallel-edge behavior |
| Complete node/edge property snapshots | Individual property getters exist; header does not expose property enumeration | Do not promise complete `Properties` snapshots without a proven query path |
| Node property removal | No direct node-remove-property function | Verify a parameterized Cypher path or defer the method |

This is the most important finding: an API implementation based on the examples
would bind missing symbols or expose unsupported behavior. Create a capability
matrix from the pinned header before freezing public signatures. Preserve the
original proposal as historical input and identify which document is authoritative
when it disagrees with the implementation plan.

The header DOES contain recursive LIST/MAP values and generic query binding.
Do not remove those capabilities based on older binding documentation. Verify
their execution behavior against the pinned native implementation.

### R02. The package audit does not enforce its advertised native exclusion

Evidence: [Verify-NuGetPackage.ps1](../eng/Verify-NuGetPackage.ps1).

The script only rejects selected extensions below `runtimes/`. A native
`content/liblattice.so`, root-level native DLL, or versioned `liblattice.so.1`
passes that check. It also accepts the first nuspec without requiring a single
matching package identity.

Use an explicit allowed-entry policy for the current scaffold: known managed
assemblies/documentation, package metadata, notices, and expected symbol files.
When native assets are introduced, validate exact RID/path/hash entries against
the build manifest. Require the expected package ID, target framework, license,
and one nuspec. Test the audit using deliberately malformed archives, including
versioned shared libraries and misplaced binaries.

### R03. Managed CI does not establish native platform support

Evidence: [ci.yml](../.github/workflows/ci.yml),
[NativeFeasibilityTests.cs](../tests/LatticeDBSharp.IntegrationTests/NativeFeasibilityTests.cs).

CI runs one assembly-name assertion on three operating systems and never runs
native integration tests. The skipped test body would pass without exercising
anything if its skip attribute were simply removed. Keep the current CI status
explicitly described as managed-scaffold coverage. Replace the placeholder with
real open/commit/query/reopen assertions before enabling a native gate; fail that
gate when its configured binary is absent or incompatible.

CI installs SDK 10 but executes net8.0 tests. Explicitly install the .NET 8 runtime
or SDK too, instead of relying on hosted runners to have it preinstalled. Record
runner architecture and distinguish a managed OS matrix from the native RID matrix.

### R04. Native provenance is recorded but not enforced

Evidence: [upstream.json](../native/upstream.json),
[LatticeDBSharp.csproj](../src/LatticeDBSharp/LatticeDBSharp.csproj), CI.

The JSON pin is not consumed by builds, tests, or package metadata. Zig 0.16.0 is
recorded, but its suitability for this exact source revision has not been proven.
Use one machine-readable manifest to drive checkout verification, header identity,
toolchain selection, build flags, export checks, ABI probes, and native asset hashes.
Publish relevant provenance in the package and assembly metadata. Verify that the
source checkout resolves to the recorded commit; a version string alone cannot
distinguish two different builds or forks claiming the same version.

### R05. Packaging and recovery gates arrive too late

Evidence: [roadmap.md](roadmap.md), phases 1 and 4; product-spec acceptance criteria.

The first useful package needs one reproducibly packaged, integration-tested RID,
working consumer installation, and basic crash/rollback/locking coverage. These
cannot all wait for phase 4 if phase 1 is advertised as an installable binding.
Move the minimum package and recovery gate into the first release; broaden the
platform matrix, stress tests, and benchmarks later. Keep Windows feasibility near
the start because the intended local consumer runs on Windows.

## P1 — ownership and transaction contracts to settle before coding

### R06. SafeHandle alone cannot model database close correctly

The pinned `lattice_close` documentation distinguishes two outcomes: active
dependent handles cause rejection while leaving the database open; an I/O error
after finalization begins consumes the handle. A single unconditional release or
retry rule can leak resources or double-close a consumed pointer.

Define explicit close/disposal behavior and an ownership state machine. Prevent
new children once closing starts, retain the database while dependent native
objects exist, and make a successful or consuming close invalidate the handle
exactly once. Separate explicit close error reporting from non-throwing finalizer
cleanup. Returning false from `ReleaseHandle` is not a resource retry strategy.
Test child-first disposal, parent-first disposal, disposal during calls, rejected
close, and consuming close failure using native probes or fault injection.

### R07. Owned and borrowed values must have different internal paths

`lattice_node_get_property` and `lattice_edge_get_property` transfer ownership;
their value trees require `lattice_value_free`. `lattice_result_get` borrows its
tree from the result and must NOT be freed with that function. Stream batch
payloads are borrowed too. Input setters and binders borrow trees for the call.

Use a shared decoder plus distinct ownership scopes at each call site. Never
represent every returned `lattice_value` as an owning handle. Some resources are
structs with allocated interiors or arrays requiring their count when freed;
their release mechanism must match the ABI rather than the proposed handle list.
Keep the database/query/result dependency chain alive until each call finishes.
Test allocations freed exactly once on success, partial decode, and exceptions.

### R08. Transaction completion needs failure semantics, not just Dispose

The header documents consumed transaction handles after successful commit or
rollback, but does not fully specify every failed completion path. Inspect the
implementation and test failure cases before deciding whether rollback or retry
is valid after a failed commit.

Define Active, Committed, RolledBack, and unusable/failure states as required by
the actual ABI. Only permit operations valid in the current state. Completion
must not accidentally invoke rollback on a consumed handle. Preserve the original
callback exception if rollback also fails and expose the cleanup failure separately.
Do not automatically retry writes whose commit outcome is uncertain.

The batch-insert header explicitly permits partial success and recommends rollback.
Make this visible in the managed contract: report partial progress and mark the
transaction rollback-required, or otherwise require an explicit documented recovery
decision. A caught insert exception must not silently become a partial commit.

### R09. Auto-transaction queries have ambiguous commit timing

The spec combines `db.Query(...)`, automatic writes, and lazy result enumeration.
It does not define what happens when callers never enumerate, stop after one row,
throw in a loop, or dispose the result. That can make persistence depend on consumer
iteration patterns. The pinned ABI also lacks the proposed write classifier.

Start with execution inside an explicit read or write transaction. Later provide
separately named convenience read/materialization and write methods with documented
commit timing. Never classify Cypher using string prefixes. If a later native
version adds classification, treat it as execution metadata rather than authorization.
Test early enumeration exit, ignored results, mapping errors, and commit failure.

## P2 — architecture, OOP, and useful design patterns

### R10. Keep a small facade and compose resource-specific objects

Use a sealed `LatticeDatabase` for opening, transaction creation, query preparation,
and explicit index administration. Put graph operations on transaction-bound objects.
Keep prepared queries and result cursors separate because they have different
lifetimes. Split search/index/stream services only when the API grows enough to
justify them; avoid a giant database class and unnecessary service indirection.

Recommended patterns and their purpose:

| Pattern | Appropriate use | Avoid |
| --- | --- | --- |
| Facade | Database entry point over native lifecycle | A second general database framework |
| Adapter | Internal translation of the pinned C ABI | Public pointer-shaped APIs |
| State machine | Transaction and cursor lifecycle | Flags permitting contradictory states |
| RAII / ownership scopes | SafeHandle plus call-scoped native allocations | Finalizers as the normal cleanup path |
| Value objects | Node/edge IDs, detached values, immutable options | Records pretending resource owners have value equality |
| Scoped callback | Read/write convenience with predictable completion | Returning a lazy object tied to a disposed transaction |
| Narrow strategy seam | Loader or fault injection where implementations differ | An interface for every concrete class |

A read transaction type and a write type sharing read behavior can prevent accidental
mutation at compile time. Prefer composition/internal helpers over a large public
transaction inheritance tree. Do not add repositories, generic units of work,
dependency-injection packages, or ORM patterns to this binding by default.

### R11. Immutable values require actual defensive ownership

The proposed records expose `IReadOnlyList`, `IReadOnlyDictionary`, and
`ReadOnlyMemory`; these do not inherently detach mutable input arrays or collections.
Define factory copying, safe accessors, equality, null/default behavior, missing
property versus explicit null, numeric conversion, and unknown native value kinds.
Do not silently turn integers into floating-point values or stringify nested values.

Bound recursive list/map depth, item counts, and copied bytes; checked-convert native
lengths before allocating. Decide how invalid UTF-8 and embedded NUL are handled:
length-prefixed values and NUL-terminated identifiers need different rules. Reject
invalid strings before truncation can change a path, query, or property name.
Snapshot builders are preferable to exposing mutable storage through public records.

### R12. Query binding and result access need usability contracts

Define whether prepared queries may be reused or used concurrently. The pinned
header has binding functions but no clear/reset-bindings API. Executing again with
fewer parameters could retain previous bindings; inspect implementation and require
complete rebinding or recreate the native query where necessary.

Preserve column order and duplicate column names. Specify whether a name lookup
throws on ambiguity, and offer ordinal access and `TryGetOrdinal`. Rows retained
after cursor advancement should be detached snapshots unless borrowing is explicitly
exposed. State whether a cursor is single-use and prevent concurrent enumeration.
Capture query diagnostics immediately before another execution clears them. Keep
query text/parameter logging opt-in and redact sensitive values by default.

### R13. Thread safety needs per-object guarantees

Multiple native readers do not imply a single transaction, query, or cursor is safe
for concurrent calls. Document database-level concurrency separately from child
objects, disposal, and separate opens of the same file. Scope synchronization to
the ownership or native constraint being protected. Use parent-handle retention
through calls; avoid naked pointer calls racing with Dispose.

The v0.10.0 header says a reader may remain active alongside a writer and a second
writer fails with LOCK_TIMEOUT. Test that actual pinned behavior and avoid broader
locking assumptions inferred from current online examples.

### R14. IDs and graph snapshots need honest semantics

Typed `ulong` IDs distinguish nodes from edges but not databases. Document their
database scope and avoid implying that an ID authorizes access or is globally unique.
Do not impose an invented invalid-ID sentinel without checking upstream.
Where operations need ownership validation, transaction-bound references can carry
an internal database identity while persisted DTOs retain plain IDs.

The pinned label getter returns comma-separated text. Test legal label syntax before
splitting blindly. Verify parallel edges and deletion cardinality before exposing
an apparently precise delete-by-ID method. Do not return partially populated node
or edge DTOs as though they were complete property snapshots.

### R15. Error translation should preserve native information without overdesign

Centralize native error translation and retain unknown numeric codes. Add dedicated
exception classes where callers can reasonably recover differently; avoid inventing
codes absent from the pin. Use normal argument/disposal exceptions for managed
precondition failures. Inspect location units before presenting query positions as
.NET character offsets: the header establishes one-based locations but not their
complete Unicode interpretation.

## P2/P3 — usefulness and operational improvements

### R16. Native loading is normally a process/assembly decision

An options-level library path may suggest each database can use a different ABI,
while static LibraryImport resolution usually binds the library for the assembly.
Choose and document one pinned native implementation per managed assembly/process
initially; reject incompatible later path choices. Load from explicit/package paths
and verify required exports and identity before accepting the library. A dynamically
selected function table is justified only if multiple native implementations become
a concrete requirement. Diagnostic APIs should distinguish unconfigured, missing,
incompatible, and successfully loaded states.

### R17. File-format compatibility is not established by a native version

[compatibility.md](compatibility.md) calls file-format compatibility an upstream
guarantee, but this repository supplies no verified guarantee or compatibility
matrix. Phrase it as an upstream contract that must be checked per supported
upgrade. Preserve old/new fixtures and test open failure without corruption. An
upgrade experiment should use copies/backups, with any migration behavior explicit.

### R18. SourceLink and public API policy need a release checkpoint

RepositoryUrl is absent, SourceLink is included only in CI, and XML documentation
warnings are suppressed. Configure the actual remote URL when supplied; validate
SourceLink in built artifacts. Consider an explicit language version and public API
baseline once real API contracts exist. Enable missing XML-doc warnings then, so
ownership and disposal requirements become part of the delivered documentation.
Do not freeze the empty placeholder API or publish it as a useful preview.

### R19. Test architecture should prove behavior at both sides of interop

Use managed tests for value conversion, state transitions, argument bounds, and
package validation. Use a small native probe for struct sizes/offsets, enum/Boolean
representation, and allocator ownership. Use the real pinned engine for graph/query
semantics, lock contention, rollback, read snapshots, and close/reopen behavior.
Use child-process crash tests for WAL recovery; disposal tests cannot prove crash
durability. Fault injection must exercise consuming close errors and partial writes.
Mocks alone cannot prove ABI correctness. Replace the assembly-name test as useful
coverage arrives; avoid measuring progress by scaffold test counts.

### R20. Make the Hetu experiment reproducible before deciding on DuckDB

Compare equivalent dataset snapshots, queries, result semantics, and hardware.
Measure retrieval relevance and completeness as well as warm/cold latency, ingest
cost, retained memory, index size, update/delete behavior, and crash recovery.
Include mixed graph/vector/text queries, representative Cypher gaps, parallel-edge
identity, missing properties, and query plans that require currently unsupported
features. Separate managed-wrapper overhead from engine time and retrieval quality.
Keep the experimental provider in Hetu and preserve a fallback until acceptance
criteria pass. Replacing one storage backend does not establish suitability for
every analytical workload currently using DuckDB.

## Recommended implementation sequence

1. Reconcile R01 against the pinned header; decide whether to retain v0.10.0 or
   deliberately choose a newer immutable revision. Record supported features.
2. Build the native library and ABI probe; establish allocator/parent lifetimes,
   close behavior, and transaction completion with focused executable tests.
3. Deliver explicit transactions, typed values, one graph operation, and a
   parameterized query with detached result values and controlled disposal.
4. Prove crash recovery and ship one verified RID through a temporary local NuGet
   feed to a clean sample consumer. Harden the package audit and manifest pipeline.
5. Expand graph/query coverage, then retrieval, then the external Hetu experiment.

The immediate value is resolving the actual native contract and proving a small
usable transaction/query path. Additional patterns and abstraction layers should
follow demonstrated ownership or usability needs.
