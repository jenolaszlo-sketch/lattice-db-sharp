param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)

function Read-ArchiveJson {
    param(
        [Parameter(Mandatory = $true)] $Archive,
        [Parameter(Mandatory = $true)][string] $EntryName
    )

    $matches = @($Archive.Entries | Where-Object { $_.FullName -ceq $EntryName })
    if ($matches.Count -ne 1) {
        throw "Package must contain exactly one '$EntryName'; found $($matches.Count)."
    }
    $reader = [IO.StreamReader]::new($matches[0].Open())
    try {
        return ($reader.ReadToEnd() | ConvertFrom-Json)
    }
    finally {
        $reader.Dispose()
    }
}

try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    $nuspecEntries = @($archive.Entries |
        Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) })
    if ($nuspecEntries.Count -ne 1) {
        throw "Package must contain exactly one .nuspec file; found $($nuspecEntries.Count)."
    }
    $nuspecEntry = $nuspecEntries[0]

    $reader = [IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml] $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $license = $nuspec.package.metadata.license
    if ($null -eq $license -or $license.type -ne 'expression' -or $license.'#text' -ne 'Apache-2.0') {
        throw "Expected PackageLicenseExpression Apache-2.0; found '$($license.'#text')'."
    }
    if ($nuspec.package.metadata.id -cne 'LatticeDBSharp') {
        throw "Expected package ID 'LatticeDBSharp'; found '$($nuspec.package.metadata.id)'."
    }
    if ($nuspec.package.metadata.repository.url -cne 'https://github.com/jenolaszlo-sketch/lattice-db-sharp') {
        throw 'Package repository URL is missing or incorrect.'
    }

    $upstreamManifest = Read-ArchiveJson -Archive $archive -EntryName 'build/LatticeDBSharp/upstream.json'
    $upstreamPatchSet = $upstreamManifest.patchSet
    $packagedPatchManifest = $null
    $packagedPatches = @()
    if ($null -ne $upstreamPatchSet) {
        if ($upstreamPatchSet.schemaVersion -ne 1 -or
            $upstreamPatchSet.path -notmatch '^native/patches/' -or
            $upstreamPatchSet.path -match '(^|/)\.\.(/|$)' -or
            $upstreamPatchSet.sha256 -notmatch '^[0-9a-f]{64}$') {
            throw 'Package upstream manifest contains an invalid native patch-set reference.'
        }
        $patchManifestPackagePath = "build/LatticeDBSharp/$($upstreamPatchSet.path)"
        $packagedPatchManifest = Read-ArchiveJson -Archive $archive -EntryName $patchManifestPackagePath
        if ($packagedPatchManifest.schemaVersion -ne 1 -or
            $packagedPatchManifest.id -cne $upstreamPatchSet.id -or
            $packagedPatchManifest.baseVersion -cne $upstreamManifest.version -or
            $packagedPatchManifest.baseReference -cne $upstreamManifest.reference -or
            $packagedPatchManifest.baseCommit -cne $upstreamManifest.commit) {
            throw 'Packaged native patch manifest does not match its upstream base.'
        }
        $packagedPatches = @($packagedPatchManifest.patches)
        if ($packagedPatches.Count -eq 0) {
            throw 'Packaged native patch manifest must contain at least one patch.'
        }

        $patchManifestEntry = @($archive.Entries | Where-Object { $_.FullName -ceq $patchManifestPackagePath })[0]
        $patchManifestStream = $patchManifestEntry.Open()
        $patchManifestSha = [Security.Cryptography.SHA256]::Create()
        try {
            $patchManifestHash = [Convert]::ToHexString($patchManifestSha.ComputeHash($patchManifestStream)).ToLowerInvariant()
        }
        finally {
            $patchManifestSha.Dispose()
            $patchManifestStream.Dispose()
        }
        if ($patchManifestHash -cne $upstreamPatchSet.sha256) {
            throw "Packaged native patch manifest hash does not match upstream.json."
        }
    }

    foreach ($required in @(
        'README.md',
        'LICENSE',
        'NOTICE',
        'THIRD_PARTY_NOTICES.md',
        'THIRD_PARTY_LICENSES/LICENSE.LatticeDB',
        'build/LatticeDBSharp/upstream.json',
        'build/LatticeDBSharp/capabilities.json',
        'build/LatticeDBSharp/zig-toolchains.json',
        'build/LatticeDBSharp/native/linux-x64/abi.json',
        'build/LatticeDBSharp/native/linux-x64/asset.json',
        'build/LatticeDBSharp/native/win-x64/abi.json',
        'build/LatticeDBSharp/native/win-x64/asset.json',
        'runtimes/linux-x64/native/liblattice.so',
        'runtimes/win-x64/native/lattice.dll',
        'lib/net8.0/LatticeDBSharp.dll',
        'lib/net8.0/LatticeDBSharp.xml'
    )) {
        $requiredCount = @($entries | Where-Object { $_ -ceq $required }).Count
        if ($requiredCount -ne 1) {
            throw "Package must contain exactly one '$required'; found $requiredCount."
        }
    }

    $allowedExact = @(
        '_rels/.rels',
        'LatticeDBSharp.nuspec',
        'LICENSE',
        'NOTICE',
        'README.md',
        'THIRD_PARTY_NOTICES.md',
        'THIRD_PARTY_LICENSES/LICENSE.LatticeDB',
        'lib/net8.0/LatticeDBSharp.dll',
        'lib/net8.0/LatticeDBSharp.xml',
        'build/LatticeDBSharp/upstream.json',
        'build/LatticeDBSharp/capabilities.json',
        'build/LatticeDBSharp/zig-toolchains.json',
        'build/LatticeDBSharp/native/linux-x64/abi.json',
        'build/LatticeDBSharp/native/linux-x64/asset.json',
        'build/LatticeDBSharp/native/win-x64/abi.json',
        'build/LatticeDBSharp/native/win-x64/asset.json',
        'runtimes/linux-x64/native/liblattice.so',
        'runtimes/win-x64/native/lattice.dll',
        'package/services/metadata/core-properties/nuget.psmdcp',
        '[Content_Types].xml'
    )
    if ($null -ne $upstreamPatchSet) {
        $allowedExact += "build/LatticeDBSharp/$($upstreamPatchSet.path)"
        foreach ($patch in $packagedPatches) {
            if ($patch.path -notmatch '^native/patches/' -or $patch.path -match '(^|/)\.\.(/|$)') {
                throw "Packaged native patch '$($patch.id)' path is outside native/patches."
            }
            $allowedExact += "build/LatticeDBSharp/$($patch.path)"
        }
    }
    $unexpected = @($entries | Where-Object {
        $entry = $_
        $allowedExact -cnotcontains $entry
    })
    if ($unexpected.Count -gt 0) {
        throw "Package contains entries outside the Phase 0 allowlist: $($unexpected -join ', ')"
    }

    $native = @($archive.Entries | Where-Object {
        $_.FullName -cmatch '(?i)(^|/)(lattice\.dll|liblattice\.so(?:\.[0-9]+)*|liblattice\.dylib|liblattice\.a)$'
    })
    if ($native.Count -ne 2 -or
        ($native.FullName -notcontains 'runtimes/linux-x64/native/liblattice.so') -or
        ($native.FullName -notcontains 'runtimes/win-x64/native/lattice.dll')) {
        throw "Package must contain exactly the verified Linux x64 and Windows x64 native assets; found: $($native.FullName -join ', ')"
    }

    foreach ($contract in @(
        @{ RuntimeIdentifier = 'linux-x64'; FileName = 'liblattice.so' },
        @{ RuntimeIdentifier = 'win-x64'; FileName = 'lattice.dll' }
    )) {
        $assetPath = "build/LatticeDBSharp/native/$($contract.RuntimeIdentifier)/asset.json"
        $assetEntries = @($archive.Entries | Where-Object { $_.FullName -ceq $assetPath })
        if ($assetEntries.Count -ne 1) {
            throw "Package must contain exactly one '$assetPath'; found $($assetEntries.Count)."
        }

        $assetReader = [IO.StreamReader]::new($assetEntries[0].Open())
        try {
            $asset = $assetReader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $assetReader.Dispose()
        }
        if (($null -eq $upstreamPatchSet -and $asset.schemaVersion -ne 1) -or
            ($null -ne $upstreamPatchSet -and $asset.schemaVersion -ne 2) -or
            $asset.runtimeIdentifier -cne $contract.RuntimeIdentifier -or
            $asset.upstreamVersion -cne $upstreamManifest.version -or
            $asset.upstreamCommit -cne $upstreamManifest.commit -or
            $asset.fileName -cne $contract.FileName) {
            throw "Native asset manifest identity does not match the pinned $($contract.RuntimeIdentifier) package contract."
        }

        if ($null -ne $upstreamPatchSet) {
            if ($asset.sourceMode -cne 'upstream-plus-patches' -or
                $asset.patchSet.id -cne $upstreamPatchSet.id -or
                $asset.patchSet.manifest -cne $upstreamPatchSet.path -or
                $asset.patchSet.manifestSha256 -cne $upstreamPatchSet.sha256) {
                throw "Packaged $($contract.RuntimeIdentifier) asset is missing matching native patch provenance."
            }
            $assetPatches = @($asset.patchSet.patches)
            if ($assetPatches.Count -ne $packagedPatches.Count) {
                throw "Packaged $($contract.RuntimeIdentifier) asset patch count does not match its patch manifest."
            }
            foreach ($patchIndex in 0..($packagedPatches.Count - 1)) {
                $expectedPatch = $packagedPatches[$patchIndex]
                $actualPatch = $assetPatches[$patchIndex]
                if ($actualPatch.id -cne $expectedPatch.id -or
                    $actualPatch.path -cne $expectedPatch.path -or
                    $actualPatch.sha256 -cne $expectedPatch.sha256) {
                    throw "Packaged $($contract.RuntimeIdentifier) asset patch provenance does not match its patch manifest."
                }

                $patchPackagePath = "build/LatticeDBSharp/$($expectedPatch.path)"
                $patchEntry = @($archive.Entries | Where-Object { $_.FullName -ceq $patchPackagePath })[0]
                $patchStream = $patchEntry.Open()
                $patchSha = [Security.Cryptography.SHA256]::Create()
                try {
                    $actualPatchHash = [Convert]::ToHexString($patchSha.ComputeHash($patchStream)).ToLowerInvariant()
                }
                finally {
                    $patchSha.Dispose()
                    $patchStream.Dispose()
                }
                if ($actualPatchHash -cne $expectedPatch.sha256) {
                    throw "Packaged native patch '$($expectedPatch.id)' does not match its manifest hash."
                }
            }
        }

        $nativePath = "runtimes/$($contract.RuntimeIdentifier)/native/$($contract.FileName)"
        $nativeEntry = @($archive.Entries | Where-Object { $_.FullName -ceq $nativePath })[0]
        $nativeStream = $nativeEntry.Open()
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $nativeHash = [Convert]::ToHexString($sha256.ComputeHash($nativeStream)).ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
            $nativeStream.Dispose()
        }
        if ($nativeEntry.Length -ne [long]$asset.length -or $nativeHash -cne $asset.sha256) {
            throw "Packaged $($contract.RuntimeIdentifier) native asset does not match its build manifest. Expected $($asset.length)/$($asset.sha256), found $($nativeEntry.Length)/$nativeHash."
        }

        if ($null -ne $upstreamPatchSet) {
            $currentPatchIdentity = $asset.patchSet | ConvertTo-Json -Compress -Depth 10
            if ($null -eq $firstPatchIdentity) {
                $firstPatchIdentity = $currentPatchIdentity
            }
            elseif ($currentPatchIdentity -cne $firstPatchIdentity) {
                throw 'Linux and Windows native assets carry different patch provenance.'
            }
        }
    }

    Write-Host "Verified package metadata and pinned Linux x64/Windows x64 native assets: $resolvedPackage"
}
finally {
    $archive.Dispose()
}
