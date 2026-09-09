# LatticeDbSharp

## Purpose

`LatticeDbSharp` is an idiomatic, safe .NET binding for LatticeDB.

LatticeDB exposes its primary API through a native C ABI. Existing Python, TypeScript and Go bindings wrap that C API. The C interface uses opaque handles, explicit allocation/free semantics, typed values, transactions, graph operations, vector search, full-text search, Cypher queries and durable streams.

The goal of LatticeDbSharp is to provide the same capabilities to .NET applications without exposing P/Invoke, native handles, pointer management or manual memory ownership to consumers.

The initial motivation is Penghou, particularly Hetu and potentially Cangjie, but the package must remain independent of Penghou concepts.

## Goals

LatticeDbSharp should:

1. Provide a thin and predictable .NET API over the official LatticeDB C API.
2. Correctly manage all native resources using `SafeHandle`, `IDisposable` and deterministic ownership.
3. Translate native error codes into useful .NET exceptions.
4. Provide idiomatic access to transactions, nodes, edges, properties, Cypher queries, parameters, vector search and full-text search.
5. Package the required native LatticeDB binaries through NuGet where practical.
6. Allow applications to use LatticeDB without installing or locating a native library manually.
7. Preserve access to LatticeDB functionality rather than hiding it behind a custom ORM or repository abstraction.
8. Pin and test against a known upstream LatticeDB version.
9. Maintain a clear compatibility policy as the LatticeDB C ABI evolves.
10. Be usable independently by any .NET application.

## Non-goals

The first release should not implement:

* a LINQ-to-Cypher provider
* an object graph mapper or ORM
* automatic persistence of arbitrary CLR objects
* Penghou-specific graph schemas
* Hetu-specific abstractions
* Cangjie-specific memory abstractions
* automatic RAG pipelines
* automatic embedding generation through Baize
* a generic database abstraction shared with SQLite or DuckDB
* distributed access to LatticeDB
* artificial asynchronous APIs around synchronous native calls

The wrapper should expose LatticeDB cleanly rather than inventing another database framework.

## Repository and packages

Repository:

```text
latticedb-sharp
```

Primary namespace:

```text
LatticeDbSharp
```

Initial package:

```text
LatticeDbSharp
```

Suggested structure:

```text
LatticeDbSharp/
├── src/
│   └── LatticeDbSharp/
│       ├── Database/
│       ├── Transactions/
│       ├── Graph/
│       ├── Query/
│       ├── Search/
│       ├── Streams/
│       ├── Interop/
│       └── Errors/
├── tests/
│   ├── LatticeDbSharp.Tests/
│   └── LatticeDbSharp.IntegrationTests/
├── benchmarks/
│   └── LatticeDbSharp.Benchmarks/
├── samples/
│   └── LatticeDbSharp.QuickStart/
└── native/
```

`Interop` should remain internal.

Consumers should never need to reference native handles directly.

## Target framework

Initially target:

```text
net8.0
```

This provides a sufficiently modern interop baseline while retaining broad compatibility.

Tests may additionally run against newer supported .NET versions.

Use source-generated interop through `LibraryImport` where appropriate rather than legacy `DllImport`, provided it maps cleanly to the LatticeDB ABI.

## Upstream versioning

LatticeDbSharp must pin an exact upstream LatticeDB release or commit.

Do not dynamically assume compatibility with arbitrary installed LatticeDB versions.

Record the upstream version in:

* repository metadata
* NuGet package metadata
* generated native assets
* CI
* compatibility tests

The managed package version and LatticeDB native version should remain independently identifiable.

For example:

```text
LatticeDbSharp 0.1.0
LatticeDB native 0.x.y
```

The package should expose runtime version information if LatticeDB provides an appropriate native version API.

## ABI compatibility

ABI compatibility is a first-class requirement.

The current LatticeDB API uses versioned open option structures. `lattice_open_options_v2`, `v3` and `v4` extend earlier structures, and the current documentation explicitly requires matching initialization because fields such as `struct_size` and `lock` affect ABI and behavior.

LatticeDbSharp must therefore:

* map structures exactly according to `lattice.h`
* validate structure layout and field sizes
* avoid manually guessing native packing
* test native enum values
* test Boolean representation
* test pointer ownership
* test UTF-8 string handling
* test `size_t`, `uint64_t`, `uint32_t` and other platform-dependent mappings
* prefer the newest supported versioned API from the pinned LatticeDB release

Interop declarations should be generated or verified against the pinned `lattice.h` whenever practical.

## Native handles

Every native resource requiring explicit destruction must have a corresponding internal `SafeHandle`.

