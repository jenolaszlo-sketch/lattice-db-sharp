# Native ownership contract

This inventory is derived from the archived LatticeDB v0.15.0 header and the
pinned implementation at commit
`9800159e22e200f2b6888c7c6be1810adb695506`. It governs internal interop design;
public APIs must not expose these pointers or weaken their lifetimes.

## Handle ownership

| Native resource | Created by | Released or completed by | Important transition |
| --- | --- | --- | --- |
| `lattice_database*` | `lattice_open*`, `lattice_deserialize*` | `lattice_close` | `INVALID_ARG` caused by active children retains the handle. Once finalization begins, success and `IO` both consume it. |
| `lattice_txn*` | `lattice_begin` | `lattice_commit`, `lattice_rollback` | Commit success consumes it. A recoverable commit error retains it for retry or rollback; an aborted transaction is no longer usable. Rollback consumes an active transaction even when rollback itself reports an error. |
| `lattice_query*` | `lattice_query_prepare` | `lattice_query_free` | Query diagnostics and their strings are borrowed until the query is reused or freed. |
| `lattice_result*` | `lattice_query_execute` | `lattice_result_free` | Column names and values are borrowed from the result and must be copied before it is freed. |
| `lattice_vector_result*` | vector-search functions | `lattice_vector_result_free` | Element IDs and scores are copied scalar values. |
| `lattice_fts_result*` | FTS-search functions | `lattice_fts_result_free` | Element IDs and scores are copied scalar values. |
| `lattice_edge_result*` | traversal and edge-scan functions | `lattice_edge_result_free` | Returned edge-type strings are borrowed from the result. |
| `lattice_stream_batch*` | `lattice_stream_read` | `lattice_stream_batch_free` | Kinds and recursive payload values are borrowed from the batch. |
| `lattice_embedding_client*` | `lattice_embedding_client_create` | `lattice_embedding_client_free` | Configuration strings are call-scoped inputs; the client itself is owned. |

Database children retain their managed parent and prevent managed close from
starting. A checked close operation must distinguish a retained handle from a
consumed handle; `SafeHandle.ReleaseHandle()` alone cannot report that distinction
to a caller.

## Transferred allocations

| Allocation | Returned by | Release function |
| --- | --- | --- |
| Recursive `lattice_value` storage | node/edge property getters | `lattice_value_free` |
| Node-ID arrays | label/all-node/index lookups | `lattice_free_node_ids` with the returned count |
| Edge-ID arrays | indexed edge lookup | `lattice_free_edge_ids` with the returned count |
| UTF-8 label string | `lattice_node_get_labels` | `lattice_free_string` |
| Serialized database bytes | `lattice_serialize` | `lattice_free_bytes` with the returned length |
| Hash or remote embedding vector | embedding functions | `lattice_hash_embed_free` with the returned dimensions |

Owning APIs transfer storage only on success. Managed code copies the value into
bounded managed storage inside the owning scope and releases the native allocation
in `finally`.

The pinned `lattice_node_get_labels` ABI returns one comma-separated string and
does not provide escaping or a count. The native symbol table itself accepts
commas, so the managed label APIs reject commas at every label input boundary;
otherwise one native label could be silently split into multiple managed labels.

## Borrowed data

- Input UTF-8 strings, byte spans, vectors, and recursive values are borrowed
  only for the native call unless an API explicitly says otherwise.
- Query result values, column names, query diagnostics, edge types, and stream
  batch payloads must not escape their owner without a defensive copy.
- `lattice_version` and `lattice_error_message` return native static strings and
  must never be freed.
- `lattice_deserialize_borrowed` is deferred. Supporting it requires a distinct
  managed owner that pins or owns the exact source bytes for the full database
  lifetime; it must not share the copying-deserialize API.

## Managed implementation rules

- Use one internal owner per native handle; do not give resource owners record
  equality.
- Track database children explicitly and reject new children after close begins.
- Serialize operations on a single handle unless the pinned implementation and
  tests prove concurrency safe.
- Mark consumed handles invalid immediately, including consumed error outcomes.
- Preserve the original numeric native error and its static message.
- Never use a finalizer as the normal completion path for transactions.

## Managed recursive-value safety limits

The managed binding bounds recursive value materialization and parameter
marshalling. A value may be at most 64 nested list/map levels, contain at most
1,000,000 aggregate collection items, and copy at most 64 MiB of recursive
payload bytes in one value. Native null-terminated diagnostic and metadata
strings are capped at 1 MiB. These limits protect the managed process from
corrupt native data and accidental unbounded allocations; callers needing
larger payloads should use a purpose-specific binary or streaming API.
