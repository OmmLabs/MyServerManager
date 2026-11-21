$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/Desktop-Common.ps1')
$evidence = Get-Content -LiteralPath (Join-Path $repo 'artifacts/desktop-tests/last-install.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$work = Join-Path $evidence.TestRoot ('launcher smoke ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$configuration = Read-ManagerLauncherConfiguration (Join-Path $evidence.InstallDirectory 'launcher.json')
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$configuration.Port = $listener.LocalEndpoint.Port
$listener.Stop()
$configuration.StateDirectory = (Join-Path $work 'separate data') + '\'
Write-ManagerAtomicText (Join-Path $work 'launcher.json') ($configuration | ConvertTo-Json)
foreach ($name in @('Start-ServerAppManager.ps1', 'Desktop-Common.ps1')) { Copy-Item -LiteralPath (Join-Path $evidence.InstallDirectory $name) -Destination (Join-Path $work $name) }
$browserLog = Join-Path $work 'browser.log'
$powershell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$hostArgs = @('-NoProfile', '-STA', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Desktop.Launcher.Host.ps1'), '-BrowserLog', $browserLog, '-LauncherPath', (Join-Path $work 'Start-ServerAppManager.ps1'))
Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(2)
$url = 'https://localhost:' + $configuration.Port + '/Setup'
$first = $null
$second = $null
try {
    $arguments = (($hostArgs + '-ExitAfterTest') | ForEach-Object { ConvertTo-ManagerProcessArgument $_ }) -join ' '
    $first = Start-Process -FilePath $powershell -ArgumentList $arguments -WorkingDirectory $work -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $work 'first.log') -RedirectStandardError (Join-Path $work 'first-errors.log')
    [void]$first.Handle
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        try { if ($client.GetStringAsync($url).GetAwaiter().GetResult() -match 'Set up your server') { $ready = $true; break } } catch { }
        $first.Refresh()
        if ($first.HasExited) { throw 'The launcher exited before the isolated HTTPS dashboard started.' }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'The installed launcher did not start the HTTPS dashboard.' }
    if (-not (Test-Path -LiteralPath (Join-Path $configuration.StateDirectory 'keys'))) { throw 'The launcher used the wrong state directory.' }
    Write-Output 'Passed: installed app starts over trusted HTTPS with a UTF-8 path, spaces and trailing slash.'
    $arguments = ($hostArgs | ForEach-Object { ConvertTo-ManagerProcessArgument $_ }) -join ' '
    $second = Start-Process -FilePath $powershell -ArgumentList $arguments -WorkingDirectory $work -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $work 'second.log') -RedirectStandardError (Join-Path $work 'second-errors.log')
    [void]$second.Handle
    if (-not $second.WaitForExit(15000) -or $second.ExitCode -ne 0) { throw 'The second launcher did not reopen the running dashboard.' }
    if ($client.GetStringAsync($url).GetAwaiter().GetResult() -notmatch 'Set up your server') { throw 'The second launcher interrupted the first.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $browserOpens = @(Get-Content -LiteralPath $browserLog -ErrorAction SilentlyContinue)
        if ($browserOpens.Count -eq 2) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($browserOpens.Count -ne 2) { throw 'The two clicks did not both open the dashboard.' }
    Write-Output 'Passed: repeated clicks reopen one running dashboard.'
    if (-not $first.WaitForExit(35000) -or $first.ExitCode -ne 0) { throw 'The launcher message loop did not exit cleanly.' }
    $stillRunning = $false
    try { [void]$client.GetStringAsync($url).GetAwaiter().GetResult(); $stillRunning = $true } catch { }
    if ($stillRunning) { throw 'The launcher left its owned dashboard running after exit.' }
    Write-Output 'Passed: exiting the tray loop stops the dashboard owned by that launcher.'
} finally {
    foreach ($process in @($second, $first)) {
        if ($process) {
            $process.Refresh()
            if (-not $process.HasExited) {
                foreach ($child in @(Get-CimInstance Win32_Process -Filter ('ParentProcessId=' + $process.Id))) { Stop-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
                Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            }
            $process.Dispose()
        }
    }
    $client.Dispose()
}