Expected handles include:

```text
SafeLatticeDatabaseHandle
SafeLatticeTransactionHandle
SafeLatticeQueryHandle
SafeLatticeResultHandle
SafeLatticeVectorResultHandle
SafeLatticeFtsResultHandle
SafeLatticeEdgeResultHandle
SafeLatticeStreamBatchHandle
```

Additional handles should be added as required by `lattice.h`.

The public API should never expose `IntPtr`.

## Database API

Primary object:

```csharp
using var db = LatticeDatabase.Open(
    "knowledge.lattice",
    new LatticeDatabaseOptions
    {
        Create = true,
        EnableVectors = true,
        VectorDimensions = 1536
    });
```

Suggested options:

```csharp
public sealed record LatticeDatabaseOptions
{
    public bool Create { get; init; }
    public bool ReadOnly { get; init; }
    public uint? CacheSizeMb { get; init; }
    public uint? PageSize { get; init; }
    public bool EnableVectors { get; init; }
    public ushort? VectorDimensions { get; init; }
    public bool EnableWal { get; init; } = true;
    public bool EnableAdjacencyCache { get; init; }
    public bool Lock { get; init; } = true;
}
```

Defaults should follow LatticeDB native defaults wherever possible rather than duplicating default values unnecessarily in managed code.

Support in-memory databases:

```csharp
using var db = LatticeDatabase.OpenMemory();
```

LatticeDB currently supports `:memory:` databases using normal transactions while omitting persistent files.

## Transactions

Expose explicit transactions:

```csharp
using var tx = db.BeginWrite();

var alice = tx.CreateNode("Person");
tx.SetProperty(alice.Id, "name", "Alice");

tx.Commit();
```

Also provide convenience transaction scopes:

```csharp
db.Write(tx =>
{
    var alice = tx.CreateNode("Person");
    tx.SetProperty(alice.Id, "name", "Alice");
});
```

and:

```csharp
var result = db.Read(tx =>
{
    return tx.GetNode(nodeId);
});
```

Behavior:

* successful `Write` delegate execution commits
* exceptions cause rollback
* disposing an uncommitted write transaction rolls it back
* read transactions complete safely on disposal
* calling `Commit` or `Rollback` twice should fail predictably or become a documented no-op

LatticeDB supports multiple concurrent readers but only one active write transaction. A second writer currently fails rather than transparently queueing.

LatticeDbSharp should preserve this semantic rather than silently introduce hidden global locking.

A convenience writer serialization mechanism may be considered later, but it must be opt-in.

## Graph model

Use strongly typed identifiers:

```csharp
public readonly record struct LatticeNodeId(ulong Value);

public readonly record struct LatticeEdgeId(ulong Value);
```

Suggested graph records:

```csharp
public sealed record LatticeNode(
    LatticeNodeId Id,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, LatticeValue> Properties);

public sealed record LatticeEdge(
    LatticeEdgeId Id,
    LatticeNodeId Source,
    LatticeNodeId Target,
    string Type,
    IReadOnlyDictionary<string, LatticeValue> Properties);
```

Node operations should include:

```text
CreateNode
DeleteNode
NodeExists
AddLabel
RemoveLabel
GetLabels
SetProperty
GetProperty
SetVector
```

The v0.15.0 C API does not export direct node-property removal or vector-read
functions. A managed operation may be added only after a parameterized Cypher
implementation and its semantics are verified; it must not bind an invented C
symbol.

Edge operations should include:

```text
CreateEdge
DeleteEdges(source, target, type)
SetEdgeProperty
GetEdgeProperty
RemoveEdgeProperty
GetOutgoingEdges
GetIncomingEdges
GetOutgoingEdgesByType
GetIncomingEdgesByType
```

Stable native node and edge IDs should be preserved when returned by the API.
The v0.15.0 deletion function identifies edges by source, target, and type, not
by stable edge ID. The wrapper must make that multiplicity explicit and must not
imply single-edge deletion until upstream semantics are proven.

## Value model

LatticeDB currently exposes null, Boolean, integer, floating-point, string, bytes, vector, list and map value types through the C API.

Create an explicit managed discriminated value representation rather than exposing `object` everywhere.

Example:

```csharp
public readonly struct LatticeValue
{
    public LatticeValueType Type { get; }

    public bool AsBoolean();
    public long AsInt64();
    public double AsDouble();
    public string AsString();
    public ReadOnlyMemory<byte> AsBytes();
    public ReadOnlyMemory<float> AsVector();
    public IReadOnlyList<LatticeValue> AsList();
    public IReadOnlyDictionary<string, LatticeValue> AsMap();
}
```

