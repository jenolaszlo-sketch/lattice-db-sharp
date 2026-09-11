# Native runtime and packaging

## Current state

The verified staging and package layout contains three native runtimes:
`runtimes/linux-x64/native/liblattice.so`,
`runtimes/win-x64/native/lattice.dll`, and
`runtimes/osx-arm64/native/liblattice.dylib`. CI builds each from the exact
source recorded in `native/upstream.json`, verifies every managed import is
exported, runs the RID-specific ABI probe and native integration suite, and
checks the packaged bytes against the generated SHA-256 asset manifests. The
macOS build runs natively on the macOS runner and ad-hoc signs the dylib and
probe so sealed Apple Silicon pages map and execute.

The expanded suite passes all 22 tests on Linux x64, Windows x64, and macOS
ARM64, including hard-kill recovery and subsequent writes. The assets are ready
for a durable first preview.

## Loading precedence

The deterministic resolver uses:

1. the explicit `LATTICEDBSHARP_NATIVE_LIBRARY` development/diagnostic override;
2. the package's verified RID-specific runtime asset copied beside the managed
   application or retained in its standard runtime directory.

Every failure must report the requested RID, architecture, expected library
name, probing locations, and pinned upstream version without exposing secrets.
Uncontrolled system-library probing is not a compatibility promise.

## Package layout

Only verified assets may be packed. The validated package layout contains:

    runtimes/linux-x64/native/
    runtimes/win-x64/native/
    runtimes/osx-arm64/native/

Future verified candidates are:

    runtimes/linux-arm64/native/
    runtimes/osx-x64/native/
    runtimes/win-arm64/native/

Each asset needs pinned build provenance, SHA-256 identity, upstream
license material, and an integration-test record. Unsupported RIDs must fail
with actionable diagnostics rather than falling back to an arbitrary library.

The v0.15.0 upstream release workflow does not publish a Windows binary, so the
Windows x64 asset is built with the pinned Zig toolchain from the
same pinned base and disclosed patch set as Linux x64. This establishes a viable
LatticeDbSharp packaging target, but does not imply that upstream publishes or
supports Windows.

## Reproducible feasibility build

`eng/Install-Zig.ps1` downloads Zig from its official release origin and checks
the archive against `native/zig-toolchains.json`. `eng/Build-Native.ps1` refuses
modified upstream source, clones an isolated temporary build tree, verifies and
applies the declared patch set, checks changed paths and effective source
identity, builds the native asset, checks required exports, runs
`native/probes/abi.c`, and writes ignored evidence beneath
`native/staging/<rid>/`.

The package includes the patch-set manifest and patch bytes. Each RID asset
manifest records their hashes, changed paths, and an effective patched-source
hash, allowing consumers and CI to identify exactly what was compiled. Zig's
Windows output currently embeds a varying PE timestamp/debug identity, so the
contract does not claim byte-for-byte deterministic native binaries.

CI enables integration tests only after producing verified library and ABI
evidence for the corresponding RID. Adding another runtime identifier requires
its own successful build, ABI, lifecycle, recovery, and packaging record.
