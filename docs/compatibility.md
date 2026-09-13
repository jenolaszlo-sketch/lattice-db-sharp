# Compatibility policy

Managed and native versions are independent. A LatticeDbSharp release supports
only the explicitly listed LatticeDB source revision and verified runtime
identifiers.

Initial upstream baseline:

| Component | Version |
| --- | --- |
| LatticeDbSharp | 0.3.0 |
| LatticeDB | v0.15.0 |
| LatticeDB commit | 9800159e22e200f2b6888c7c6be1810adb695506 |
| Zig toolchain | 0.16.0 |

Changing the upstream header requires an ABI review covering versioned option
structures, struct_size, packing, alignment, enums, Boolean representation,
pointer ownership, UTF-8, size_t, integer widths, and free functions.

A platform is supported only after its exact native asset passes ABI,
functional, persistence/recovery, locking, and repeated cleanup tests. Linux
x64, Windows x64, and macOS ARM64 have verified build/package/load mechanics
and are exercised by the 43-test native suite.

The packaged native assets are built from the pinned upstream commit plus the
hash-verified patch set declared in `native/upstream.json`. That patch persists
the initial tree baseline before commits and repairs only a physically
incomplete final WAL frame. Patch identity is part of every native asset
manifest and the NuGet package audit.

Persisted file-format compatibility is an upstream contract that must be
checked independently for every upgrade. LatticeDbSharp does not infer it from
C ABI compatibility or promise compatibility that the pinned upstream release
does not establish.

## Compatibility matrix

| Wrapper | Upstream | Upstream commit | Verified RIDs |
| --- | --- | --- | --- |
| 0.1.0 | v0.15.0 | `9800159` | linux-x64, win-x64 |
| 0.1.1 | v0.15.0 | `9800159` | linux-x64, win-x64, osx-arm64 |

Unlisted wrapper versions were never published; `0.1.0` on NuGet predates the
macOS asset and has no matching symbols, so `0.1.1` is the first good stable.
RIDs not listed fail with an actionable diagnostic instead of loading.

## Upgrade protocol

Every upstream or wrapper upgrade follows these steps in order; a later step
never starts while an earlier one is red:

1. Read the upstream release notes and diff the public header against the
   archived `native/include/lattice.h`.
2. Update the pin (`native/upstream.json`), re-archive the header, and record
   the outcome in `native/capabilities.json` per feature (supported,
   deferred, unsupported).
3. Extend the ABI probe and managed layout assertions for every newly bound
   struct, enum, or callback surface.
4. Check persisted file-format compatibility independently: open databases
   written by the previous pin with the new build, across restarts, and
   prove old files either open or fail with a documented corruption error.
5. Gate Linux x64, Windows x64, and macOS ARM64 independently through the
   full native suite before any asset enters a package.
6. Version the wrapper by impact: new bindings are a minor bump, behavior
   fixes a patch bump, and any native file-format break is a major bump
   regardless of managed API stability.

The wrapper preserves the pinned engine's Cypher dialect rather than claiming
full openCypher compatibility. For example, v0.15.0 does not plan standalone
`RETURN` expressions; supported query examples use an explicit graph clause.