Provide implicit or factory conversions for common CLR values:

```csharp
LatticeValue.From("Alice");
LatticeValue.From(42L);
LatticeValue.From(3.14);
LatticeValue.From(true);
LatticeValue.Null;
```

Do not silently perform lossy conversions.

Native memory returned by LatticeDB must be copied before the native owner is released unless the API explicitly guarantees a longer lifetime.

## Cypher queries

Cypher should be a first-class feature.

Example:

```csharp
var rows = db.Query(
    """
    MATCH (m:Method)-[:CALLS]->(d:Method)
    WHERE m.name = $name
    RETURN d.name
    """,
    new LatticeParameters
    {
        ["name"] = "RunAsync"
    });
```

The C API follows a prepare, bind, execute pattern and can determine whether a prepared query performs writes.

LatticeDbSharp may use this capability for a convenience API only when commit
timing is unambiguous. The core API executes queries inside explicit read or
write transactions. A future:

```csharp
db.Query(...)
```

may automatically select a read or write transaction, but it must fully
materialize detached results and commit a write before returning. It must never
return lazy results tied to an already-disposed automatic transaction.

Also expose prepared queries:

```csharp
using var query = db.Prepare(
    "MATCH (n:Method) WHERE n.name = $name RETURN n");

var rows = query.Execute(new()
{
    ["name"] = "RunAsync"
});
```

Prepared queries are important for repeated operations and should not be hidden.

## Query parameters

Support at minimum:

```text
null
bool
long
double
string
byte[]
float[] / ReadOnlyMemory<float>
```

Add list and map parameter support if the underlying C API supports them for query binding in the pinned version.

Do not concatenate user values into Cypher strings when parameter binding is available.

## Query results

Avoid returning `Dictionary<string, object?>`.

Use:

```csharp
public sealed class LatticeRow
{
    public int Count { get; }

    public string GetName(int ordinal);
    public LatticeValue GetValue(int ordinal);
    public LatticeValue this[string name] { get; }
}
```

Potential result API:

```csharp
foreach (var row in db.Query(...))
{
    var name = row["name"].AsString();
}
```

Result enumeration must keep the underlying native result alive until enumeration completes.

Disposing the result should deterministically free native memory.

## Query diagnostics

LatticeDB query errors expose considerably more information than a single error code, including:

```text
error code
human-readable message
query processing stage
line
column
length
```

Stages include parse, semantic, planning and execution failures.

Represent these through:

```csharp
public sealed class LatticeQueryException : LatticeException
{
    public string? QueryErrorCode { get; }
    public LatticeQueryStage Stage { get; }
    public int? Line { get; }
    public int? Column { get; }
    public int? Length { get; }
}
```

The original Cypher query may be included where safe and useful.

## Error handling

Create:

```csharp
LatticeException
LatticeIOException
LatticeCorruptionException
LatticeNotFoundException
LatticeAlreadyExistsException
LatticeInvalidArgumentException
LatticeTransactionAbortedException
LatticeLockTimeoutException
LatticeReadOnlyException
LatticeDatabaseFullException
LatticeVersionMismatchException
LatticeChecksumException
LatticeOutOfMemoryException
LatticeUnsupportedException
LatticeValueTooLargeException
LatticeDatabaseLockedException
LatticeQueryException
```

Preserve the original native error code on the base exception.

Do not collapse all errors into `InvalidOperationException`.

## Vector support

Expose native vector operations without adding an embedding provider abstraction.
The pinned v0.15.0 engine stores one configured vector per node; its C-API
`key` parameter is currently ignored, so the managed API does not expose a
misleading multi-vector namespace.

Example:

```csharp
tx.SetVector(nodeId, embedding);

var matches = db.VectorSearch(
    embedding,
    k: 20,
    efSearch: 64);
```

Return:

```csharp
public readonly record struct LatticeVectorMatch(
    LatticeNodeId NodeId,
    float Distance);
```

Support bulk vector insertion if exposed by the pinned C API.

Embedding generation is explicitly outside the core wrapper. Penghou can use Baize or another component to generate vectors before passing them to LatticeDbSharp.

## Full-text search

Expose creation and deletion of per-property FTS indexes.

Example:

```csharp
db.CreateNodeFullTextIndex("Document", "content");

var results = db.FullTextSearch(
    label: "Document",
    property: "content",
    query: "workflow cancellation",
    limit: 20);
```

Support fuzzy search separately:

```csharp
db.FuzzyFullTextSearch(...);
```

Do not hide BM25 scores.

## Property indexes

