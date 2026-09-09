param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch] $VerifyRemote
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$manifestPath = Join-Path $repositoryRootPath 'native/upstream.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Test-PathWithinRepository {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CandidatePath
    )

    $relativePath = [IO.Path]::GetRelativePath($repositoryRootPath, $CandidatePath)
    return -not [IO.Path]::IsPathRooted($relativePath) -and
        $relativePath -cne '..' -and
        -not $relativePath.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -and
        -not $relativePath.StartsWith("..$([IO.Path]::AltDirectorySeparatorChar)", [StringComparison]::Ordinal)
}

Assert-Contract ($manifest.schemaVersion -eq 1) 'Unsupported upstream manifest schema.'
Assert-Contract ($manifest.commit -cmatch '^[0-9a-f]{40}$') 'Upstream commit must be a lowercase 40-character Git identity.'
Assert-Contract ($manifest.reference -cmatch '^v[0-9]+\.[0-9]+\.[0-9]+$') 'Upstream reference must be a stable version tag.'
Assert-Contract ($manifest.version -ceq $manifest.reference.Substring(1)) 'Upstream version and reference disagree.'
Assert-Contract ($manifest.license -ceq 'MIT') 'The pinned upstream license must be MIT.'

$licensePath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $manifest.licenseFile.path))
Assert-Contract (Test-PathWithinRepository $licensePath) 'License path escapes the repository.'
Assert-Contract (Test-Path -LiteralPath $licensePath -PathType Leaf) 'Pinned upstream license is missing.'
$licenseFile = Get-Item -LiteralPath $licensePath
Assert-Contract ($licenseFile.Length -eq $manifest.licenseFile.length) 'Pinned upstream license length does not match the manifest.'
$licenseHash = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Contract ($licenseHash -ceq $manifest.licenseFile.sha256) 'Pinned upstream license SHA-256 does not match the manifest.'

$toolchainsPath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $manifest.zigToolchains))
Assert-Contract (Test-PathWithinRepository $toolchainsPath) 'Zig toolchain path escapes the repository.'
$toolchains = Get-Content -Raw -LiteralPath $toolchainsPath | ConvertFrom-Json
Assert-Contract ($toolchains.schemaVersion -eq 1) 'Unsupported Zig toolchain manifest schema.'
Assert-Contract ($toolchains.version -ceq $manifest.zigVersion) 'Zig toolchain manifest version does not match the upstream manifest.'
foreach ($platform in $toolchains.platforms.PSObject.Properties) {
    Assert-Contract ($platform.Value.archive -cin @('zip', 'tar.xz')) "Zig toolchain '$($platform.Name)' has an unsupported archive type."
    Assert-Contract ([Uri]::IsWellFormedUriString($platform.Value.url, [UriKind]::Absolute)) "Zig toolchain '$($platform.Name)' has an invalid URL."
    Assert-Contract ($platform.Value.url.StartsWith('https://ziglang.org/', [StringComparison]::Ordinal)) "Zig toolchain '$($platform.Name)' must use the official HTTPS origin."
    Assert-Contract ($platform.Value.sha256 -cmatch '^[0-9a-f]{64}$') "Zig toolchain '$($platform.Name)' has an invalid SHA-256 identity."
}

$headerPath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $manifest.header.path))
Assert-Contract (Test-PathWithinRepository $headerPath) 'Header path escapes the repository.'
Assert-Contract (Test-Path -LiteralPath $headerPath -PathType Leaf) 'Pinned header is missing.'
$headerFile = Get-Item -LiteralPath $headerPath
Assert-Contract ($headerFile.Length -eq $manifest.header.length) 'Pinned header length does not match the manifest.'
$headerHash = (Get-FileHash -LiteralPath $headerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Contract ($headerHash -ceq $manifest.header.sha256) 'Pinned header SHA-256 does not match the manifest.'
$headerText = [IO.File]::ReadAllText($headerPath)
$escapedVersion = [regex]::Escape($manifest.version)
$versionPattern = '#define LATTICE_VERSION "' + $escapedVersion + '"'
Assert-Contract ($headerText -cmatch $versionPattern) 'Pinned header version does not match the manifest.'

$capabilitiesPath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $manifest.capabilities))
Assert-Contract (Test-PathWithinRepository $capabilitiesPath) 'Capability path escapes the repository.'
$capabilities = Get-Content -Raw -LiteralPath $capabilitiesPath | ConvertFrom-Json
Assert-Contract ($capabilities.schemaVersion -eq 1) 'Unsupported capability matrix schema.'
Assert-Contract ($capabilities.upstreamVersion -ceq $manifest.version) 'Capability matrix version does not match the manifest.'
Assert-Contract ($capabilities.upstreamCommit -ceq $manifest.commit) 'Capability matrix commit does not match the manifest.'

foreach ($feature in $capabilities.features.PSObject.Properties) {
    $definition = $feature.Value
    Assert-Contract ($definition.status -cin @('supported', 'deferred', 'unsupported', 'blocked')) "Capability '$($feature.Name)' has an invalid status."
    if ($definition.status -cin @('supported', 'deferred') -and $null -ne $definition.symbol) {
        $symbol = [regex]::Escape([string] $definition.symbol)
        Assert-Contract ($headerText -cmatch "\b$symbol\s*\(") "Capability '$($feature.Name)' references missing symbol '$($definition.symbol)'."
    }
    if ($definition.status -ceq 'unsupported' -and $null -ne $definition.missingSymbol) {
        $missingSymbol = [regex]::Escape([string] $definition.missingSymbol)
        Assert-Contract ($headerText -cnotmatch "\b$missingSymbol\s*\(") "Capability '$($feature.Name)' marks an exported symbol as unsupported."
    }
}

