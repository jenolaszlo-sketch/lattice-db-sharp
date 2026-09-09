param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$verifier = Join-Path $PSScriptRoot 'Verify-NuGetPackage.ps1'
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$caseDirectory = Join-Path $systemTempRoot "latticedbsharp-package-audit-$([Guid]::NewGuid().ToString('N'))"
$caseDirectory = [IO.Path]::GetFullPath($caseDirectory)
if (-not $caseDirectory.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package-audit temporary directory escaped the system temporary root.'
}

function Add-ArchiveEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ArchivePath,
        [Parameter(Mandatory = $true)]
        [string] $EntryName
    )

    $archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.CreateEntry($EntryName)
        $stream = $entry.Open()
        try {
            $stream.WriteByte(0)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-PackageRejected {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CandidatePath
    )

    $rejected = $false
    try {
        & $verifier -PackagePath $CandidatePath
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected) {
        throw "Package verifier accepted malformed archive '$CandidatePath'."
    }
}

function Corrupt-ArchiveEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ArchivePath,
        [Parameter(Mandatory = $true)]
        [string] $EntryName
    )

    $archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $matches = @($archive.Entries | Where-Object { $_.FullName -ceq $EntryName })
        if ($matches.Count -ne 1) {
            throw "Expected one '$EntryName' to corrupt; found $($matches.Count)."
        }

        $memory = [IO.MemoryStream]::new()
        $source = $matches[0].Open()
        try {
            $source.CopyTo($memory)
        }
        finally {
            $source.Dispose()
        }
        $bytes = $memory.ToArray()
        $memory.Dispose()
        if ($bytes.Length -eq 0) {
            throw "Cannot corrupt empty archive entry '$EntryName'."
        }
        $bytes[0] = $bytes[0] -bxor 0xff

        $matches[0].Delete()
        $replacement = $archive.CreateEntry($EntryName)
        $destination = $replacement.Open()
        try {
            $destination.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

New-Item -ItemType Directory -Path $caseDirectory | Out-Null
try {
    & $verifier -PackagePath $resolvedPackage

    $misplacedNative = Join-Path $caseDirectory 'misplaced-native.nupkg'
    Copy-Item -LiteralPath $resolvedPackage -Destination $misplacedNative
    Add-ArchiveEntry -ArchivePath $misplacedNative -EntryName 'content/liblattice.so.1'
    Assert-PackageRejected -CandidatePath $misplacedNative

    $duplicateNuspec = Join-Path $caseDirectory 'duplicate-nuspec.nupkg'
    Copy-Item -LiteralPath $resolvedPackage -Destination $duplicateNuspec
    Add-ArchiveEntry -ArchivePath $duplicateNuspec -EntryName 'unexpected.nuspec'
    Assert-PackageRejected -CandidatePath $duplicateNuspec

    $corruptNative = Join-Path $caseDirectory 'corrupt-native.nupkg'
    Copy-Item -LiteralPath $resolvedPackage -Destination $corruptNative
    Corrupt-ArchiveEntry `
        -ArchivePath $corruptNative `
        -EntryName 'runtimes/linux-x64/native/liblattice.so'
    Assert-PackageRejected -CandidatePath $corruptNative

    $patchArchive = [IO.Compression.ZipFile]::OpenRead($resolvedPackage)
    try {
        $patchEntryName = @($patchArchive.Entries |
            Where-Object { $_.FullName -like 'build/LatticeDBSharp/native/patches/*.patch' } |
            Select-Object -ExpandProperty FullName -First 1)
    }
    finally {
        $patchArchive.Dispose()
    }
    if ($patchEntryName.Count -eq 1) {
        $corruptPatch = Join-Path $caseDirectory 'corrupt-patch.nupkg'
        Copy-Item -LiteralPath $resolvedPackage -Destination $corruptPatch
        Corrupt-ArchiveEntry -ArchivePath $corruptPatch -EntryName $patchEntryName[0]
        Assert-PackageRejected -CandidatePath $corruptPatch
    }

    Write-Host 'Verified that malformed package fixtures are rejected.'
}
finally {
    if (Test-Path -LiteralPath $caseDirectory) {
        Remove-Item -LiteralPath $caseDirectory -Recurse -Force
    }
}
