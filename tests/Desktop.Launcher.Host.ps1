param([string]$BrowserLog, [string]$LauncherPath, [switch]$ExitAfterTest)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
function Start-Process {
    param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory, [string]$WindowStyle,
        [string]$RedirectStandardOutput, [string]$RedirectStandardError, [switch]$PassThru)
    if ($FilePath -like 'https://localhost:*') { Add-Content -LiteralPath $BrowserLog -Value $FilePath; return }
    Microsoft.PowerShell.Management\Start-Process @PSBoundParameters
}
$exitTimer = [Windows.Forms.Timer]::new()
try {
    if ($ExitAfterTest) {
        $exitTimer.Interval = 30000
        $exitTimer.Add_Tick({ [Windows.Forms.Application]::ExitThread() })
        $exitTimer.Start()
    }
    & $LauncherPath
} finally { $exitTimer.Stop(); $exitTimer.Dispose() }
