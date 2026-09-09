param(
    [string] $RuntimeIdentifier = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$toolchainManifestPath = Join-Path $repositoryRootPath 'native/zig-toolchains.json'
$manifest = Get-Content -Raw -LiteralPath $toolchainManifestPath | ConvertFrom-Json
$platform = $manifest.platforms.PSObject.Properties[$RuntimeIdentifier].Value
if ($null -eq $platform) {
    throw "Zig $($manifest.version) is not pinned for runtime identifier '$RuntimeIdentifier'."
}

$toolsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRootPath 'native/build/tools'))
$toolRoot = [IO.Path]::GetFullPath((Join-Path $toolsRoot "zig-$($manifest.version)-$RuntimeIdentifier"))
$relativeToolRoot = [IO.Path]::GetRelativePath($toolsRoot, $toolRoot)
if ([IO.Path]::IsPathRooted($relativeToolRoot) -or
    $relativeToolRoot -eq '..' -or
    $relativeToolRoot.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal)) {
    throw "Computed Zig tool directory escapes '$toolsRoot'."
}

$executableName = if ($RuntimeIdentifier.StartsWith('win-', [StringComparison]::Ordinal)) { 'zig.exe' } else { 'zig' }
if (Test-Path -LiteralPath $toolRoot) {
    # The download archive is the pinned trust root. A cached executable could
    # be replaced while still reporting the expected version, so never execute
    # an existing extraction without recreating it from verified bytes.
    Remove-Item -LiteralPath $toolRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$archiveExtension = if ($platform.archive -ceq 'zip') { '.zip' } else { '.tar.xz' }
$archivePath = Join-Path $toolRoot "zig-$($manifest.version)$archiveExtension"
try {
    Write-Host "Downloading Zig $($manifest.version) for $RuntimeIdentifier..."
    $curlName = if ($IsWindows) { 'curl.exe' } else { 'curl' }
    $curl = (Get-Command $curlName -ErrorAction Stop).Source
    & $curl --fail --location --retry 5 --retry-all-errors --connect-timeout 30 --max-time 600 `
        --show-error --silent --output $archivePath $platform.url
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to download Zig from '$($platform.url)'."
    }
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $platform.sha256) {
        throw "Zig archive SHA-256 mismatch. Expected '$($platform.sha256)', found '$actualHash'."
    }

    if ($platform.archive -ceq 'zip') {
        Expand-Archive -LiteralPath $archivePath -DestinationPath $toolRoot
    }
    elseif ($platform.archive -ceq 'tar.xz') {
        & tar -xf $archivePath -C $toolRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to extract Zig archive '$archivePath'."
        }
    }
    else {
        throw "Unsupported Zig archive format '$($platform.archive)'."
    }

    $executable = Get-ChildItem -LiteralPath $toolRoot -Filter $executableName -File -Recurse |
        Select-Object -First 1
    if ($null -eq $executable) {
        throw "The verified Zig archive did not contain '$executableName'."
    }

    $installedVersion = (& $executable.FullName version).Trim()
    if ($installedVersion -cne $manifest.version) {
        throw "Installed Zig reports '$installedVersion'; expected '$($manifest.version)'."
    }

    Write-Output $executable.DirectoryName
}
catch {
    if (Test-Path -LiteralPath $toolRoot) {
        Remove-Item -LiteralPath $toolRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    throw
}
