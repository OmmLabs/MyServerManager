[CmdletBinding()]
param(
    [string]$ConfigurationPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConfigurationPath)) {
    $ConfigurationPath = Join-Path $PSScriptRoot 'launcher.json'
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Net.Http

$script:dashboardProcess = $null
$tray = $null
$menu = $null
$timer = $null
$mutex = $null
$ownsMutex = $false
$client = $null

function Test-DashboardReady {
    try {
        $response = $client.GetAsync($script:dashboardUrl + '/Account/Login').GetAwaiter().GetResult()
        try {
            if (-not $response.IsSuccessStatusCode) {
                return $false
            }
            $html = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return $html -match '<title>[^<]*(Server App Manager|My Server Manager)'
        } finally {
            $response.Dispose()
        }
    } catch {
        return $false
    }
}

function Open-Dashboard {
    Start-Process -FilePath ($script:dashboardUrl + '/')
}

try {
    $configuration = Get-Content -Raw -LiteralPath $ConfigurationPath | ConvertFrom-Json
    $port = [int]$configuration.Port
    if ($port -lt 1 -or $port -gt 65535) {
        throw 'The saved local port is invalid. Run Create-Desktop-Shortcut.cmd again.'
    }

    $script:dashboardUrl = 'https://localhost:' + $port
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    $handler.MaxAutomaticRedirections = 3
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(2)
    $mutex = [Threading.Mutex]::new($false, 'Local\ServerAppManager-' + $port)
    try {
        $ownsMutex = $mutex.WaitOne(0)
    } catch [Threading.AbandonedMutexException] {
        $ownsMutex = $true
    }

    if (-not $ownsMutex) {
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        do {
            if (Test-DashboardReady) {
                Open-Dashboard
                return
            }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $deadline)
        throw 'Server App Manager is starting or closing. Try the shortcut again in a moment.'
    }

    if (Test-DashboardReady) {
        Open-Dashboard
        return
    }

    $applicationDirectory = [IO.Path]::GetFullPath($configuration.ApplicationDirectory)
    $stateDirectory = [IO.Path]::GetFullPath($configuration.StateDirectory)
    if (-not (Test-Path -LiteralPath (Join-Path $applicationDirectory 'MyServerManager.dll') -PathType Leaf)) {
        throw 'The installed dashboard is missing. Run Create-Desktop-Shortcut.cmd again.'
    }

    $logDirectory = Join-Path $stateDirectory 'launcher-logs'
    New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
    $logName = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $outputLog = Join-Path $logDirectory ($logName + '.log')
    $errorLog = Join-Path $logDirectory ($logName + '-errors.log')
    $arguments = @(
        'MyServerManager.dll',
        '--Manager:Transport=ssh',
        ('--Manager:LocalPort=' + $port),
        ('--Manager:DataDirectory="' + $stateDirectory + '"')
    )
    $startOptions = @{
        FilePath = (Get-Command dotnet -ErrorAction Stop).Source
        ArgumentList = $arguments
        WorkingDirectory = $applicationDirectory
        WindowStyle = 'Hidden'
        RedirectStandardOutput = $outputLog
        RedirectStandardError = $errorLog
        PassThru = $true
    }
    $script:dashboardProcess = Start-Process @startOptions

    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $ready = $false
    do {
        $script:dashboardProcess.Refresh()
        if ($script:dashboardProcess.HasExited) {
            throw ('The dashboard could not start. See ' + $errorLog)
        }
        if (Test-DashboardReady) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $ready) {
        throw ('The dashboard did not become ready. Check the local HTTPS certificate and ' + $outputLog)
    }

    $tray = [Windows.Forms.NotifyIcon]::new()
    $tray.Icon = [Drawing.SystemIcons]::Application
    $tray.Text = 'Server App Manager'
    $menu = [Windows.Forms.ContextMenuStrip]::new()
    $open = $menu.Items.Add('Open dashboard')
    $open.Add_Click({ Open-Dashboard })
    [void]$menu.Items.Add([Windows.Forms.ToolStripSeparator]::new())
    $quit = $menu.Items.Add('Quit Server App Manager')
    $quit.Add_Click({
        $answer = [Windows.Forms.MessageBox]::Show(
            'Close Server App Manager? Active dashboard requests may be interrupted.',
            'Server App Manager', [Windows.Forms.MessageBoxButtons]::YesNo,
            [Windows.Forms.MessageBoxIcon]::Question, [Windows.Forms.MessageBoxDefaultButton]::Button2)
        if ($answer -eq [Windows.Forms.DialogResult]::Yes) {
            [Windows.Forms.Application]::ExitThread()
        }
    })
    $tray.ContextMenuStrip = $menu
    $tray.Add_DoubleClick({ Open-Dashboard })
    $tray.Visible = $true

    $timer = [Windows.Forms.Timer]::new()
    $timer.Interval = 2000
    $timer.Add_Tick({
        $script:dashboardProcess.Refresh()
        if ($script:dashboardProcess.HasExited) {
            [Windows.Forms.Application]::ExitThread()
        }
    })
    $timer.Start()
    Open-Dashboard
    [Windows.Forms.Application]::Run()
} catch {
    $errorMessage = $_.Exception.Message
    [IO.File]::AppendAllText((Join-Path $PSScriptRoot 'launcher-error.log'),
        ([DateTime]::UtcNow.ToString('O') + ' ' + $errorMessage + [Environment]::NewLine))
    [void][Windows.Forms.MessageBox]::Show(
        $errorMessage, 'Server App Manager',
        [Windows.Forms.MessageBoxButtons]::OK, [Windows.Forms.MessageBoxIcon]::Error)
    exit 1
} finally {
    if ($timer) { $timer.Stop(); $timer.Dispose() }
    if ($tray) { $tray.Visible = $false; $tray.Dispose() }
    if ($menu) { $menu.Dispose() }
    if ($script:dashboardProcess) {
        $script:dashboardProcess.Refresh()
        if (-not $script:dashboardProcess.HasExited) {
            Stop-Process -Id $script:dashboardProcess.Id -ErrorAction SilentlyContinue
        }
        $script:dashboardProcess.Dispose()
    }
    if ($client) { $client.Dispose() }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    if ($mutex) { $mutex.Dispose() }
}
