[CmdletBinding()]
param(
[string]$InstallDirectory,
[string]$ShortcutDirectory
)

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'Desktop-Common.ps1')
    if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
        if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'Windows could not locate your local application data folder.' }
        $InstallDirectory = Join-Path $env:LOCALAPPDATA 'ServerAppManager'
    }
    if ([string]::IsNullOrWhiteSpace($ShortcutDirectory)) { $ShortcutDirectory = [Environment]::GetFolderPath('Desktop') }
    $sourceDirectory = Split-Path -Parent $PSScriptRoot
    $project = Join-Path $sourceDirectory 'src\MyServerManager\ServerAppManager.csproj'
    $dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dotnetCommand) { throw 'Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0, then run setup again.' }
    $dotnet = $dotnetCommand.Source
    $installedSdks = @(& $dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0 -or -not @($installedSdks | Where-Object { $_ -match '^10\.' }).Count) {
        throw 'Setup needs the .NET 10 SDK. Install it from https://dotnet.microsoft.com/download/dotnet/10.0, then run setup again.'
    }
    $InstallDirectory = Resolve-ManagerPath $InstallDirectory 'The installation folder'
    $ShortcutDirectory = Resolve-ManagerPath $ShortcutDirectory 'The desktop folder'
    $configurationPath = Join-Path $InstallDirectory 'launcher.json'

    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw 'Open this script from the Server App Manager source folder.'
    }

    $setupMutex = [Threading.Mutex]::new($false, (Get-ManagerSetupMutexName $InstallDirectory))
    $ownsSetupMutex = $false
    try { $ownsSetupMutex = $setupMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsSetupMutex = $true }
    if (-not $ownsSetupMutex) { $setupMutex.Dispose(); throw 'Desktop setup is already running. Wait for it to finish before trying again.' }
    try {

        & $dotnet dev-certs https --check --trust
        if ($LASTEXITCODE -ne 0) {
            & $dotnet dev-certs https --trust
            if ($LASTEXITCODE -ne 0) {
                throw 'Trust the local HTTPS certificate to finish creating the shortcut.'
            }
        }

        $settings = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $sourceDirectory 'src\MyServerManager\appsettings.json') | ConvertFrom-Json
        $port = Get-ManagerPort $settings.Manager.LocalPort

        $sourceState = Join-Path $sourceDirectory 'src\MyServerManager\.manager-state'
        $stateDirectory = Join-Path $env:LOCALAPPDATA 'MyServerManager'
        if (Test-Path -LiteralPath $sourceState -PathType Container) {
            $stateDirectory = $sourceState
        }
        if (Test-Path -LiteralPath $configurationPath -PathType Leaf) {
            $existing = Read-ManagerLauncherConfiguration $configurationPath
            $stateDirectory = $existing.StateDirectory
            $port = $existing.Port
        }
        $stateDirectory = Resolve-ManagerPath $stateDirectory 'The saved data folder'

        $releaseDirectory = Join-Path $InstallDirectory ('app-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
        & $dotnet publish $project -c Release -p:UseAppHost=false -o $releaseDirectory
        if ($LASTEXITCODE -ne 0) {
            throw 'The dashboard could not be published. See the build output above.'
        }

        $launcherPath = Join-Path $InstallDirectory 'Start-ServerAppManager.ps1'
        Write-ManagerAtomicText (Join-Path $InstallDirectory 'Desktop-Common.ps1') ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Desktop-Common.ps1')))
        Write-ManagerAtomicText $launcherPath ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Start-ServerAppManager.ps1')))
        $configuration = [ordered]@{
            ApplicationDirectory = $releaseDirectory
            StateDirectory = $stateDirectory
            Port = $port
        }

        New-Item -ItemType Directory -Path $ShortcutDirectory -Force | Out-Null
        $shortcutPath = Join-Path $ShortcutDirectory 'Server App Manager.lnk'
        $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $shell = New-Object -ComObject WScript.Shell
        try {
            $shortcut = $shell.CreateShortcut($shortcutPath)
            $shortcut.TargetPath = $windowsPowerShell
            $shortcut.Arguments = '-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File ' + (ConvertTo-ManagerProcessArgument $launcherPath)
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

        Write-ManagerAtomicText $configurationPath ($configuration | ConvertTo-Json)
        Write-Host "Created $shortcutPath"
        Write-Host 'Double-click Server App Manager on your desktop to open the dashboard.'
    } finally {
        if ($ownsSetupMutex) { $setupMutex.ReleaseMutex() }
        $setupMutex.Dispose()
    }
} catch {
    Write-Host ('Setup could not finish: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
