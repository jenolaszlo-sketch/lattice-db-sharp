# LatticeDbSharp.Extensions.AI
## Microsoft .NET AI Integration for LatticeDB

**Status:** Proposed
**Package:** `LatticeDbSharp.Extensions.AI`

---

# 1. Summary

Create `LatticeDbSharp.Extensions.AI`, an optional integration package that makes LatticeDB usable through the standard Microsoft .NET AI abstractions.

The initial package will integrate LatticeDB with:

* `Microsoft.Extensions.AI`
* `Microsoft.Extensions.VectorData`

This should make LatticeDB usable by higher-level Microsoft AI components without taking dependencies on those higher-level frameworks.

The primary architecture is:

```text
                  Microsoft Agent Framework
                           |
                  Microsoft.Extensions.DataIngestion
                           |
                           v
                Microsoft.Extensions.VectorData
                           |
                           v
              LatticeDbSharp.Extensions.AI
                           |
                           v
                    LatticeDbSharp
                           |
                           v
                       LatticeDB
```

Embedding generation remains external:

```text
OpenAI / Azure / Ollama / Baize / custom provider
                       |
                       v
 IEmbeddingGenerator<string, Embedding<float>>
                       |
                       v
              LatticeVectorStore
                       |
                       v
                    LatticeDB
```

The package must not use `lattice_embedding_client_*` as its normal embedding path.

---

# 2. Design Principle

The responsibility split is:

```text
Microsoft.Extensions.AI
    model interaction and embedding generation

Microsoft.Extensions.VectorData
    portable vector-store contract

LatticeDbSharp.Extensions.AI
    Microsoft abstraction -> LatticeDB adaptation

LatticeDbSharp
    database and native API wrapper

LatticeDB
    persistence, vector search, BM25 and graph operations

Application
    retrieval policy, meaning, provenance and orchestration
```

The central rule is:

> LatticeDB stores and retrieves knowledge. AI providers create embeddings. Applications decide what retrieved information means.

---

# 3. Goals

The package should:

* implement the Microsoft `VectorStore` abstraction
* implement typed `VectorStoreCollection<TKey,TRecord>` collections
* support Microsoft vector data model attributes
* support programmatic `VectorStoreCollectionDefinition`
* support dynamic record models where practical
* use `IEmbeddingGenerator<string, Embedding<float>>`
* support automatic embedding generation
* support native LatticeDB HNSW vector search
* support metadata filtering where LatticeDB can execute it correctly
* support BM25 full-text indexing
* implement Microsoft hybrid search where semantics can be mapped correctly
* integrate naturally with dependency injection
* preserve cancellation wherever the underlying operations permit it
* expose native LatticeDB capabilities through `GetService`
* work without Agent Framework
* work without Semantic Kernel
* work without Baize
* work without any particular model provider

---

# 4. Non-goals

The package must not:

* implement an agent runtime
* implement an orchestration framework
* implement chat completion
* implement model routing
* implement credentials
* implement retry policies for AI providers
* implement provider-specific OpenAI, Azure, Gemini or Ollama clients
* depend on Baize
* depend on Microsoft Agent Framework
* depend on Semantic Kernel
* replace `Microsoft.Extensions.AI` abstractions
* invent another embedding interface
* use LatticeDB's native HTTP embedding client for normal operation
* treat similarity scores as authoritative evidence
* hide graph operations behind misleading generic vector-store abstractions
* silently modify vector dimensions
* silently convert incompatible distance functions

---

# 5. Package Dependencies

Prefer abstraction-only dependencies.

Required:

```text
LatticeDbSharp

Microsoft.Extensions.AI.Abstractions
Microsoft.Extensions.VectorData.Abstractions
```

Add non-abstraction Microsoft packages only when required by actual API implementation.

Do not depend on:

```text
Microsoft.Agents.AI
Microsoft.SemanticKernel
OpenAI
Azure.AI.OpenAI
OllamaSharp
Baize
```

Those should be consumers of this package, not dependencies.

Version selection should follow the repository's supported target frameworks and current Microsoft package compatibility.

---

# 6. Primary Public Types

The package should initially expose:

```csharp
LatticeVectorStore
LatticeVectorStoreOptions

LatticeVectorStoreCollection<TKey,TRecord>
LatticeVectorStoreCollectionOptions
```

Potential later additions:

```csharp
LatticeHybridSearchOptions
LatticeGraphRetrievalOptions
LatticeGraphSearchResult<TRecord>
```

Do not expose implementation-specific mapping classes unless consumers need them.

---

# 7. LatticeVectorStore

Implement:

```csharp
public sealed class LatticeVectorStore : VectorStore
{
}
```

The store represents an existing LatticeDB database.

Example construction:

