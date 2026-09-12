param(
    [ValidateSet('linux-x64', 'win-x64', 'osx-arm64')]
    [string] $RuntimeIdentifier = 'linux-x64',
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $SourceDirectory,
    [string] $ZigDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repositoryRootPath 'native/upstream.json') |
    ConvertFrom-Json

function Get-RepositoryPath {
    param([Parameter(Mandatory = $true)][string] $RelativePath)

    if ([IO.Path]::IsPathRooted($RelativePath)) {
        throw "Native provenance path '$RelativePath' must be repository-relative."
    }

    $candidate = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $RelativePath))
    $relative = [IO.Path]::GetRelativePath($repositoryRootPath, $candidate)
    if ([IO.Path]::IsPathRooted($relative) -or
        $relative -ceq '..' -or
        $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -or
        $relative.StartsWith("..$([IO.Path]::AltDirectorySeparatorChar)", [StringComparison]::Ordinal)) {
        throw "Native provenance path '$RelativePath' escapes the repository."
    }

    return $candidate
}

function Get-GitOutput {
    param(
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $output = @(& git -C $WorkingDirectory @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed in '$WorkingDirectory': git $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Get-CanonicalPatchSourceHash {
    param(
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]] $ChangedPaths
    )

    $records = foreach ($path in ($ChangedPaths | Sort-Object -Unique)) {
        $blob = Get-GitOutput -WorkingDirectory $WorkingDirectory -Arguments @('hash-object', "--path=$path", '--', $path)
        "$path`0$blob`n"
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($records -join ''))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToHexString($sha256.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}
$sourceDirectory = if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    Join-Path $repositoryRootPath "native/build/latticedb-$($manifest.reference)"
}
else {
    [IO.Path]::GetFullPath($SourceDirectory)
}
$stagingDirectory = Join-Path $repositoryRootPath "native/staging/$RuntimeIdentifier"

$zig = if ([string]::IsNullOrWhiteSpace($ZigDirectory)) {
    (Get-Command zig -ErrorAction Stop).Source
}
else {
    $name = if ($IsWindows) { 'zig.exe' } else { 'zig' }
    Join-Path ([IO.Path]::GetFullPath($ZigDirectory)) $name
}
if (-not (Test-Path -LiteralPath $zig -PathType Leaf)) {
    throw "Zig executable '$zig' does not exist."
}
$isWindowsTarget = $RuntimeIdentifier -eq 'win-x64'
$isMacOsTarget = $RuntimeIdentifier -eq 'osx-arm64'
$target = if ($isWindowsTarget) { 'x86_64-windows-gnu' } elseif ($isMacOsTarget) { 'aarch64-macos' } else { 'x86_64-linux-gnu' }
$libraryFileName = if ($isWindowsTarget) { 'lattice.dll' } elseif ($isMacOsTarget) { 'liblattice.dylib' } else { 'liblattice.so' }
$builtLibrary = if ($isWindowsTarget) {
    Join-Path $sourceDirectory 'zig-out/bin/lattice.dll'
}
else {
    Join-Path $sourceDirectory "zig-out/lib/$libraryFileName"
}
$buildSourceDirectory = $sourceDirectory
$patchProvenance = $null
$temporaryBuildDirectory = $null

$nm = if (-not $isWindowsTarget) {
    (Get-Command nm -ErrorAction Stop).Source
}
else {
    $null
}
$zigVersion = (& $zig version).Trim()
if ($zigVersion -cne $manifest.zigVersion) {
    throw "Zig reports '$zigVersion'; expected '$($manifest.zigVersion)'."
}

try {
if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory '.git'))) {
    if (Test-Path -LiteralPath $sourceDirectory) {
        throw "Native source directory '$sourceDirectory' exists but is not a Git checkout."
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $sourceDirectory) -Force | Out-Null
    & git clone --branch $manifest.reference --depth 1 $manifest.repository $sourceDirectory | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to clone the pinned LatticeDB source.'
    }
}