Expose explicit property index management:

```csharp
db.CreateNodePropertyIndex("Method", "fullName");
db.DropNodePropertyIndex("Method", "fullName");

db.CreateEdgePropertyIndex("CALLS", "kind");
```

Indexed lookup should preserve upstream semantics rather than silently falling back to scans.

## Durable streams

Durable streams and the built-in graph changefeed should be supported, but they may land after the graph/query/search core if this reduces the first milestone.

LatticeDB streams provide named durable records, sequence numbers, consumer offsets and trimming. The built-in `__lattice_changes` stream exposes committed graph mutations. They are local durable logs, not distributed queues and do not provide leases, retries or dead-letter behavior.

Potential API:

```csharp
tx.Publish(
    "events",
    kind: "document.updated",
    payload);

var events = db.ReadStream(
    "events",
    afterSequence: 120,
    limit: 100);

db.SetStreamOffset(
    "events",
    "indexer",
    sequence);
```

Graph changes:

```csharp
var changes = db.ReadChanges(
    afterSequence: sequence);
```

Do not attempt to turn this into a message-broker abstraction.

## Async policy

Do not create fake asynchronous APIs solely by wrapping native calls in `Task.Run`.

The native LatticeDB API is fundamentally synchronous.

The initial API should therefore be synchronous.

Introduce asynchronous methods only where LatticeDB itself exposes a genuinely blocking or asynchronous operation, or where a future native cancellation/wait primitive justifies it.

This keeps API semantics honest and avoids unnecessary thread-pool use in ASP.NET and other server environments.

## Cancellation

Do not claim `CancellationToken` support for operations that cannot actually interrupt native execution.

If the native API later exposes query cancellation, waiting cancellation or interruption, map it explicitly.

## Native library loading

Native loading should be automatic.

Use an internal resolver based on:

```text
NativeLibrary
AssemblyLoadContext where necessary
RID-specific runtime assets
```

Users should normally be able to:

```text
dotnet add package LatticeDbSharp
```

and immediately open a database.

Provide a diagnostic API such as:

```csharp
LatticeNative.IsAvailable
LatticeNative.Version
LatticeNative.LibraryPath
```

Failures should provide useful information about:

```text
requested RID
expected library name
probing paths
architecture
upstream native version
```

## Native packaging

Target RID-specific native assets using standard NuGet layout:

```text
runtimes/linux-x64/native/
runtimes/linux-arm64/native/
runtimes/osx-x64/native/
runtimes/osx-arm64/native/
runtimes/win-x64/native/
runtimes/win-arm64/native/
```

Only publish a RID after it passes integration tests.

Current official LatticeDB installation documentation describes `.so` for Linux
and `.dylib` for macOS, while the upstream release workflow does not publish a
Windows binary. LatticeDbSharp nevertheless builds the pinned source unchanged
with Zig for `x86_64-windows-gnu` and packages the resulting `lattice.dll`.

The Windows x64 feasibility gate is complete:

1. LatticeDB successfully builds for Windows.
2. The C ABI loads correctly from .NET.
3. file creation, locking, transactions and WAL recovery work.
4. integration tests pass.
5. native assets can be packaged reproducibly.

Windows x64 is therefore an advertised LatticeDbSharp preview runtime. Other
Windows architectures remain unsupported until they pass the same gate.

## Native build

LatticeDB is implemented in Zig and its documented shared-library build is:

```text
zig build shared
```

The upstream project currently documents Zig 0.16.0 for its CI and release workflow.

CI should build native assets from a pinned source revision rather than pulling an uncontrolled binary.

Where upstream publishes verifiable release binaries, consuming those artifacts may later become preferable.

## Testing

### Interop tests

Verify:

```text
struct layout
enum values
pointer sizes
string encoding
memory ownership
SafeHandle release
native error mapping
value conversion
native version compatibility
```

### Functional tests

Test:

```text
database create/open/close
in-memory databases
read-only mode
node CRUD
edge CRUD
properties
labels
graph traversal
transactions
rollback
multiple concurrent readers
single-writer behavior
Cypher reads
Cypher writes
prepared queries
parameter binding
query error locations
vector insertion
vector search
FTS indexes
FTS search
fuzzy FTS
property indexes
batch insertion
streams
graph changefeed
consumer offsets
```

### Recovery tests

At minimum validate:

```text
close/reopen persistence
committed data survives restart
rolled-back data is absent
WAL recovery behavior
database locking
read-only locking behavior
```

### Memory tests

Run repeated create/query/dispose cycles and monitor native memory usage.

Particular attention should be given to:

