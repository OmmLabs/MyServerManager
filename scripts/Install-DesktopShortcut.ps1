[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'ServerAppManager'),
    [string]$ShortcutDirectory = ([Environment]::GetFolderPath('Desktop'))
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = Split-Path -Parent $PSScriptRoot
$project = Join-Path $sourceDirectory 'src\MyServerManager\ServerAppManager.csproj'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
$configurationPath = Join-Path $InstallDirectory 'launcher.json'

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw 'Open this script from the Server App Manager source folder.'
}
if ([string]::IsNullOrWhiteSpace($ShortcutDirectory)) {
    throw 'Windows could not locate your desktop folder.'
}

& $dotnet dev-certs https --check --trust
if ($LASTEXITCODE -ne 0) {
    & $dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) {
        throw 'Trust the local HTTPS certificate to finish creating the shortcut.'
    }
}

$settings = Get-Content -Raw -LiteralPath (Join-Path $sourceDirectory 'src\MyServerManager\appsettings.json') | ConvertFrom-Json
$port = [int]$settings.Manager.LocalPort
if ($port -lt 1 -or $port -gt 65535) {
    throw 'Set Manager:LocalPort to a value between 1 and 65535.'
}

$sourceState = Join-Path $sourceDirectory 'src\MyServerManager\.manager-state'
$stateDirectory = Join-Path $env:LOCALAPPDATA 'MyServerManager'
if (Test-Path -LiteralPath $sourceState -PathType Container) {
    $stateDirectory = $sourceState
}
if (Test-Path -LiteralPath $configurationPath -PathType Leaf) {
    $existing = Get-Content -Raw -LiteralPath $configurationPath | ConvertFrom-Json
    if ([IO.Path]::IsPathRooted($existing.StateDirectory)) {
        $stateDirectory = $existing.StateDirectory
    }
}

$releaseDirectory = Join-Path $InstallDirectory ('app-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
& $dotnet publish $project -c Release -p:UseAppHost=false -o $releaseDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'The dashboard could not be published. See the build output above.'
}

$launcherPath = Join-Path $InstallDirectory 'Start-ServerAppManager.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-ServerAppManager.ps1') -Destination $launcherPath -Force
$configuration = [ordered]@{
    ApplicationDirectory = $releaseDirectory
    StateDirectory = [IO.Path]::GetFullPath($stateDirectory)
    Port = $port
}
$temporaryConfiguration = $configurationPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
[IO.File]::WriteAllText($temporaryConfiguration, ($configuration | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $configurationPath -PathType Leaf) {
    [IO.File]::Replace($temporaryConfiguration, $configurationPath, $null)
} else {
    [IO.File]::Move($temporaryConfiguration, $configurationPath)
}

New-Item -ItemType Directory -Path $ShortcutDirectory -Force | Out-Null
$shortcutPath = Join-Path $ShortcutDirectory 'Server App Manager.lnk'
$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $windowsPowerShell
    $shortcut.Arguments = '-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $launcherPath + '"'
    $shortcut.WorkingDirectory = $InstallDirectory
    $shortcut.Description = 'Start Server App Manager and open its local dashboard.'
    $shortcut.IconLocation = (Join-Path $env:SystemRoot 'System32\shell32.dll') + ',15'
    $shortcut.Save()
} finally {
    if ($shortcut) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
    }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}

Write-Host "Created $shortcutPath"
Write-Host 'Double-click Server App Manager on your desktop to open the dashboard.'