$sourceCommitOutput = @(& git -C $sourceDirectory rev-parse HEAD 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the native source commit: $($sourceCommitOutput -join [Environment]::NewLine)"
}
$sourceCommit = ($sourceCommitOutput -join [Environment]::NewLine).Trim()
if ($sourceCommit -cne $manifest.commit) {
    throw "Native source resolves to '$sourceCommit'; expected '$($manifest.commit)'."
}
$sourceChangesOutput = @(& git -C $sourceDirectory status --porcelain --untracked-files=all 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect native source status: $($sourceChangesOutput -join [Environment]::NewLine)"
}
$sourceChanges = ($sourceChangesOutput -join [Environment]::NewLine).Trim()
if (-not [string]::IsNullOrWhiteSpace($sourceChanges)) {
    throw "Native source checkout '$sourceDirectory' has local changes; refusing to build modified source."
}
$archivedHeader = Join-Path $repositoryRootPath $manifest.header.path
if (-not (Test-Path -LiteralPath $archivedHeader -PathType Leaf)) {
    throw "The archived ABI header '$archivedHeader' does not exist."
}
$archivedHeaderHash = (Get-FileHash -LiteralPath $archivedHeader -Algorithm SHA256).Hash.ToLowerInvariant()
if ($archivedHeaderHash -cne $manifest.header.sha256) {
    throw 'The archived ABI header does not match its manifest SHA-256.'
}

# Git may materialize this text header with CRLF in the working tree. Compare its
# clean Git blob identity to the verified commit instead of hashing transformed
# checkout bytes as the source identity.
$sourceHeaderBlobOutput = @(& git -C $sourceDirectory hash-object --path=include/lattice.h -- include/lattice.h 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to hash the native source header through Git: $($sourceHeaderBlobOutput -join [Environment]::NewLine)"
}
$sourceHeaderBlob = ($sourceHeaderBlobOutput -join [Environment]::NewLine).Trim()
$commitHeaderBlobOutput = @(& git -C $sourceDirectory rev-parse "$sourceCommit`:include/lattice.h" 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the native source header blob: $($commitHeaderBlobOutput -join [Environment]::NewLine)"
}
$commitHeaderBlob = ($commitHeaderBlobOutput -join [Environment]::NewLine).Trim()
if ($sourceHeaderBlob -cne $commitHeaderBlob) {
    throw "The checked-out source header Git blob '$sourceHeaderBlob' does not match the verified commit blob '$commitHeaderBlob'."
}

