# Native runtime and packaging

## Current state

No native library is bundled. Phase 0 builds from the exact source recorded in
native/upstream.json and stages outputs outside source control.

## Loading precedence

The first implementation should use a deterministic resolver with:

1. an explicit caller-provided library path;
2. the package's verified RID-specific runtime asset;
3. a documented development override.

Every failure must report the requested RID, architecture, expected library
name, probing locations, and pinned upstream version without exposing secrets.
Uncontrolled system-library probing is not a compatibility promise.

## Package layout

Only verified assets may be packed:

    runtimes/linux-x64/native/
    runtimes/linux-arm64/native/
    runtimes/osx-x64/native/
    runtimes/osx-arm64/native/
    runtimes/win-x64/native/
    runtimes/win-arm64/native/

Each asset needs reproducible build provenance, SHA-256 identity, upstream
license material, and an integration-test record. Unsupported RIDs must fail
with actionable diagnostics rather than falling back to an arbitrary library.

Windows is a feasibility gate and must not be advertised merely because .NET
can declare the RID.
