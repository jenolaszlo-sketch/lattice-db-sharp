# Compatibility policy

Managed and native versions are independent. A LatticeDBSharp release supports
only the explicitly listed LatticeDB source revision and verified runtime
identifiers.

Initial upstream baseline:

| Component | Version |
| --- | --- |
| LatticeDBSharp | 0.1.0-preview.1 |
| LatticeDB | v0.10.0 |
| LatticeDB commit | bcf4c553411cb6758b4b1481f1ec0be5e4872af9 |
| Zig toolchain | 0.16.0 |

Changing the upstream header requires an ABI review covering versioned option
structures, struct_size, packing, alignment, enums, Boolean representation,
pointer ownership, UTF-8, size_t, integer widths, and free functions.

A platform is supported only after its exact native asset passes ABI,
functional, persistence/recovery, locking, and repeated cleanup tests. Linux
x64 and macOS ARM64 are the first feasibility targets. Windows x64 is
experimental until upstream builds and the complete native gate passes.

Persisted LatticeDB file-format compatibility is an upstream guarantee and must
not be inferred from C ABI compatibility alone.
