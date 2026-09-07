# Native source boundary

This directory records the exact upstream source used to define and test the
managed ABI. It must not contain ad-hoc or unverified native binaries.

The initial pin is LatticeDB v0.10.0 at commit
bcf4c553411cb6758b4b1481f1ec0be5e4872af9. Phase 0 must reproduce the shared
library from that source, archive the header and build provenance, and verify
each runtime identifier before its native asset is placed in a NuGet package.

Generated and staged native outputs belong under ignored native/build and
native/staging directories. Upstream source remains separately licensed.