```text
query results
returned strings
value buffers
vector results
FTS results
edge results
stream batches
```

because the C API explicitly requires callers to free several classes of returned native data.

## Benchmarks

BenchmarkDotNet should measure wrapper overhead separately from database performance.

Initial benchmarks:

```text
database open
node insert
edge insert
property set/get
graph traversal
prepared query
parameterized query
vector search
FTS search
bulk ingest
managed/native allocation rate
```

The objective is not to reproduce LatticeDB's published benchmarks.

The objective is to ensure the .NET binding does not introduce pathological overhead.

## Documentation

README should contain:

1. installation
2. five-minute quick start
3. graph CRUD
4. Cypher querying
5. parameter binding
6. vector search
7. full-text search
8. transactions
9. streams
10. native platform support
11. upstream version compatibility
12. limitations

The API should be usable without understanding P/Invoke.

## Phase 0: feasibility

Before building the complete API:

1. Pin an upstream LatticeDB version.
2. Build the shared library.
3. Verify Linux x64 and Windows x64.
4. Verify macOS ARM64.
5. Open a database from .NET.
6. Create two nodes and an edge.
7. Commit.
8. Query through Cypher.
9. Close and reopen.
10. Validate persisted data.
11. Verify native resource cleanup.

The Windows x64 build is isolated in the pinned Zig/toolchain gate and does not
modify or fork upstream source.

Do not fork LatticeDB unless genuinely necessary.

## Phase 1: core binding

Implement:

```text
database lifecycle
native error mapping
SafeHandle ownership
transactions
LatticeValue
nodes
edges
properties
traversal
Cypher
parameters
query results
query diagnostics
```

This is the minimum useful release.

Target:

```text
0.1.0
```

## Phase 2: retrieval capabilities

Add:

```text
vector storage
vector search
batch vector insertion
FTS indexes
BM25 search
fuzzy FTS
property indexes
```

Target:

```text
0.2.0
```

This should make the package sufficient for a real Hetu experiment.

## Phase 3: durable event capabilities

Add:

```text
durable streams
consumer offsets
stream trimming
graph changefeed
```

Target:

```text
0.3.0
```

## Phase 4: hardening

Add:

```text
cross-platform native packaging
native compatibility matrix
benchmarks
recovery tests
memory stress tests
API polish
XML documentation
NuGet release automation
```

A stable `1.0` should wait until both the API shape and upstream ABI integration have proven themselves in real usage.

## Hetu validation spike

After Phase 2, create a separate experimental integration in Hetu.

Do not add Hetu concepts to LatticeDbSharp.

Load a representative repository as a graph containing:

```text
Repository
Project
Namespace
Type
Method
Interface
Test
Document
Decision
```

with relationships such as:

```text
CONTAINS
DECLARES
CALLS
IMPLEMENTS
REFERENCES
TESTED_BY
GOVERNED_BY
```

Attach source text and embeddings where appropriate.

Evaluate queries such as:

```text
Find code semantically related to workflow cancellation.

Traverse dependencies from WorkflowEngine.RunAsync.

Find tests connected to code affected by lease fencing.

Find architectural documentation associated with the affected components.

Construct the smallest useful context set for changing a workflow behavior.
```

The success criterion is not merely that LatticeDbSharp works.

The spike should determine whether combining graph traversal, semantic search and full-text retrieval materially improves Hetu's context selection.

## Acceptance criteria for 0.1

`LatticeDbSharp 0.1.0` is complete when:

* no public API exposes raw native pointers
* all owned native resources have deterministic cleanup
* database creation and reopening work
* in-memory databases work
* read and write transactions work
* rollback is verified
* node and edge CRUD work
* property values round-trip correctly
* graph traversal works
* Cypher queries work
* parameters work
* query diagnostics preserve native location information
* native errors map predictably to .NET exceptions
* unsupported native platforms fail with actionable diagnostics
* CI executes integration tests against the pinned native library
* the README contains a working quick start
* a sample application runs without requiring consumers to write native interop code

## Architectural principle

LatticeDbSharp is a binding, not a framework.

Its responsibility is:

```text
LatticeDB C API
        ↓
safe native interop
        ↓
idiomatic .NET types
        ↓
LatticeDbSharp
```

Domain-specific behavior remains above it:

```text
                  Hetu
                    │
                 Cangjie
                    │
              other products
                    │
             LatticeDbSharp
                    │
             LatticeDB C API
                    │
                LatticeDB
```

This boundary lets Penghou experiment with LatticeDB without coupling its architecture directly to native interop and simultaneously creates a generally useful .NET package that can evolve independently.
