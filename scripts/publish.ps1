
param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug', 'Release')]
    [string]$Target,

    [Parameter(Mandatory)]
    [string]$TargetPath,

    [Parameter(Mandatory)]
    [string]$TargetAssembly,

    [Parameter(Mandatory)]
    [string]$ValheimPath,

    [Parameter(Mandatory)]
    [string]$ProjectPath,

    [string]$DeployPath
)

Push-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Path)

try {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($TargetAssembly)

    # Resolve project and repository paths.
    $ProjectPath = (Resolve-Path $ProjectPath).Path
    $RepoPath = Split-Path $ProjectPath -Parent

    $SourcePath = $ProjectPath
    $PackagePath = Join-Path $ProjectPath 'Package'
    $PluginsPath = Join-Path $PackagePath 'plugins'
    $ReleasesPath = Join-Path $RepoPath 'Releases'

    # Read version from the plugin source.
    $versionSource = Join-Path $SourcePath 'src\ValeriusCustomPrefabs.cs'

    if (!(Test-Path $versionSource)) {
        throw "Version source not found: $versionSource"
    }

    $sourceCode = Get-Content $versionSource -Raw

    if ($sourceCode -match 'PluginVersion\s*=\s*"([^"]+)"') {
        $version = $Matches[1]
    }
    elseif ($sourceCode -match '\[BepInPlugin\(\s*"[^"]+"\s*,\s*"[^"]+"\s*,\s*"([^"]+)"\s*\)\]') {
        $version = $Matches[1]
    }
    else {
        throw "Could not determine plugin version from $versionSource"
    }

    # Use UTF-8 without BOM for text files.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    $dllPath = Join-Path $TargetPath $TargetAssembly

    if (!(Test-Path $dllPath)) {
        throw "Compiled DLL not found: $dllPath"
    }

    # Check source files.
    foreach ($file in @('README.md', 'manifest.json', 'icon.png')) {
        $sourceFile = Join-Path $SourcePath $file

        if (!(Test-Path $sourceFile)) {
            throw "Source file not found: $sourceFile"
        }

        Write-Host "Source file: $sourceFile"
    }

    Write-Host "Publishing $name version $version ($Target)"

    # Debug deployment.
    if ($Target -eq 'Debug') {
        if ([string]::IsNullOrWhiteSpace($DeployPath)) {
            $DeployPath = Join-Path $ValheimPath 'BepInEx\plugins'
        }

        $pluginFolder = Join-Path $DeployPath $name
        New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null

        Copy-Item $dllPath $pluginFolder -Force

        $pdb = Join-Path $TargetPath "$name.pdb"
        if (Test-Path $pdb) {
            Copy-Item $pdb $pluginFolder -Force
        }

        $mdb = Join-Path $TargetPath "$name.dll.mdb"
        if (Test-Path $mdb) {
            Copy-Item $mdb $pluginFolder -Force
        }

        # Update Debug manifest version.
        $manifestSource = Join-Path $SourcePath 'manifest.json'
        $manifestDestination = Join-Path $pluginFolder 'manifest.json'

        $manifest = [System.IO.File]::ReadAllText($manifestSource) |
        ConvertFrom-Json

        $manifest.version_number = $version

        $manifestJson = $manifest | ConvertTo-Json -Depth 10

        [System.IO.File]::WriteAllText(
            $manifestDestination,
            $manifestJson + [Environment]::NewLine,
            $utf8NoBom
        )

        Write-Host "Debug manifest updated: $manifestDestination ($version)"
        Write-Host "Debug plugin deployed to: $pluginFolder"
    }

    # Rebuild the package folder.
    if (Test-Path $PackagePath) {
        Remove-Item $PackagePath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $PluginsPath -Force | Out-Null

    # Copy the compiled plugin.
    Copy-Item $dllPath $PluginsPath -Force

    # Write README without BOM.
    $readmeSource = Join-Path $SourcePath 'README.md'
    $readmeDestination = Join-Path $PackagePath 'README.md'
    $readmeContent = [System.IO.File]::ReadAllText($readmeSource)

    [System.IO.File]::WriteAllText(
        $readmeDestination,
        $readmeContent,
        $utf8NoBom
    )

    # Copy icon.
    Copy-Item `
    (Join-Path $SourcePath 'icon.png') `
    (Join-Path $PackagePath 'icon.png') -Force

    # Update packaged manifest version and write without BOM.
    $manifestSource = Join-Path $SourcePath 'manifest.json'
    $manifestDestination = Join-Path $PackagePath 'manifest.json'

    $manifest = [System.IO.File]::ReadAllText($manifestSource) |
    ConvertFrom-Json

    $manifest.version_number = $version

    $manifestJson = $manifest | ConvertTo-Json -Depth 10

    [System.IO.File]::WriteAllText(
        $manifestDestination,
        $manifestJson + [Environment]::NewLine,
        $utf8NoBom
    )

    if (!(Test-Path $manifestDestination)) {
        throw "Manifest was not created: $manifestDestination"
    }

    Write-Host "Package manifest updated: $manifestDestination ($version)"

    # Create versioned ZIP in the repository's Releases folder.
    New-Item -ItemType Directory -Path $ReleasesPath -Force | Out-Null

    $zipPath = Join-Path $ReleasesPath "$name-$version.zip"

    Compress-Archive `
        -Path "$PackagePath\*" `
        -DestinationPath $zipPath `
        -Force

    Write-Host ''
    Write-Host "Package folder: $PackagePath"
    Write-Host "Release ZIP:    $zipPath"
}
finally {
    Pop-Location
}
