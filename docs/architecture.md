# Architecture

## Boundary

LatticeDbSharp translates the pinned LatticeDB C ABI into deterministic resource
ownership and idiomatic .NET values. Interop and every native handle remain
internal. Public APIs never expose pointers.

    consumer
       |
    database / transaction / graph / query / search / stream APIs
       |
    error translation + managed snapshots + SafeHandle ownership
       |
    internal source-generated interop
       |
    pinned LatticeDB native library

The wrapper is synchronous while the native API is synchronous. It will not use
Task.Run to manufacture asynchronous APIs or claim cancellation that cannot
interrupt native work.

## Ownership rules

- Every owned native resource has one SafeHandle.
- Borrowed values never outlive their documented owner.
- Returned strings, values, buffers, and result rows are copied before their
  native owner is released unless the pinned ABI proves a longer lifetime.
- Dispose is deterministic and idempotent; invalid use after disposal fails
  predictably.
- An uncommitted write transaction rolls back on disposal.
- Native errors retain their original code and query diagnostics.

## Concurrency

LatticeDB's multiple-reader/single-writer behavior is preserved. The wrapper
does not silently add a process-wide writer queue. Any future writer
serialization helper must be explicit and opt-in.

Native transactions are explicit and do not enlist in ambient
`System.Transactions` for the preview. The pinned engine exposes no prepare or
two-phase-commit contract. Any future opt-in enlistment must reject promotion,
preserve single-writer behavior, and prove commit, rollback, and uncertain
outcome semantics without implying distributed atomicity.

## Planned source layout

- Interop — generated or header-verified C declarations and ABI constants.
- SafeHandles — internal ownership types.
- Errors — exception taxonomy and native error translation.
- Database and Transactions — lifecycle and transaction state machines.
- Graph — typed IDs, values, nodes, edges, properties, and traversal.
- Query — prepared Cypher, parameters, rows, and result lifetime.
- Search — vectors, FTS, fuzzy search, and property indexes.
- Streams — durable streams and graph changefeeds.

Domain schemas, embeddings, RAG, Hetu, and Cangjie remain above this boundary.