```csharp
var store = new LatticeVectorStore(
    database,
    new LatticeVectorStoreOptions
    {
        EmbeddingGenerator = embeddingGenerator
    });
```

Where:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
```

is the standard Microsoft embedding abstraction.

---

# 8. LatticeVectorStoreOptions

Initial conceptual API:

```csharp
public sealed class LatticeVectorStoreOptions
{
    public IEmbeddingGenerator<string, Embedding<float>>?
        EmbeddingGenerator { get; init; }

    public bool OwnsDatabase { get; init; }

    public bool ValidateDimensions { get; init; } = true;
}
```

Additional options may be added only when required by implementation.

Avoid exposing native tuning knobs prematurely.

LatticeDB-specific HNSW parameters may later belong in collection/vector configuration rather than global store options.

---

# 9. Resource Ownership

The caller owns the supplied `LatticeDatabase` by default.

```csharp
OwnsDatabase = false;
```

If `OwnsDatabase` is enabled, disposing `LatticeVectorStore` may dispose the database.

The embedding generator must not be disposed by the vector store unless explicitly created or owned by the vector store.

Default behavior:

```text
LatticeDatabase supplied by caller      -> caller owns
IEmbeddingGenerator supplied by caller  -> caller owns
LatticeVectorStore                      -> owns adapters only
```

---

# 10. VectorStore Contract

`LatticeVectorStore` should implement the current `VectorStore` contract, including equivalent support for:

```text
GetCollection<TKey,TRecord>
GetDynamicCollection
ListCollectionNamesAsync
EnsureCollectionDeletedAsync
GetService
```

Unsupported operations must fail explicitly.

Do not silently return incomplete behavior.

---

# 11. Collection Model

Implement:

```csharp
public sealed class LatticeVectorStoreCollection<TKey,TRecord>
    : VectorStoreCollection<TKey,TRecord>
    where TRecord : class
{
}
```

The collection must support the standard VectorData lifecycle:

```text
EnsureCollectionExistsAsync
EnsureCollectionDeletedAsync

UpsertAsync
GetAsync
DeleteAsync

SearchAsync
```

Batch operations should be supported where the Microsoft abstraction exposes them and where LatticeDB can execute them efficiently.

---

# 12. Collection Storage Mapping

The public API must not expose assumptions about how a VectorData collection is physically encoded in LatticeDB.

Internally, create a dedicated collection mapping abstraction.

Example conceptual component:

```text
LatticeCollectionStorageMapper
```

It maps:

```text
VectorData collection
        |
        v
LatticeDB storage representation
```

Preferred mapping order:

1. Use a native LatticeDB collection or namespace concept if one exists.
2. Otherwise use a stable reserved node-label or equivalent namespace.
3. Otherwise use reserved internal metadata identifying the collection.

The storage encoding must:

* be deterministic
* avoid collisions
* preserve collection names
* allow collection enumeration
* allow collection deletion
* not leak into application records
* be versionable

Use a reserved prefix for implementation metadata.

For example:

```text
__latticedbsharp_ai_*
```

The exact prefix should follow existing LatticeDbSharp conventions.

---

# 13. Data Models

Support both VectorData schema mechanisms.

## 13.1 Attributes

Support:

```csharp
[VectorStoreKey]

[VectorStoreData]
[VectorStoreData(IsIndexed = true)]
[VectorStoreData(IsFullTextIndexed = true)]

[VectorStoreVector(...)]
```

Example:

```csharp
public sealed class MemoryRecord
{
    [VectorStoreKey]
    public required string Id { get; init; }

