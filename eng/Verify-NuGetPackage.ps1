param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)

try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    $nuspecEntry = $archive.Entries |
        Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if ($null -eq $nuspecEntry) {
        throw 'Package does not contain a .nuspec file.'
    }

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

    foreach ($required in @('README.md', 'LICENSE', 'NOTICE', 'THIRD_PARTY_NOTICES.md')) {
        if ($entries -cnotcontains $required) {
            throw "Package is missing required root file '$required'."
        }
    }

    $native = $entries | Where-Object {
        $_.StartsWith('runtimes/', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetExtension($_).ToLowerInvariant() -in @('.dll', '.so', '.dylib', '.a')
    }
    if ($native.Count -gt 0) {
        throw "The Phase 0 package unexpectedly contains unverified native assets: $($native -join ', ')"
    }

    Write-Host "Verified package metadata and the Phase 0 native boundary: $resolvedPackage"
}
finally {
    $archive.Dispose()
}