if ($null -ne $manifest.patchSet) {
    if ($manifest.patchSet.schemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace($manifest.patchSet.id) -or
        [string]::IsNullOrWhiteSpace($manifest.patchSet.path) -or
        [string]::IsNullOrWhiteSpace($manifest.patchSet.sha256)) {
        throw 'The native patch-set manifest reference is incomplete.'
    }

    $patchManifestPath = Get-RepositoryPath -RelativePath $manifest.patchSet.path
    if (-not (Test-Path -LiteralPath $patchManifestPath -PathType Leaf)) {
        throw "The native patch-set manifest '$patchManifestPath' does not exist."
    }
    $patchManifestHash = (Get-FileHash -LiteralPath $patchManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($patchManifestHash -cne $manifest.patchSet.sha256) {
        throw "The native patch-set manifest hash '$patchManifestHash' does not match the declared '$($manifest.patchSet.sha256)'."
    }

    $patchManifest = Get-Content -Raw -LiteralPath $patchManifestPath | ConvertFrom-Json
    if ($patchManifest.schemaVersion -ne 1 -or
        $patchManifest.id -cne $manifest.patchSet.id -or
        $patchManifest.baseVersion -cne $manifest.version -or
        $patchManifest.baseReference -cne $manifest.reference -or
        $patchManifest.baseCommit -cne $manifest.commit) {
        throw 'The native patch-set manifest does not match the pinned upstream base.'
    }

    $patches = @($patchManifest.patches)
    if ($patches.Count -eq 0) {
        throw 'The native patch-set manifest must contain at least one patch.'
    }

    $temporaryBuildDirectory = Join-Path ([IO.Path]::GetTempPath()) "latticedbsharp-native-$([Guid]::NewGuid().ToString('N'))"
    & git clone --local --no-hardlinks --no-tags $sourceDirectory $temporaryBuildDirectory | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to create the isolated native patch build checkout.'
    }
    $isolatedCommit = Get-GitOutput -WorkingDirectory $temporaryBuildDirectory -Arguments @('rev-parse', 'HEAD')
    if ($isolatedCommit -cne $manifest.commit) {
        throw "Isolated native checkout resolves to '$isolatedCommit'; expected '$($manifest.commit)'."
    }

    $changedPaths = [Collections.Generic.List[string]]::new()
    foreach ($patch in ($patches | Sort-Object order)) {
        if ([string]::IsNullOrWhiteSpace($patch.id) -or
            [string]::IsNullOrWhiteSpace($patch.path) -or
            $patch.sha256 -notmatch '^[0-9a-f]{64}$') {
            throw 'Each native patch must declare id, repository-relative path, and SHA-256.'
        }
        $patchPath = Get-RepositoryPath -RelativePath $patch.path
        if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
            throw "Native patch '$patchPath' does not exist."
        }
        $patchHash = (Get-FileHash -LiteralPath $patchPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($patchHash -cne $patch.sha256) {
            throw "Native patch '$($patch.id)' hash '$patchHash' does not match '$($patch.sha256)'."
        }
        foreach ($changedPath in @($patch.changedPaths)) {
            if ([IO.Path]::IsPathRooted($changedPath) -or
                $changedPath.Contains('\', [StringComparison]::Ordinal) -or
                $changedPath.StartsWith('../', [StringComparison]::Ordinal) -or
                $changedPath -eq '..') {
                throw "Native patch '$($patch.id)' declares an invalid changed path '$changedPath'."
            }
            $changedPaths.Add([string] $changedPath)
        }

        $null = Get-GitOutput -WorkingDirectory $temporaryBuildDirectory -Arguments @('apply', '--check', '--whitespace=error', '--binary', '--', $patchPath)
        $null = Get-GitOutput -WorkingDirectory $temporaryBuildDirectory -Arguments @('apply', '--whitespace=error', '--binary', '--', $patchPath)
    }

    $changedPathOutput = Get-GitOutput -WorkingDirectory $temporaryBuildDirectory -Arguments @('diff', '--name-only')
    $actualChangedPaths = @($changedPathOutput -split "`r?`n" | Where-Object { $_ }) | Sort-Object -Unique
    $expectedChangedPaths = @($changedPaths | Sort-Object -Unique)
    if (($actualChangedPaths -join "`n") -cne ($expectedChangedPaths -join "`n")) {
        throw "Native patch set changed unexpected files. Expected '$($expectedChangedPaths -join ', ')'; found '$($actualChangedPaths -join ', ')'."
    }
    $patchStatus = Get-GitOutput -WorkingDirectory $temporaryBuildDirectory -Arguments @('status', '--porcelain', '--untracked-files=all')
    if (-not [string]::IsNullOrWhiteSpace($patchStatus) -and
        ($patchStatus -split "`r?`n" | Where-Object { $_ -and $_ -notmatch '^ ?M\s+' }).Count -gt 0) {
        throw "Native patch checkout contains unexpected status entries: $patchStatus"
    }

    $buildSourceDirectory = $temporaryBuildDirectory
    $builtLibrary = if ($isWindowsTarget) {
        Join-Path $buildSourceDirectory 'zig-out/bin/lattice.dll'
    }
    else {
        Join-Path $buildSourceDirectory "zig-out/lib/$libraryFileName"
    }
    $patchProvenance = [ordered]@{
        id = [string] $patchManifest.id
        manifest = [IO.Path]::GetRelativePath($repositoryRootPath, $patchManifestPath).Replace('\', '/')
        manifestSha256 = $patchManifestHash
        patches = @($patches | Sort-Object order | ForEach-Object {
            [ordered]@{
                id = [string] $_.id
                path = [string] $_.path
                sha256 = ([string] $_.sha256).ToLowerInvariant()
                changedPaths = @($_.changedPaths)
            }
        })
        changedPaths = $expectedChangedPaths
        effectiveSourceSha256 = Get-CanonicalPatchSourceHash -WorkingDirectory $buildSourceDirectory -ChangedPaths $expectedChangedPaths
    }
}

Push-Location $buildSourceDirectory
try {
    & $zig build shared "-Dtarget=$target" -Doptimize=ReleaseFast | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The pinned LatticeDB shared-library build failed.'
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $builtLibrary -PathType Leaf)) {
    throw "The build did not produce '$builtLibrary'."
}

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
$stagedLibrary = Join-Path $stagingDirectory $libraryFileName
Copy-Item -LiteralPath $builtLibrary -Destination $stagedLibrary -Force

if ($isMacOsTarget) {
    # Apple Silicon refuses to map unsigned code pages; ad-hoc signing is
    # sufficient for a bundled dependency loaded by a non-hardened host.
    & codesign --sign - --force --timestamp=none $stagedLibrary
    if ($LASTEXITCODE -ne 0) {
        throw 'Ad-hoc code signing of the macOS native library failed.'
    }
}

$probeSource = Join-Path $repositoryRootPath 'native/probes/abi.c'
$probeExecutable = Join-Path $stagingDirectory $(if ($isWindowsTarget) { 'lattice-abi-probe.exe' } else { 'lattice-abi-probe' })
& $zig cc $probeSource "-I$(Join-Path $repositoryRootPath 'native/include')" "-target" $target -o $probeExecutable
if ($LASTEXITCODE -ne 0) {
    throw 'The LatticeDB ABI probe did not compile.'
}

if ($isMacOsTarget) {
    & codesign --sign - --force --timestamp=none $probeExecutable
    if ($LASTEXITCODE -ne 0) {
        throw 'Ad-hoc code signing of the macOS ABI probe failed.'
    }
}

$abiJson = (& $probeExecutable).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'The LatticeDB ABI probe did not run successfully.'
}
$null = $abiJson | ConvertFrom-Json
$abiPath = Join-Path $stagingDirectory 'abi.json'
[IO.File]::WriteAllText($abiPath, "$abiJson`n", [Text.UTF8Encoding]::new($false))

