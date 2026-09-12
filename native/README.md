# Native source boundary

This directory records the exact upstream source used to define and test the
managed ABI. It must not contain ad-hoc or unverified native binaries.

The initial implementation pin is LatticeDB v0.15.0 at commit
9800159e22e200f2b6888c7c6be1810adb695506. The exact public header is archived
at `include/lattice.h`; `upstream.json` records its length and SHA-256 identity,
and `capabilities.json` records which proposed wrapper features the header can
actually support. CI verifies these files against each other and may also
verify that the upstream tag still resolves to the pinned commit.

`patches/` contains the disclosed, hash-verified changes applied to that base.
The current durable-recovery patch persists the initial tree baseline before
commits and repairs a physically incomplete final WAL frame without accepting
checksum or mid-log corruption. Builds apply it only in an isolated temporary
checkout and record its identity in each native asset manifest.

The Linux x64, Windows x64, and macOS ARM64 builds are reproduced,
ABI/lifecycle-tested, and packaged with generated content-hash manifests.
Database, transaction, query, result, and recursive-value lifetimes are covered
by the native gate. macOS ARM64 builds natively on the macOS CI runner (no
cross-compilation) and is ad-hoc code-signed so the sealed Apple Silicon
code pages map at load. Every runtime identifier must independently pass the
same gates before its asset is added to NuGet.

Generated and staged native outputs belong under ignored native/build and
native/staging directories. Upstream source remains separately licensed.
