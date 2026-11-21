[CmdletBinding()]
param([switch]$UnitOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/Desktop-Common.ps1')
$testRoot = Join-Path $repo ('artifacts/desktop-tests/' + [guid]::NewGuid().ToString('N') + ' space ' + [char]0x00E9)
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$script:passed = 0
function Assert-Desktop($Condition, [string]$Name) {
    if (-not $Condition) { throw ('FAILED: ' + $Name) }
    $script:passed++
    Write-Output ('Passed: ' + $Name)
}
function Assert-DesktopThrows([scriptblock]$Action, [string]$Name) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-Desktop $threw $Name
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'scripts') -Filter '*.ps1') {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-Desktop ($parseErrors.Count -eq 0) ($file.Name + ' parses in PowerShell ' + $PSVersionTable.PSVersion)
}
$configPath = Join-Path $testRoot 'launcher.json'
$configuration = @{ ApplicationDirectory = (Join-Path $testRoot 'first app'); StateDirectory = (Join-Path $testRoot 'saved state') + '\'; Port = 7311 }
Write-ManagerAtomicText $configPath ($configuration | ConvertTo-Json)
Assert-Desktop ([IO.File]::Exists($configPath)) 'Fresh settings creation'
$configuration.ApplicationDirectory = Join-Path $testRoot 'second app'
Write-ManagerAtomicText $configPath ($configuration | ConvertTo-Json)
$saved = Read-ManagerLauncherConfiguration $configPath
Assert-Desktop ($saved.ApplicationDirectory -eq $configuration.ApplicationDirectory) 'Existing settings replaced under Windows PowerShell'
Assert-Desktop ($saved.StateDirectory -eq $configuration.StateDirectory) 'UTF-8 state path and trailing slash survive'
Assert-Desktop ($saved.Port -eq 7311) 'Custom local port survives'
Assert-Desktop (@(Get-ChildItem -LiteralPath $testRoot -Filter '*.tmp').Count -eq 0) 'Atomic save leaves no temporary file'

$before = [IO.File]::ReadAllText($configPath)
$locked = [IO.File]::Open($configPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try { Assert-DesktopThrows { Write-ManagerAtomicText $configPath '{"changed":true}' } 'A locked destination fails safely' }
finally { $locked.Dispose() }
Assert-Desktop ([IO.File]::ReadAllText($configPath) -eq $before) 'A failed replacement preserves saved settings'
Assert-Desktop (@(Get-ChildItem -LiteralPath $testRoot -Filter '*.tmp').Count -eq 0) 'Failed replacement cleans only its temporary file'
foreach ($value in @('', 'relative/path')) {
    Assert-DesktopThrows { Resolve-ManagerPath $value 'Test folder' } 'Invalid saved path rejected'
}
foreach ($value in @('0', '65536', '3.5', 'not-a-port')) {
    Assert-DesktopThrows { Get-ManagerPort $value } 'Invalid saved port rejected'
}
Write-ManagerAtomicText $configPath '{bad-json'
Assert-DesktopThrows { Read-ManagerLauncherConfiguration $configPath } 'Corrupt settings rejected without resetting state'
Write-ManagerAtomicText $configPath ($configuration | ConvertTo-Json)

# Verify Windows quoting against an actual .NET process, rather than comparing
# strings with an implementation of the same quoting algorithm.
$echoRoot = Join-Path $testRoot 'argv echo'
New-Item -ItemType Directory -Path $echoRoot | Out-Null
[IO.File]::WriteAllText((Join-Path $echoRoot 'Echo.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
[IO.File]::WriteAllText((Join-Path $echoRoot 'Program.cs'), 'System.IO.File.WriteAllText(args[0], System.Text.Json.JsonSerializer.Serialize(args.Skip(1).ToArray()));')
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
& $dotnet build (Join-Path $echoRoot 'Echo.csproj') -c Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'The native argument test fixture could not build.' }
$echoOutput = Join-Path $echoRoot 'arguments.json'
$expected = @('a path with spaces', ('Unicode ' + [char]0x00E9), 'C:\ends with slash\', '', 'a"quoted"word', 'two\slashes\\')
$echoArgs = @((Join-Path $echoRoot 'bin/Release/net10.0/Echo.dll'), $echoOutput) + $expected
$echoArguments = ($echoArgs | ForEach-Object { ConvertTo-ManagerProcessArgument $_ }) -join ' '
$echoProcess = Start-Process -FilePath $dotnet -ArgumentList $echoArguments -WindowStyle Hidden -PassThru -Wait
try { Assert-Desktop ($echoProcess.ExitCode -eq 0) 'Actual .NET argument receiver started' } finally { $echoProcess.Dispose() }
$actual = Get-Content -LiteralPath $echoOutput -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Desktop ($actual.Count -eq $expected.Count) 'All arguments reach the native process'
for ($i = 0; $i -lt $expected.Count; $i++) { Assert-Desktop ($actual[$i] -ceq $expected[$i]) ('Native argument round trip ' + ($i + 1)) }

if ($UnitOnly) {
    Write-Output ('Desktop regression checks passed: ' + $script:passed)
    return
}

$zipPath = Join-Path $testRoot 'source.zip'
& git -C $repo archive --format=zip --output=$zipPath HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot make a clean source snapshot.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$extracted = Join-Path $testRoot 'MyServerManager-main/MyServerManager-main'
[IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extracted)
$install = Join-Path $testRoot 'installed app'
$desktop = Join-Path $testRoot 'redirected desktop'
$sourceState = Join-Path $extracted 'src/MyServerManager/.manager-state'
New-Item -ItemType Directory -Path $sourceState -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $sourceState 'keep.txt'), 'existing test registration data')
$powerShell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
function Invoke-DesktopInstaller {
    $argsToSend = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $extracted 'scripts/Install-DesktopShortcut.ps1'), '-InstallDirectory', $install, '-ShortcutDirectory', $desktop)
    $arguments = ($argsToSend | ForEach-Object { ConvertTo-ManagerProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $powerShell -ArgumentList $arguments -WorkingDirectory $testRoot -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $testRoot 'setup.log') -RedirectStandardError (Join-Path $testRoot 'setup-errors.log')
    try { if ($process.ExitCode -ne 0) { throw ('Extracted-source setup failed. Inspect ' + (Join-Path $testRoot 'setup.log')) } } finally { $process.Dispose() }
}
Invoke-DesktopInstaller
$installedConfig = Join-Path $install 'launcher.json'
$first = Read-ManagerLauncherConfiguration $installedConfig
Assert-Desktop (Test-Path -LiteralPath (Join-Path $first.ApplicationDirectory 'MyServerManager.dll')) 'Fresh ZIP publishes a real application'
Assert-Desktop ($first.StateDirectory -eq $sourceState) 'First setup preserves existing source state'
Assert-Desktop (Test-Path -LiteralPath (Join-Path $desktop 'Server App Manager.lnk')) 'Shortcut created on redirected desktop'
$first.Port = 7311
$first.StateDirectory = $sourceState + '\'
Write-ManagerAtomicText $installedConfig ($first | ConvertTo-Json)
Invoke-DesktopInstaller
$second = Read-ManagerLauncherConfiguration $installedConfig
Assert-Desktop ($second.ApplicationDirectory -ne $first.ApplicationDirectory) 'Reinstall publishes a new release'
Assert-Desktop ($second.StateDirectory -eq $first.StateDirectory -and $second.Port -eq 7311) 'Reinstall preserves data path and custom port'
Assert-Desktop ([IO.File]::ReadAllText((Join-Path $sourceState 'keep.txt')) -eq 'existing test registration data') 'Reinstall retains the data contents'
$shell = New-Object -ComObject WScript.Shell
$link = $null
try {
    $link = $shell.CreateShortcut((Join-Path $desktop 'Server App Manager.lnk'))
    Assert-Desktop ($link.TargetPath -eq $powerShell -and $link.Arguments.Contains('-WindowStyle Hidden') -and $link.Arguments.Contains('Start-ServerAppManager.ps1')) 'Shortcut targets the installed hidden launcher'
} finally {
    if ($link) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}
$goodConfiguration = [IO.File]::ReadAllText($installedConfig)
Write-ManagerAtomicText $installedConfig '{not-json'
Assert-DesktopThrows { Invoke-DesktopInstaller } 'Reinstall rejects corrupt configuration'
Assert-Desktop ([IO.File]::ReadAllText($installedConfig) -eq '{not-json') 'Failed setup does not overwrite corrupt configuration'
Write-ManagerAtomicText $installedConfig $goodConfiguration
$heldMutex = [Threading.Mutex]::new($true, (Get-ManagerSetupMutexName $install))
try { Assert-DesktopThrows { Invoke-DesktopInstaller } 'A second setup cannot run concurrently' }
finally { $heldMutex.ReleaseMutex(); $heldMutex.Dispose() }
Assert-Desktop ([IO.File]::ReadAllText($installedConfig) -eq $goodConfiguration) 'Blocked concurrent setup preserves the installation'
$savedPath = $env:PATH
try {
    $env:PATH = Join-Path $env:SystemRoot 'System32'
    Assert-DesktopThrows { Invoke-DesktopInstaller } 'Missing SDK produces a failed setup result'
} finally { $env:PATH = $savedPath }
Assert-Desktop ([IO.File]::ReadAllText($installedConfig) -eq $goodConfiguration) 'Missing SDK preserves the installation'
$evidence = @{ TestRoot = $testRoot; InstallDirectory = $install; ExtractedSource = $extracted; Passed = $script:passed }
[IO.File]::WriteAllText((Join-Path $repo 'artifacts/desktop-tests/last-install.json'), ($evidence | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Output ('Desktop install checks passed: ' + $script:passed)
Write-Output ('Evidence: ' + $testRoot)
