param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$project = Join-Path $repositoryRootPath 'eng/LatticeDbSharp.PackageSmoke/LatticeDbSharp.PackageSmoke.csproj'
$packageSource = [IO.Path]::GetFullPath((Split-Path -Parent $resolvedPackage))

$archive = [IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $nuspecs = @($archive.Entries | Where-Object {
        $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase)
    })
    if ($nuspecs.Count -ne 1) {
        throw "Package must contain exactly one nuspec; found $($nuspecs.Count)."
    }

    $reader = [IO.StreamReader]::new($nuspecs[0].Open())
    try {
        [xml] $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    $version = [string]$nuspec.package.metadata.version
}
finally {
    $archive.Dispose()
}

$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$packagesRoot = [IO.Path]::GetFullPath((Join-Path $systemTempRoot "latticedbsharp-consumer-$([Guid]::NewGuid().ToString('N'))"))
if (-not $packagesRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Consumer package cache escaped the system temporary root.'
}
$nugetConfigPath = [IO.Path]::GetFullPath((Join-Path $systemTempRoot "latticedbsharp-consumer-$([Guid]::NewGuid().ToString('N')).nuget.config"))
if (-not $nugetConfigPath.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Consumer NuGet configuration escaped the system temporary root.'
}

$nugetConfig = [Xml.XmlDocument]::new()
$null = $nugetConfig.AppendChild($nugetConfig.CreateXmlDeclaration('1.0', 'utf-8', $null))
$configuration = $nugetConfig.AppendChild($nugetConfig.CreateElement('configuration'))
$packageSources = $configuration.AppendChild($nugetConfig.CreateElement('packageSources'))
$null = $packageSources.AppendChild($nugetConfig.CreateElement('clear'))
$localSource = $packageSources.AppendChild($nugetConfig.CreateElement('add'))
$localSource.SetAttribute('key', 'local')
$localSource.SetAttribute('value', $packageSource)
$nugetSource = $packageSources.AppendChild($nugetConfig.CreateElement('add'))
$nugetSource.SetAttribute('key', 'nuget.org')
$nugetSource.SetAttribute('value', 'https://api.nuget.org/v3/index.json')
$mapping = $configuration.AppendChild($nugetConfig.CreateElement('packageSourceMapping'))
$localMapping = $mapping.AppendChild($nugetConfig.CreateElement('packageSource'))
$localMapping.SetAttribute('key', 'local')
$localPattern = $localMapping.AppendChild($nugetConfig.CreateElement('package'))
$localPattern.SetAttribute('pattern', 'LatticeDbSharp')
$remoteMapping = $mapping.AppendChild($nugetConfig.CreateElement('packageSource'))
$remoteMapping.SetAttribute('key', 'nuget.org')
foreach ($pattern in @('Microsoft.*', 'NETStandard.Library', 'runtime.*', 'System.*')) {
    $remotePattern = $remoteMapping.AppendChild($nugetConfig.CreateElement('package'))
    $remotePattern.SetAttribute('pattern', $pattern)
}
try {
    $nugetConfig.Save($nugetConfigPath)
}
catch {
    if (Test-Path -LiteralPath $nugetConfigPath) {
        Remove-Item -LiteralPath $nugetConfigPath -Force -ErrorAction SilentlyContinue
    }
    throw
}

try {
    & dotnet restore $project `
        "-p:SmokePackageVersion=$version" `
        --configfile $nugetConfigPath `
        --packages $packagesRoot `
        --force `
        --no-cache | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to restore the clean package consumer.'
    }

    & dotnet run `
        --project $project `
        --configuration Release `
        --no-restore `
        "-p:SmokePackageVersion=$version" `
        "-p:RestorePackagesPath=$packagesRoot" | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The clean package consumer failed.'
    }
}
finally {
    if (Test-Path -LiteralPath $packagesRoot) {
        Remove-Item -LiteralPath $packagesRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $nugetConfigPath) {
        Remove-Item -LiteralPath $nugetConfigPath -Force -ErrorAction SilentlyContinue
    }
}
