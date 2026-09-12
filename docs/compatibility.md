# Compatibility policy

Managed and native versions are independent. A LatticeDbSharp release supports
only the explicitly listed LatticeDB source revision and verified runtime
identifiers.

Initial upstream baseline:

| Component | Version |
| --- | --- |
| LatticeDbSharp | 0.1.0-preview.2 |
| LatticeDB | v0.15.0 |
| LatticeDB commit | 9800159e22e200f2b6888c7c6be1810adb695506 |
| Zig toolchain | 0.16.0 |

Changing the upstream header requires an ABI review covering versioned option
structures, struct_size, packing, alignment, enums, Boolean representation,
pointer ownership, UTF-8, size_t, integer widths, and free functions.

A platform is supported only after its exact native asset passes ABI,
functional, persistence/recovery, locking, and repeated cleanup tests. Linux
x64, Windows x64, and macOS ARM64 have verified build/package/load mechanics
and pass all 22 expanded native tests.

The packaged native assets are built from the pinned upstream commit plus the
hash-verified patch set declared in `native/upstream.json`. That patch persists
the initial tree baseline before commits and repairs only a physically
incomplete final WAL frame. Patch identity is part of every native asset
manifest and the NuGet package audit.

Persisted file-format compatibility is an upstream contract that must be
checked independently for every upgrade. LatticeDbSharp does not infer it from
C ABI compatibility or promise compatibility that the pinned upstream release
does not establish.

The wrapper preserves the pinned engine's Cypher dialect rather than claiming
full openCypher compatibility. For example, v0.15.0 does not plan standalone
`RETURN` expressions; supported query examples use an explicit graph clause.