$nativeMethodsPath = Join-Path $repositoryRootPath 'src/LatticeDbSharp/Interop/NativeMethods.cs'
$nativeMethodsSource = Get-Content -Raw -LiteralPath $nativeMethodsPath
$requiredSymbols = @(
    [regex]::Matches($nativeMethodsSource, 'EntryPoint\s*=\s*"(?<symbol>lattice_[^"]+)"') |
        ForEach-Object { $_.Groups['symbol'].Value } |
        Sort-Object -Unique
)
if ($requiredSymbols.Count -eq 0) {
    throw "No native entry points were found in '$nativeMethodsPath'."
}

$exports = if ($isWindowsTarget) {
    $libraryHandle = [Runtime.InteropServices.NativeLibrary]::Load($stagedLibrary)
    try {
        foreach ($requiredSymbol in $requiredSymbols) {
            $address = [IntPtr]::Zero
            if (-not [Runtime.InteropServices.NativeLibrary]::TryGetExport(
                    $libraryHandle,
                    $requiredSymbol,
                    [ref]$address)) {
                throw "The native library does not export required symbol '$requiredSymbol'."
            }
        }

        # NativeLibrary.Load/TryGetExport avoids host-specific PE export utilities such as
        # dumpbin or nm while checking the symbols from the managed P/Invoke declarations.
        $requiredSymbols
    }
    finally {
        [Runtime.InteropServices.NativeLibrary]::Free($libraryHandle)
    }
}
elseif ($isMacOsTarget) {
    # Mach-O nm has no GNU -D flag; list globals and keep defined text
    # symbols. C names carry a '_' prefix that is stripped for comparison.
    $macSymbols = @(& $nm -g $stagedLibrary)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the LatticeDB shared-library exports with nm.'
    }
    @($macSymbols |
        Where-Object { $_ -match '\sT\s' } |
        ForEach-Object { ((($_ -split '\s+')[-1]).TrimStart('_')) } |
        Where-Object { $_ })
}
else {
    @(& $nm -D --defined-only $stagedLibrary)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the LatticeDB shared-library exports with nm.'
    }
}
foreach ($requiredSymbol in $requiredSymbols) {
    $exported = if ($isWindowsTarget -or $isMacOsTarget) {
        $exports -contains $requiredSymbol
    }
    else {
        $exports -cmatch "\b$requiredSymbol`$"
    }
    if (-not $exported) {
        throw "The native library does not export required symbol '$requiredSymbol'."
    }
}

$asset = [ordered]@{
    schemaVersion = if ($null -eq $patchProvenance) { 1 } else { 2 }
    runtimeIdentifier = $RuntimeIdentifier
    upstreamVersion = $manifest.version
    upstreamCommit = $manifest.commit
    zigVersion = $manifest.zigVersion
    target = $target
    fileName = $libraryFileName
    verifiedExportCount = $requiredSymbols.Count
    verifiedExports = $requiredSymbols
    length = (Get-Item -LiteralPath $stagedLibrary).Length
    sha256 = (Get-FileHash -LiteralPath $stagedLibrary -Algorithm SHA256).Hash.ToLowerInvariant()
    abi = 'abi.json'
}
if ($null -ne $patchProvenance) {
    $asset.sourceMode = 'upstream-plus-patches'
    $asset.patchSet = $patchProvenance
}
$assetPath = Join-Path $stagingDirectory 'asset.json'
$assetJson = $asset | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($assetPath, "$assetJson`n", [Text.UTF8Encoding]::new($false))

Write-Output ([IO.Path]::GetFullPath($stagedLibrary))
}
finally {
    if ($null -ne $temporaryBuildDirectory -and (Test-Path -LiteralPath $temporaryBuildDirectory)) {
        & git -C $sourceDirectory worktree prune --expire now 2>$null
        Remove-Item -LiteralPath $temporaryBuildDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
