# BoltSharp and LatticeDbSharp.Bolt — Protocol-first compatibility specification

**ID:** BS-LDB-01  
**Status:** PROPOSED; specification only; no protocol implementation, NuGet package, or client compatibility yet  
**Date:** 2026-11-02  
**Specification branch:** \`feature/neo4j-bolt-compatibility\` in \`jenolaszlo-sketch/lattice-db-sharp\`  
**Primary outcome:** Use the current Neo4j Browser to inspect real LatticeDB graph nodes and relationships, without writing a custom visualization tool.  
**Implementation order:** Standalone reusable BoltSharp server library → reference backend conformance tests → LatticeDbSharp.Bolt adapter → real Neo4j Browser qualification.  
**Packaging note:** \`BoltSharp\` and \`LatticeDbSharp.Bolt\` are **candidate** names, not verified unclaimed NuGet IDs or published packages.

## 1. Architecture decision

Instead of embedding a Neo4j-specific wire-protocol server in LatticeDbSharp, develop **BoltSharp** as a separate, backend-independent .NET server library. Its consumer supplies an implementation of a small graph-query backend contract. LatticeDbSharp integrates through an independent \`LatticeDbSharp.Bolt\` package.

\`\`\`text
Neo4j Browser / NeoDash               Official .NET Neo4j.Driver
  Bolt over WSS                           Bolt over TCP/TLS
       |                                        |
Kestrel WebSocket adapter                    TCP adapter
       \_______________  _____________________/
                       \/
                 BoltSharp
          handshake + version selection
       PackStream + chunk framing + messages
       session / transaction state machine
       auth hooks + result streaming
                       |
             IBoltQueryBackend
                       |
             LatticeDbSharp.Bolt
             node/edge mapping
          Cypher/query/transaction adapter
                       |
              LatticeDbSharp
                       |
               embedded LatticeDB
\`\`\`

**Separation rule:** BoltSharp must not reference LatticeDbSharp, LatticeDB, Cypher parser implementations, Neo4j.Driver client internals, or a particular storage system. It implements a documented server-side Bolt dialect with explicit capability constraints. It must remain useful for synthetic/in-memory backends and other graph databases.

**Repository rule:** This spec lives on a feature branch in \`lattice-db-sharp\` for initial planning. **The standalone BoltSharp implementation should live in a separate repository and ship independently.** Do not add BoltSharp protocol sources under \`LatticeDbSharp\` simply because this spec is tracked there. Separate repository creation and package naming are follow-up implementation decisions; this document does not create either.

## 2. Goals / non-goals

### Goals

- A reusable, **server-side** .NET Bolt implementation capable of native TCP/TLS and TLS WebSocket transport.
- Conformance to selected, explicitly named Bolt protocol revisions; correct negotiation, framing, PackStream, state transitions, auth, result records and errors.
- A small and practical \`IBoltQueryBackend\` contract with read-only queries initially, typed nodes and relationships, metadata/capabilities and clear resource ownership.
- Runnable, deterministic reference backend independent of LatticeDB for end-to-end client/protocol tests.
- A separate \`LatticeDbSharp.Bolt\` package bridging the public, safe managed LatticeDbSharp API; no native interop in BoltSharp.
- Real graph rendering in a pinned current Neo4j Browser build via WSS, followed by a separately qualified official Neo4j .NET driver via TCP/TLS.
- Strong security and bounds by default: loopback only, explicit hosting, authentication, TLS, frame/query/result limits and safe cleanup.
- NuGet preview only after a real clean consumer and conformance gates.

### Initial non-goals

- Full Neo4j server emulation; every Bolt release; all Cypher/GQL syntax; full Neo4j procedure/admin catalog; plugins/APOC/GDS, clustering or routing, bookmarks, reactive/distributed execution.
- A proprietary Neo4j Browser distribution or copied Neo4j driver implementation.
- Write queries, multi-writer server behavior or misleading transactional guarantees.
- A network server inside LatticeDbSharp core; automatic listener startup.
- Fake native query cancellation, fake database versions or successful replies to unsupported requests.
- A generic protocol-for-any-database abstraction beyond the necessary Bolt backend contract.

## 3. Relevant baseline facts and constraints

1. Neo4j Bolt has distinct transports: raw TCP and WebSocket; WebSocket support in ASP.NET Core **does not implement Bolt**. Both transports should call the same versioned protocol engine.
2. Bolt protocol versions evolve. As documented in November 2026, Neo4j supports Bolt 6.0/6.1 as well as 5.x and older variants. Do not assume one convenient version is what a client will accept; determine the minimum compatible version with real Browser/driver traces. Manifest handshake introduced during Bolt 5.x must be evaluated, including its version/capability negotiation.
3. PackStream is a specific binary format (not MessagePack), including typed structures for graph entities. Message chunking, finite-state transitions, backpressure, failures and explicit transaction state are essential.
4. \`Neo4j.Driver\` is a **client package**, useful for integration tests, not a server framework. \`Bolt.Net.Server\` refers to an unrelated RPC protocol. An old \`PackStream.NET\` alpha package is not an established maintained Bolt 5/6 server. Do not adopt an unverified dependency.
5. The LatticeDbSharp repository currently targets managed \`net8.0\` and its native binding is pinned to upstream LatticeDB v0.15.0 commit \`9800159e22e200f2b6888c7c6be1810adb695506\` plus hash-verified recovery patches. Native assets and ABI gates are deliberate.
6. At that upstream revision, the native query engine internally represents \`node_ref\` and \`edge_ref\`, but the public C API function \`resultValueToBorrowedCValue\` converts both to ordinary \`int\` values. It cannot reliably identify whether \`RETURN n\` or \`RETURN r\` yielded a node or relationship. The pinned C header also lacks a general node/edge property enumeration API. **This is an adapter blocker, not a BoltSharp blocker.**
7. LatticeDbSharp's native queries are synchronous; it uses explicit transactions with multiple readers/one writer. A second process opening an actively held database file may fail due to native locks. Use in-process hosting against an already-owned database for primary acceptance.

## 4. Proposed package boundaries

| Artifact | Independent responsibility | Depends on |
| --- | --- | --- |
| \`BoltSharp\` | PackStream, protocol messages/state, negotiation, backend contract, configurable core | .NET only; no LatticeDB |
| \`BoltSharp.AspNetCore\` (candidate) | TLS WebSocket endpoint/host integration for Browser | BoltSharp, ASP.NET Core |
| \`BoltSharp.Tcp\` (candidate) | Raw TCP/TLS listener for native Neo4j drivers | BoltSharp, .NET sockets/TLS |
| \`BoltSharp.Testing\` (optional) | In-memory backend and canonical protocol fixtures | BoltSharp |
| \`LatticeDbSharp.Bolt\` | Implement backend contract using LatticeDbSharp, translate graph values and read transactions | BoltSharp, LatticeDbSharp |
| Example viewer host | Explicit TLS/auth/local listeners and user walkthrough | packages above |

The package split should **not** be multiplied merely for aesthetics: validate consumers first; consolidate ASP.NET/TCP into BoltSharp assemblies if the simpler packaging better fits dependencies. Keep transport boundaries in architecture regardless.

### Backend interface design constraints

Use a backend-neutral query session with clearly owned read-only transactions and asynchronously enumerable or cursor-based records only where the backend can support them. Do **not** force native synchronous LatticeDB into fake cancellable async. A contract sketch (not a committed API):

\`\`\`csharp
public interface IBoltQueryBackend
{
    ValueTask<IBoltBackendSession> OpenSessionAsync(
        BoltSessionRequest request, CancellationToken cancellationToken);
}

public interface IBoltBackendSession : IAsyncDisposable
{
    ValueTask<IBoltQueryResult> RunAsync(
        string statement, IReadOnlyDictionary<string, BoltValue> parameters,
        BoltRunOptions options, CancellationToken cancellationToken);
}
\`\`\`

Result and transaction contracts must explicitly define session ownership, fields, records, summary, close/rollback, read-only enforcement, bounded \`PULL\` semantics, and how cancellation is handled if it cannot stop native execution. No storage-specific IDs, raw pointers or handles in the protocol's public model.

**Typed Bolt values:** \`BoltNode\`, \`BoltRelationship\`, scalar/list/map types, optional path and special types only when conformance demands them. Identity and relationship endpoints must be unambiguous; never infer entity type from a numeric ID.

## 5. Milestone B0 — protocol/version research and minimal vertical-slice choice

Before implementation, capture the target version of Neo4j Browser and the official Neo4j JavaScript driver it uses, as available; the driver version for official .NET tests; supported encryption/certificate modes; handshake offers and chosen revision; messages required for \`MATCH ... RETURN\` visualization.

Select one specific **revision**, not just “Bolt 5.x” or “Bolt 6.x”. Explicitly record whether current clients require Bolt 5.7 manifest negotiation or Bolt 6.x. If compatibility is unavailable without major extra features, document **go/no-go** before implementing a broad protocol.

**Exit:** Reproducible client/protocol matrix and version-feature list with authoritative spec citations; wire fixtures from a genuine Neo4j test instance where licensing and tool access permit. No guesswork about current Browser behavior.

## 6. Milestone B1 — standalone BoltSharp protocol kernel

Implement and test:

- Bolt magic and version handshake, legacy/manifest forms as actually required; strict version/capability advertisement and rejection.
- Network-order integer reading, PackStream markers/containers/structures, UTF-8/length/depth validation, unknown markers and malformed input rejection.
- Bolt chunking, partial reads, multiple frames/messages, end marker, buffering limits and fragmentation.
- Version-specific messages: \`HELLO\`, \`LOGON\`, \`GOODBYE\`, \`RUN\`, \`PULL\`, \`DISCARD\`, \`RESET\`, \`SUCCESS\`, \`RECORD\`, \`FAILURE\`, \`IGNORED\` as required; \`BEGIN\`/\`COMMIT\`/\`ROLLBACK\` only with faithful read-only semantics.
- Connection/session finite-state machine; pipelining, error propagation and recovery/defunct state; cursor/result ownership.
- Protocol-version-specific graph structures, metadata and error codes; unsupported features fail deterministically rather than fabricating success.
- Resource bounds for connection count, chunk/record sizes, nesting, result lifetime, memory and idle timeout.

**Exit:** Independent wire fixtures, reference backend and adversarial parsing/state tests pass without any LatticeDB dependency. No production code in LatticeDbSharp.

## 7. Milestone B2 — transport adapters and independent client qualification

Implement the two transports against the same protocol core:

1. **WSS through Kestrel:** HTTPS WebSocket upgrade, trusted TLS certificate, allowed Origin/Host policy, optional browser Private Network Access preflight where applicable, authentication. Verify \`bolt+s://\` Browser connection against the exact WebSocket listener path and the actual JS driver.
2. **TCP/TLS:** \`TcpListener\`/sockets/streams, TLS via \`SslStream\` where configured; prove \`bolt://\` and \`bolt+s://\` connections using official \`Neo4j.Driver\` to the independent reference backend.

**Exit:** Real current Browser establishes an authenticated session and renders **synthetic** two-node/one-directed-edge data returned by the reference backend; independently, an official Neo4j .NET driver reads the same synthetic graph over TCP/TLS. Tests prove native TCP vs WebSocket are not conflated.

## 8. Milestone L0 — LatticeDB graph-result feasibility (BLOCKING for LatticeDbSharp.Bolt)

Preserve a fully separate native/wrapper investigation:

- Probe \`MATCH (a)-[r]->(b) RETURN a,r,b\` from the pinned engine through C ABI and managed LatticeValue. Demonstrate the loss of typed graph references, including equal numeric node/edge identifiers.
- Probe native node/relationship label, type, endpoint and **complete property enumeration** in a consistent read snapshot; support nested values or reject them truthfully.
- Propose the smallest versioned upstream C API extension retaining typed result entities and property enumeration. Prefer upstream acceptance. If a local patch is necessary, follow exact upstream pin + patch hashes, ABI checks, native runtime proof on Linux x64/Windows x64/macOS ARM64, recovery and file-format compatibility gates.
- Extend LatticeDbSharp only with safe typed entity/result primitives needed by actual use; preserve SafeHandle semantics and core query compatibility.
- Establish correct behavior for large unsigned native IDs vs PackStream signed integer constraints; use documented opaque string element IDs where protocol version supports them, not lossy casts.
- Confirm query read-only enforcement using actual native read transactions; \`MayWrite\` is advisory only.

**Exit:** A native-backed, correctly typed and hydrated real node and relationship (with properties and direction), independent review, full native evidence. If not possible, **STOP LatticeDbSharp.Bolt**; BoltSharp may still ship independently.

## 9. Milestone L1 — LatticeDbSharp.Bolt integration

Create a separate package implementing the approved BoltSharp backend API. Use a caller-provided open database. Queries execute only under native read-only transaction scopes with deterministic ownership.

- Map bound parameters into LatticeValue without string concatenation.
- Return correct typed nodes/relationships/IDs, labels, properties, relationship direction and result columns; preserve supported value types without silent coercion.
- Allowlist truthful metadata operations required for Browser startup/query visualization. Unavailable Neo4j \`CALL\` procedures or administrative commands must fail explicitly.
- Implement server-side request/result limits, read concurrency bounds, auth and connection cleanup; no fake native cancellation or transaction guarantees.
- Single database alias, e.g. \`neo4j\`, mapped to an existing embedded file or in-memory database. No multi-database, writes, routing or distributed semantics.

**Exit:** Browser draws two **real** LatticeDB nodes plus the actual relationship, with correct labels, edges, properties and identity; read-only write attempt fails with no mutation; standalone \`Neo4j.Driver\` consumer reads the same graph on TCP/TLS.

## 10. Hardening, documentation and package gates (H0)

- Test unsupported Bolt messages, wrong versions, bad PackStream, malformed chunk lengths, partial TCP/WebSocket frames, invalid state transitions, failed auth, TLS errors and disconnections.
- Test PULL limits/backpressure and DISCARD, session resets, read-only explicit transactions if supported, disposal during/after disconnect, native writer lock contention, repeated startup/shutdown and same-database concurrent readers.
- Read-only is a **native transaction guarantee**, not a regex or string classification. No hidden write fallback.
- Do not claim interruptible query cancellation if the native engine cannot cancel it. Bound concurrency, document blocking risk and test hostile queries.
- Pin and record Browser build, .NET driver version, negotiated Bolt revision, OS/RID, TLS mode, exact commit and redacted wire traces. Avoid logging credentials and property payloads.
- Clean, independent NuGet consumers for protocol-only reference backend and LatticeDbSharp adapter; Linux x64, Windows x64 and macOS ARM64 native gates remain required for changed native assets.
- Separate package and repository licensing/attribution/security audit, versioned capability matrix and independent reviewer acceptance before any preview publication.
- Keep \`LatticeDbSharp\` core free of Bolt transport/ASP.NET dependencies.

## 11. Explicit scope / go-no-go matrix

| Gate | Question | If no |
| --- | --- | --- |
| B0 | Can a limited Bolt revision serve the current Browser and .NET driver without reproducing Neo4j internals? | Re-evaluate protocol target |
| B1 | Do binary codec, state and framing tests agree with published Bolt specification? | Fix protocol core; no client claim |
| B2 | Can current Browser connect over real WSS and render a synthetic reference graph? | Do not claim Browser support |
| L0 | Can native LatticeDB expose typed graph entities/properties in one read snapshot? | Stop adapter; BoltSharp can still ship |
| L1 | Does Browser render authentic LatticeDB graph with no writes and no invented identity? | No adapter preview |
| H0 | Do security, lifecycle, portability and clean consumer tests pass? | No publication |

## 12. Naming and repository follow-ups

- Check \`BoltSharp\`, \`BoltSharp.AspNetCore\`, \`BoltSharp.Tcp\` and \`LatticeDbSharp.Bolt\` against the NuGet registry and source repositories before reserving brand/package IDs; preliminary search has **not** confirmed availability.
- First implementation belongs in an independent repository for BoltSharp; this branch's document is the integration decision record and handoff, not the code destination.
- No automatic merge or publish. Preserve this spec, test evidence and independent review receipts. A future LatticeDbSharp adapter can be built on a short-lived branch from the then-current canonical \`main\`.

## References

- Neo4j Bolt protocol: https://neo4j.com/docs/bolt/current/
- Version compatibility including 5.x/6.x: https://neo4j.com/docs/bolt/current/bolt-compatibility/
- Handshake: https://neo4j.com/docs/bolt/current/bolt/handshake/
- Message/state contract: https://neo4j.com/docs/bolt/current/bolt/message/
- PackStream: https://neo4j.com/docs/bolt/current/packstream/
- Browser connection: https://neo4j.com/docs/browser/operations/dbms-connection/
- Official .NET driver (client): https://github.com/neo4j/neo4j-dotnet-driver
- Pinned upstream C API: https://github.com/jeffhajewski/latticedb/blob/9800159e22e200f2b6888c7c6be1810adb695506/src/api/c_api.zig
- LatticeDbSharp ABI contract: \`docs/compatibility.md\`