    [VectorStoreData(IsIndexed = true)]
    public required string SessionId { get; init; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public required string Content { get; init; }

    [VectorStoreVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity,
        IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; init; }
}
```

## 13.2 Programmatic definitions

Support:

```csharp
VectorStoreCollectionDefinition
```

Example:

```csharp
var definition = new VectorStoreCollectionDefinition
{
    Properties =
    [
        new VectorStoreKeyProperty("Id", typeof(string)),

        new VectorStoreDataProperty(
            "Content",
            typeof(string))
        {
            IsFullTextIndexed = true
        },

        new VectorStoreVectorProperty(
            "Embedding",
            typeof(ReadOnlyMemory<float>),
            dimensions: 1536)
    ]
};
```

Programmatic schema definitions should take precedence over inferred attribute configuration when the VectorData contract requires this.

---

# 14. Dynamic Collections

Support:

```csharp
GetDynamicCollection(...)
```

where feasible.

The expected record shape is:

```csharp
Dictionary<string, object?>
```

Dynamic collections must require an explicit:

```csharp
VectorStoreCollectionDefinition
```

Do not attempt to infer arbitrary dictionary schema.

If dynamic mapping proves disproportionately complex, it may move to milestone 1.1, but the architecture must not prevent it.

---

# 15. Key Support

Investigate LatticeDB's existing key capabilities and support all safe mappings.

Initial desirable key types:

```text
string
Guid
int
long
uint
ulong
```

If LatticeDB internally requires string identifiers, use a stable reversible encoding.

Never rely on culture-sensitive string conversion.

For example:

```text
Guid -> invariant canonical representation
integer -> invariant decimal representation
string -> escaped/preserved directly
```

Unsupported key types must produce a clear schema validation error.

---

# 16. Data Property Mapping

`VectorStoreData` properties should map to LatticeDB properties.

Initial desirable CLR types:

```text
string
bool

byte
short
int
long

float
double
decimal where lossless mapping exists

Guid

DateTime
DateTimeOffset

arrays / supported primitive collections
nullable variants
```

Do not serialize arbitrary CLR objects into opaque JSON merely to claim compatibility.

Unsupported types should fail during collection schema validation.

Additional serialization may be introduced later through explicit configuration.

---

# 17. Indexed Data Properties

Map:

```csharp
[VectorStoreData(IsIndexed = true)]
```

to the most appropriate LatticeDB property/index mechanism.

Filters over indexed fields should execute natively whenever possible.

The implementation must document which filterable CLR types are supported.

---

# 18. Full-Text Indexed Properties

Map:

```csharp
[VectorStoreData(IsFullTextIndexed = true)]
```

to LatticeDB BM25/full-text indexing.

This is important because LatticeDB provides both:

```text
vector similarity
+
BM25 lexical retrieval
```

inside the same storage engine.

Full-text indexing must not be simulated in managed code if LatticeDB can execute it natively.

---

# 19. Vector Properties

Map:

```csharp
[VectorStoreVector(...)]
```

to LatticeDB's existing vector storage and HNSW capabilities.

Supported initial vector type:

```csharp
ReadOnlyMemory<float>
```

Potentially support:

```csharp
float[]
```

through mapping where allowed by Microsoft VectorData.

Do not introduce unnecessary internal vector copies.

---

# 20. Distance Functions

Map Microsoft distance-function semantics to native LatticeDB semantics.

At minimum investigate:

```text
CosineSimilarity
CosineDistance
DotProductSimilarity
EuclideanDistance
```

Only advertise functions whose ordering and score semantics can be reproduced correctly.

Unsupported distance functions must fail collection creation.

Never silently substitute cosine for another function.

---

# 21. Index Kinds

Map:

```text
IndexKind.Hnsw
```

to native LatticeDB HNSW.

If the Microsoft API supports other index kinds and LatticeDB does not, reject them explicitly.

Do not claim support for index kinds merely because queries can technically execute without one.

---

# 22. Embedding Generation

Do not create a LatticeDbSharp-specific embedding provider abstraction.

Use:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
```

directly.

Example:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
    embeddingGenerator = ...;

var store = new LatticeVectorStore(
    database,
    new()
    {
        EmbeddingGenerator = embeddings
    });
```

The embedding generator may come from:

```text
OpenAI
Azure OpenAI
Ollama
Baize adapter
local model
custom implementation
```

LatticeDbSharp must not care which.

---

# 23. Automatic Embedding Generation

Support Microsoft VectorData's automatic embedding behavior.

The desired flow is:

```text
source text
    |
    v
IEmbeddingGenerator<string, Embedding<float>>
    |
    v
ReadOnlyMemory<float>
    |
    v
LatticeDB vector field
```

Applications should therefore be able to supply source text to the VectorData APIs when the configured schema and Microsoft abstraction permit automatic embedding generation.

Embedding generation must preserve:

* cancellation
* provider exceptions
* Microsoft pipeline middleware
* caching
* OpenTelemetry
* provider-specific metadata

Do not bypass the supplied generator.

---

# 24. Baize Compatibility

Baize integration belongs outside this package.

Baize may implement or adapt to:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
```

which gives:

```text
Baize
  |
  v
Microsoft.Extensions.AI
  |
  v
LatticeDbSharp.Extensions.AI
```

No direct reference from LatticeDbSharp to Baize should exist.

---

# 25. Native Lattice Embedding Client

The managed VectorData provider must not use:

```text
lattice_embedding_client_*
```

Those APIs may remain:

* unbound
* available only in low-level native bindings
* exposed separately for ABI completeness

They must not participate in the recommended Microsoft AI integration.

---

# 26. Vector Search

Implement:

```csharp
SearchAsync<TInput>(
    TInput searchValue,
    int top,
    VectorSearchOptions<TRecord>? options,
    CancellationToken cancellationToken)
```

Support at least:

```text
ReadOnlyMemory<float>
string when an embedding generator is configured
```

Potentially support other input types only where `IEmbeddingGenerator<TInput,...>` support is deliberately added later.

---

# 27. Search Execution

For vector input:

```text
query vector
    |
    v
native LatticeDB HNSW search
    |
    v
VectorSearchResult<TRecord>
```

For string input:

```text
query string
    |
    v
IEmbeddingGenerator
    |
    v
query vector
    |
    v
native LatticeDB HNSW search
    |
    v
VectorSearchResult<TRecord>
```

No LLM should participate in this flow.

---

# 28. Search Scores

Similarity score semantics must be documented.

The provider must ensure that:

```text
higher score vs lower score
distance vs similarity
normalization
```

match the expectations of Microsoft VectorData.

If LatticeDB exposes distance while VectorData expects similarity, perform an explicit documented conversion where mathematically valid.

Do not expose a misleading score.

---

# 29. Search Filters

Implement VectorData filtering over supported indexed properties.

Translate supported expression trees into native LatticeDB predicates.

Support should initially focus on operations that can be translated safely:

```text
equality
inequality

< <= > >=

logical AND
logical OR
logical NOT

supported string equality
supported collection membership
```

Exact supported expressions should be determined by the current VectorData API and LatticeDB query capabilities.

Unsupported expressions must throw:

```text
NotSupportedException
```

or the provider-standard VectorData exception.

Never fetch an unbounded candidate set and silently filter everything in memory.

Managed post-filtering is acceptable only when:

* explicitly bounded
* documented
* semantically safe

---

# 30. Hybrid Search

Implement:

```csharp
IKeywordHybridSearchable<TRecord>
```

when a collection has:

```text
a vector field
+
one or more full-text indexed fields
```

Expected flow:

```text
                    query
                   /     \
                  /       \
                 v         v
             embedding    BM25
                 |         |
                 v         v
              HNSW      lexical
                 \         /
                  \       /
                   v     v
                score fusion
                     |
                     v
             ranked result set
```

---

# 31. Hybrid Search Score Fusion

Do not combine cosine and BM25 scores by naïvely adding them.

Investigate what score data LatticeDB exposes.

Preferred order:

1. Use a native LatticeDB hybrid ranking operation if it has well-defined semantics.
2. Use Reciprocal Rank Fusion if independent ranked lists can be retrieved.
3. Introduce another explicit documented fusion algorithm only if necessary.

Default recommended fallback:

```text
Reciprocal Rank Fusion
```

because it combines rankings without assuming directly comparable score scales.

Fusion must be deterministic.

---

# 32. Hybrid Search Inputs

The VectorData hybrid API should support:

```text
embedding/search value
+
keyword collection
+
top
+
filters
```

Example conceptual usage:

```csharp
var hybrid =
    (IKeywordHybridSearchable<MemoryRecord>)collection;

await foreach (var result in hybrid.HybridSearchAsync(
    "workflow mutation strategy",
    ["workflow", "mutation", "strategy"],
    top: 10))
{
    ...
}
```

If an embedding generator is configured, a string search value should be embedded through the standard generator.

---

# 33. Multiple Full-Text Fields

If multiple properties are:

```csharp
IsFullTextIndexed = true
```

the provider should support searching them according to Microsoft VectorData hybrid-search options.

If the Microsoft contract leaves weighting provider-specific, expose optional LatticeDB-specific configuration later.

Do not invent field weights in milestone 1 unless needed.

---

# 34. Collection Creation

`EnsureCollectionExistsAsync` should validate the complete schema before modifying the database.

Validation should include:

* exactly one valid key
* supported key type
* supported data property types
* vector dimensions
* distance function
* index kind
* duplicate storage names
* valid full-text fields
* embedding configuration consistency

Fail before partially creating indexes when possible.

---

# 35. Dimension Validation

Vector dimensions must be explicit in the VectorData schema.

On writes:

```text
expected dimension != actual dimension
```

must fail clearly.

Example:

```text
Expected vector dimension 1536 for 'Embedding',
but received 768.
```

Never:

```text
truncate
pad
repeat
normalize dimensions automatically
```

---

# 36. Upsert

Implement single and batch upsert according to VectorData abstractions.

Logical operation:

```text
TRecord
   |
   v
schema mapper
   |
   +--> key
   +--> data properties
   +--> full-text properties
   +--> vector properties
   |
   v
LatticeDB record
```

If automatic embedding generation is required:

```text
TRecord
   |
   v
extract source text
   |
   v
IEmbeddingGenerator
   |
   v
vector
   |
   v
native write
```

Batch embedding should use the embedding generator's batch API where possible.

Do not invoke the provider once per record if it supports generating the entire batch.

---

# 37. Retrieval

Implement:

```text
GetAsync(key)
GetAsync(keys)
filtered GetAsync where supported
```

Retrieval should reconstruct `TRecord` without requiring AI calls.

By default, preserve vector properties according to `RecordRetrievalOptions`.

Avoid loading large vectors when the caller explicitly excludes them.

---

# 38. Delete

Support:

```text
delete record
delete records
delete collection
```

Collection deletion must remove:

* records
* vector indexes
* full-text indexes
* provider metadata

without affecting other collections.

---

# 39. Collection Enumeration

Implement:

```csharp
ListCollectionNamesAsync(...)
```

using the internal collection metadata model.

Do not infer collections by scanning arbitrary application node labels if that could produce false positives.

Collections created outside this provider should only appear when they can be safely interpreted as compatible VectorData collections.

---

# 40. GetService Integration

Implement:

```csharp
GetService(Type serviceType, object? serviceKey)
```

to allow advanced consumers to reach appropriate native services.

Likely useful services include:

```text
LatticeDatabase
LatticeVectorStore
LatticeVectorStoreCollection<TKey,TRecord>
```

Do not expose internal schema-mapping implementation classes through `GetService`.

This gives advanced applications access to graph functionality without polluting the portable VectorData contract.

---

# 41. Graph Access

Graph functionality is a LatticeDB differentiator, but it should not be forced into Microsoft VectorData abstractions.

Portable path:

```csharp
VectorStoreCollection<TKey,TRecord>
```

Native path:

```csharp
var lattice = collection.GetService<LatticeDatabase>();
```

Conceptually:

```text
Agent/RAG
   |
   v
VectorData search
   |
   v
matching records
   |
   +---- optional native graph expansion ----+
                                             |
                                             v
                                  related graph context
```

---

# 42. Graph Retrieval Extensions

A later milestone may add explicit LatticeDB-specific retrieval helpers.

Possible API:

```csharp
SearchGraphAsync(...)
```

or:

```csharp
RetrieveContextAsync(...)
```

Example concept:

```csharp
var context = await collection.RetrieveContextAsync(
    "Why was Temporal rejected?",
    new LatticeGraphRetrievalOptions
    {
        TopK = 5,
        MaxTraversalDepth = 2
    },
    cancellationToken);
```

Logical flow:

```text
query
  |
  v
vector / hybrid search
  |
  v
seed records
  |
  v
graph traversal
  |
  v
related records
  |
  v
context result
```

This is explicitly not milestone 1.

---

# 43. GraphRAG Boundary

The package may provide retrieval primitives.

It must not:

```text
prompt an LLM
summarize graph results
decide what evidence is true
generate final answers
orchestrate an agent
```

Graph retrieval should return structured data.

Interpretation belongs to the caller.

---

# 44. Microsoft.Extensions.DataIngestion Compatibility

Do not create DataIngestion-specific storage APIs unless necessary.

A correct VectorData provider should allow Microsoft's existing:

```text
VectorStoreWriter<T>
IngestionPipeline<T>
```

to work against LatticeDB.

Desired architecture:

```text
Reader
  |
Chunker
  |
Enricher
  |
VectorStoreWriter<T>
  |
LatticeVectorStore
  |
LatticeDB
```

Add an integration test proving this works.

If no additional implementation is needed, do not add a DataIngestion dependency to the package.

---

# 45. Agent Framework Compatibility

Do not reference Microsoft Agent Framework from this package.

Instead, compatibility should emerge naturally:

```text
ChatHistoryMemoryProvider
        |
        v
     VectorStore
        |
        v
 LatticeVectorStore
```

This should allow Agent Framework to use LatticeDB for persistent semantic memory without an Agent Framework-specific adapter.

Add a sample project or integration-test project separately.

---

# 46. Future Agent Framework Package

If direct Agent Framework integration becomes useful, create a separate package:

```text
LatticeDbSharp.Extensions.AgentFramework
```

Potential features:

```text
LatticeAgentSessionStore
LatticeChatHistoryProvider
Agent DI registration helpers
agent session persistence
agent-specific metadata conventions
```

That package may depend on:

```text
Microsoft.Agents.AI
```

`LatticeDbSharp.Extensions.AI` must remain framework-neutral.

---

# 47. Dependency Injection

Provide normal Microsoft.Extensions.DependencyInjection helpers if doing so does not introduce unnecessary dependency weight.

Possible API:

```csharp
services.AddLatticeVectorStore(options =>
{
    ...
});
```

or:

```csharp
services.AddSingleton<VectorStore>(
    new LatticeVectorStore(...));
```

Do not create a custom service-location framework.

DI helpers should simply register standard Microsoft abstractions.

---

# 48. Configuration

Avoid configuration-file abstractions in milestone 1 unless needed.

If added later, configuration should use:

```text
Microsoft.Extensions.Options
```

and normal .NET conventions.

Do not put:

```text
API keys
model credentials
embedding endpoints
```

into LatticeDbSharp options.

Those belong to the supplied `IEmbeddingGenerator`.

---

# 49. Cancellation

Cancellation must propagate through all managed async operations.

Especially:

```text
embedding generation
search
batch operations
schema operations
collection operations
```

Where native LatticeDB cannot cancel an operation after dispatch, document that boundary.

Never swallow:

```csharp
OperationCanceledException
```

---

# 50. Exception Model

Use VectorData-compatible exceptions where the abstraction defines them.

Preserve distinctions between:

```text
schema validation failure
unsupported schema
embedding failure
dimension mismatch
query translation failure
native LatticeDB error
cancellation
collection not found
```

Do not turn embedding-provider errors into database errors.

---

# 51. Observability

Do not implement duplicate AI telemetry.

Embedding telemetry belongs to the supplied:

```csharp
IEmbeddingGenerator
```

and its Microsoft.Extensions.AI pipeline.

LatticeDB operations may expose existing LatticeDbSharp diagnostics.

Potential provider diagnostics:

```text
collection operation duration
vector search duration
BM25 search duration
hybrid fusion duration
candidate counts
```

Do not log:

```text
full prompts
full embedded content
vectors
sensitive record contents
```

by default.

---

# 52. Provenance

The provider must not claim that a vector match is evidence.

Vector search returns:

```text
record
score
```

not:

```text
verified fact
```

Embedding provenance remains an application concern.

Applications such as Hongxian may separately persist:

```text
embedding provider
model
dimensions
generation time
source revision
content hash
```

The VectorData adapter must not prevent this metadata from being stored as normal record properties.

---

# 53. Thread Safety

Determine whether:

```text
LatticeDatabase
LatticeVectorStore
LatticeVectorStoreCollection
```

are safe for concurrent use.

The adapter must follow native LatticeDB/LatticeDbSharp guarantees.

Do not add coarse global locks unless required.

If native writes must be serialized, isolate serialization at the database boundary rather than blocking embedding generation.

---

# 54. Async Boundaries

Do not fake asynchronous behavior with:

```csharp
Task.Run(...)
```

around synchronous native calls unless required for a clearly documented reason.

Embedding calls are genuinely asynchronous.

Native database calls should preserve their real execution semantics.

---

# 55. Performance

Milestone 1 performance goals:

* no unnecessary vector copies
* batch upsert uses batching where available
* batch embedding uses one generator batch request where possible
* filters execute inside LatticeDB when supported
* top-k vector search remains native
* BM25 remains native
* hybrid candidate sets remain bounded
* record materialization avoids reflection on every operation

Keep mapping implementation internal.

---

# 56. Reflection and Mapping

Schema analysis may use reflection once during collection initialization.

Compile property accessors where worthwhile.

Avoid repeated:

```csharp
PropertyInfo.GetValue(...)
PropertyInfo.SetValue(...)
```

on hot paths if benchmarks show material overhead.

Keep mapping implementation internal.

---

# 57. Schema Cache

Introduce an internal immutable descriptor, conceptually:

```text
LatticeVectorSchema
```

containing:

```text
key property
data properties
full-text fields
vector fields
dimensions
distance functions
index kinds
storage names
compiled getters
compiled setters
```

Cache it for the lifetime of the store or collection.

---

# 58. Schema Evolution

Milestone 1 does not need automatic destructive schema migration.

If an existing collection conflicts with the requested schema:

```text
fail clearly
```

Do not silently:

```text
drop indexes
rebuild vectors
delete fields
change dimensions
```

Future schema migration can be designed separately.

---

# 59. Multiple Vector Fields

Microsoft VectorData models can potentially contain multiple vector properties.

Investigate current LatticeDB support.

Preferred:

```text
support multiple named vector fields if native storage supports it
```

Otherwise:

```text
reject schemas containing more than one vector field
```

Do not arbitrarily choose the first vector property.

---

# 60. Multiple Embedding Sources

When automatic embedding generation is used with multiple vector fields, each field may require its own source/model semantics.

Do not invent automatic mapping.

Milestone 1 may restrict automatic embedding generation to:

```text
one vector field per collection
```

unless the Microsoft abstractions provide an unambiguous source mapping.

Explicit vectors may still support multiple fields if LatticeDB supports them.

---

# 61. BM25 Without Embeddings

Collections should be able to use:

```text
BM25 only
```

without configuring an embedding generator.

Likewise:

```text
vector only
```

must work without full-text indexing.

And:

```text
hybrid
```

requires both capabilities.

Do not force AI services onto users who only need local lexical retrieval.

---

# 62. Offline Operation

The package itself must remain fully usable offline.

Example:

```text
local hash/model embedding generator
+
LatticeDB
```

or:

```text
caller-supplied vector
+
LatticeDB
```

should require no network.

---

# 63. Security

Treat search input as untrusted.

In particular:

* parameterize native queries
* never concatenate filter strings into Cypher or equivalent query text
* validate collection names
* validate storage names
* escape internal identifiers correctly
* bound top-k values
* avoid exposing arbitrary graph queries through generic search APIs

Agent-generated search strings must not become executable database query fragments.

These requirements apply especially because retrieved records may influence model behavior (indirect prompt injection surface).

---

# 64. Test Strategy

## 64.1 Unit tests

Cover:

1. schema extraction from attributes
2. schema extraction from `VectorStoreCollectionDefinition`
3. key mapping
4. data mapping
5. vector mapping
6. dimension validation
7. unsupported type detection
8. unsupported index detection
9. unsupported distance detection
10. filter translation
11. collection naming
12. record materialization
13. cancellation propagation
14. score conversion
15. RRF fusion if implemented

## 64.2 Integration tests

Use a real temporary LatticeDB database.

Cover:

1. create collection
2. list collection
3. delete collection
4. upsert record
5. update record
6. retrieve record
7. delete record
8. vector search
9. filtered vector search
10. BM25 search
11. hybrid search
12. automatic embedding generation
13. dynamic collection
14. multiple collections in one database
15. persistence across database reopen

---

# 65. Fake Embedding Generator

Tests should use a deterministic fake:

```csharp
internal sealed class FakeEmbeddingGenerator
    : IEmbeddingGenerator<string, Embedding<float>>
{
    ...
}
```

It should map known inputs to deterministic vectors.

Do not use network models in the normal test suite.

This makes ranking assertions repeatable.

---

# 66. Interoperability Test

Add a test that uses only Microsoft abstractions after construction.

Example shape:

```csharp
VectorStore store = new LatticeVectorStore(...);

var collection =
    store.GetCollection<string, MemoryRecord>("memory");

await collection.EnsureCollectionExistsAsync();

await collection.UpsertAsync(record);

await foreach (var result in collection.SearchAsync(
    "workflow mutation",
    top: 5))
{
    ...
}
```

The consumer should not need any LatticeDB-specific API for this path.

---

# 67. DataIngestion Test

Add an integration test or sample proving:

```text
Microsoft.Extensions.DataIngestion
            |
            v
     VectorStoreWriter
            |
            v
    LatticeVectorStore
```

works without a special adapter.

Do not ship DataIngestion integration code if no integration code is actually necessary.

---

# 68. Agent Framework Compatibility Test

Prefer a sample or optional integration-test project rather than a package dependency.

Prove that:

```text
ChatHistoryMemoryProvider
        |
        v
LatticeVectorStore
```

can persist and retrieve semantic chat memory.

This test/sample may reference Agent Framework.

The production `Extensions.AI` package must not.

---

# 69. Documentation

The README should begin with a straightforward description:

> `LatticeDbSharp.Extensions.AI` integrates LatticeDB with Microsoft.Extensions.AI and Microsoft.Extensions.VectorData, allowing LatticeDB to be used as a local persistent vector, full-text, and hybrid retrieval store in .NET AI applications.

Show three primary examples.

## Example 1

Vector search with explicit vectors.

## Example 2

Automatic embeddings with `IEmbeddingGenerator`.

## Example 3

Persistent Agent Framework memory using the standard `VectorStore` abstraction.

Later add:

## Example 4

Hybrid BM25 + vector retrieval.

## Example 5

Graph expansion after retrieval.

---

# 70. Package Positioning

Do not position this as:

> an embedding client for LatticeDB

Position it as:

> a Microsoft .NET AI provider for LatticeDB

The differentiators are:

```text
embedded/local
persistent
HNSW vectors
BM25
hybrid retrieval
property graph
single database
standard Microsoft AI abstractions
```

---

# 71. Milestone 0: Compatibility Spike

Before implementation, inspect:

```text
current Microsoft.Extensions.VectorData API
current LatticeDbSharp vector API
current LatticeDB BM25 API
current LatticeDB index lifecycle
current LatticeDB graph/property model
```

Produce a short compatibility table:

```text
VectorData capability        LatticeDB capability       Status
----------------------------------------------------------------
collections                  ?                          map
key properties               ?                          map
data properties              ?                          map
indexed properties           ?                          map
full-text                    BM25                       native
vectors                      vectors                    native
HNSW                         HNSW                       native
filters                      query predicates           map
hybrid search                vector + BM25              design
multiple vectors             ?                          investigate
dynamic model                properties                 investigate
```

Resolve unknowns before locking public APIs.

---

# 72. Milestone 1: Core VectorData Provider

Implement:

```text
LatticeVectorStore
LatticeVectorStoreCollection<TKey,TRecord>

collection lifecycle
typed models
attribute schema
programmatic schema
upsert
get
delete
vector search
filters
IEmbeddingGenerator integration
automatic embeddings
dimension validation
GetService
```

This is the first publishable milestone.

---

# 73. Milestone 1.1: Dynamic Models

Add or complete:

```text
GetDynamicCollection
Dictionary<string, object?>
runtime collection definitions
```

This may be included in milestone 1 if implementation is straightforward.

---

# 74. Milestone 2: Full-Text and Hybrid Retrieval

Implement:

```text
IsFullTextIndexed
BM25 mapping
IKeywordHybridSearchable<TRecord>
hybrid filtering
deterministic score fusion
```

Add ranking tests.

---

# 75. Milestone 3: AI Ecosystem Samples

Provide working examples for:

```text
Microsoft.Extensions.DataIngestion
Microsoft Agent Framework ChatHistoryMemoryProvider
custom IEmbeddingGenerator
Baize adapter example if useful
local Ollama example
```

Examples belong outside the core package dependency graph.

---

# 76. Milestone 4: Graph-Aware Retrieval

Explore LatticeDB-specific extensions:

```text
vector seed retrieval
hybrid seed retrieval
bounded graph traversal
structured context expansion
GraphRAG-oriented result types
```

Do not change the portable `VectorStore` contract.

---

# 77. Future Separate Package

Potential:

```text
LatticeDbSharp.Extensions.AgentFramework
```

Only create it when direct Agent Framework contracts provide meaningful functionality that cannot already be obtained through VectorData.

Candidates:

```text
AgentSessionStore
AgentHistoryProvider
durable agent metadata
agent-specific history conventions
```

That package may depend on:

```text
Microsoft.Agents.AI
```

`LatticeDbSharp.Extensions.AI` must remain framework-neutral.

---

# 78. Acceptance Criteria for First Release

The first release is complete when:

* `LatticeVectorStore` derives from Microsoft's `VectorStore`
* typed collections derive from `VectorStoreCollection<TKey,TRecord>`
* attribute-defined models work
* programmatic collection definitions work
* collection creation and deletion work
* records can be upserted, read and deleted
* native vector search works
* string search uses `IEmbeddingGenerator` when configured
* explicit vectors work without an embedding generator
* cancellation reaches embedding operations
* vector dimensions are validated
* supported filters execute correctly
* `GetService` exposes the underlying LatticeDB service where appropriate
* package has no model-provider dependency
* package has no Agent Framework dependency
* package has no Semantic Kernel dependency
* package has no Baize dependency
* native `lattice_embedding_client_*` is not used by the provider
* persistence survives reopening the database
* standard VectorData usage does not require LatticeDB-specific code after store construction
* README contains working examples

Hybrid retrieval may ship in the same release if it can be implemented correctly, otherwise it is milestone 2.

---

# 79. Acceptance Criteria for Hybrid Search

Hybrid search is complete when:

* full-text fields map to native BM25 indexes
* the collection implements `IKeywordHybridSearchable<TRecord>`
* vector retrieval is native
* lexical retrieval is native
* filters apply consistently
* score fusion is deterministic
* fusion does not assume cosine and BM25 scores share a scale
* ranking tests cover lexical-only, semantic-only and mixed relevance
* result scores have documented semantics

---

# 80. Architectural Invariants

These rules should remain true as the package evolves.

### Invariant 1

```text
Embedding generation != database responsibility
```

### Invariant 2

```text
Microsoft.Extensions.AI owns the model abstraction
```

### Invariant 3

```text
Microsoft.Extensions.VectorData owns the portable storage contract
```

### Invariant 4

```text
LatticeDbSharp.Extensions.AI adapts those abstractions to LatticeDB
```

### Invariant 5

```text
Graph functionality remains accessible without corrupting
the generic VectorData abstraction
```

### Invariant 6

```text
Agent Framework remains a consumer, not a dependency
```

### Invariant 7

```text
Similarity is retrieval evidence, not factual authority
```

---

# 81. Intended End State

A generic Microsoft AI application should eventually be able to write:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
    embeddings = CreateEmbeddingGenerator();

using var database = new LatticeDatabase("memory.db");

VectorStore vectorStore =
    new LatticeVectorStore(
        database,
        new()
        {
            EmbeddingGenerator = embeddings
        });

await foreach (var result in memories.SearchAsync(
    "What did we decide about changing workflows?",
    top: 5))
{
    Console.WriteLine(result.Record.Content);
}
```

The application should then be able to pass the same:

```csharp
VectorStore
```

to compatible Microsoft AI components without knowing that the persistence engine is LatticeDB.

At the same time, applications that know they are using LatticeDB should still be able to access:

```text
BM25
graph traversal
native queries
LatticeDB-specific retrieval
```

through explicit extension points.

That combination is the purpose of `LatticeDbSharp.Extensions.AI`:

> Standard Microsoft AI interoperability on the outside, LatticeDB's richer retrieval model underneath.