if ($null -ne $manifest.patchSet) {
    Assert-Contract ($manifest.patchSet.schemaVersion -eq 1) 'Unsupported native patch-set schema.'
    Assert-Contract ($manifest.patchSet.id -cmatch '^[A-Za-z0-9._-]+$') 'Native patch-set ID is invalid.'
    Assert-Contract ($manifest.patchSet.path -is [string]) 'Native patch-set path is missing.'
    Assert-Contract ($manifest.patchSet.sha256 -cmatch '^[0-9a-f]{64}$') 'Native patch-set manifest SHA-256 is invalid.'

    $patchManifestPath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $manifest.patchSet.path))
    Assert-Contract (Test-PathWithinRepository $patchManifestPath) 'Native patch-set manifest escapes the repository.'
    Assert-Contract (Test-Path -LiteralPath $patchManifestPath -PathType Leaf) 'Native patch-set manifest is missing.'
    $patchManifestHash = (Get-FileHash -LiteralPath $patchManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Contract ($patchManifestHash -ceq $manifest.patchSet.sha256) 'Native patch-set manifest SHA-256 does not match the upstream manifest.'

    $patchManifest = Get-Content -Raw -LiteralPath $patchManifestPath | ConvertFrom-Json
    Assert-Contract ($patchManifest.schemaVersion -eq 1) 'Unsupported native patch manifest schema.'
    Assert-Contract ($patchManifest.id -ceq $manifest.patchSet.id) 'Native patch manifest ID does not match the upstream manifest.'
    Assert-Contract ($patchManifest.baseVersion -ceq $manifest.version) 'Native patch manifest base version does not match upstream.'
    Assert-Contract ($patchManifest.baseReference -ceq $manifest.reference) 'Native patch manifest base reference does not match upstream.'
    Assert-Contract ($patchManifest.baseCommit -ceq $manifest.commit) 'Native patch manifest base commit does not match upstream.'

    $patches = @($patchManifest.patches)
    Assert-Contract ($patches.Count -gt 0) 'Native patch manifest must contain at least one patch.'
    $patchOrder = @()
    foreach ($patch in $patches) {
        Assert-Contract ($patch.id -cmatch '^[A-Za-z0-9._-]+$') 'Native patch ID is invalid.'
        Assert-Contract ($patch.path -is [string]) "Native patch '$($patch.id)' path is missing."
        Assert-Contract ($patch.sha256 -cmatch '^[0-9a-f]{64}$') "Native patch '$($patch.id)' SHA-256 is invalid."
        Assert-Contract ($patch.changedPaths.Count -gt 0) "Native patch '$($patch.id)' must declare changed paths."
        $patchPath = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath $patch.path))
        Assert-Contract (Test-PathWithinRepository $patchPath) "Native patch '$($patch.id)' escapes the repository."
        Assert-Contract (Test-Path -LiteralPath $patchPath -PathType Leaf) "Native patch '$($patch.id)' is missing."
        $patchHash = (Get-FileHash -LiteralPath $patchPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-Contract ($patchHash -ceq $patch.sha256) "Native patch '$($patch.id)' SHA-256 does not match its manifest."
        foreach ($changedPath in @($patch.changedPaths)) {
            Assert-Contract ($changedPath -is [string] -and
                -not [IO.Path]::IsPathRooted($changedPath) -and
                $changedPath -notmatch '(^|[\/])\.\.([\/]|$)' -and
                $changedPath -notmatch '\\') "Native patch '$($patch.id)' declares an invalid changed path '$changedPath'."
        }
        if ($null -ne $patch.order) {
            $patchOrder += [int] $patch.order
        }
    }
    Assert-Contract (($patchOrder | Sort-Object -Unique).Count -eq $patchOrder.Count) 'Native patch order values must be unique.'
}

[xml] $buildProperties = Get-Content -Raw -LiteralPath (Join-Path $repositoryRootPath 'Directory.Build.props')
$properties = $buildProperties.Project.PropertyGroup
Assert-Contract ($properties.LatticeDbNativeVersion -ceq $manifest.version) 'MSBuild native version does not match the manifest.'
Assert-Contract ($properties.LatticeDbNativeCommit -ceq $manifest.commit) 'MSBuild native commit does not match the manifest.'

if ($VerifyRemote) {
    $tagReference = "refs/tags/$($manifest.reference)"
    $peeledReference = "$tagReference^{}"
    $remoteLines = @(& git ls-remote --tags $manifest.repository $tagReference $peeledReference)
    Assert-Contract ($LASTEXITCODE -eq 0) 'Unable to verify the upstream Git tag.'
    $resolvedCommit = $null
    foreach ($line in $remoteLines) {
        $parts = $line -split "\s+"
        if ($parts.Count -ge 2 -and $parts[1] -ceq $peeledReference) {
            $resolvedCommit = $parts[0]
            break
        }
        if ($parts.Count -ge 2 -and $parts[1] -ceq $tagReference) {
            $resolvedCommit = $parts[0]
        }
    }
    Assert-Contract ($resolvedCommit -ceq $manifest.commit) 'Upstream tag does not resolve to the pinned commit.'
}

Write-Host "Verified LatticeDB $($manifest.reference) contract at $($manifest.commit)."
