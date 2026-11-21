function Resolve-ManagerPath {
    param([string]$Value, [string]$Description)
    if ([string]::IsNullOrWhiteSpace($Value) -or -not [IO.Path]::IsPathRooted($Value)) {
        throw ($Description + ' must be a full Windows folder path.')
    }
    try { return [IO.Path]::GetFullPath($Value) }
    catch { throw ($Description + ' is not a valid Windows folder path.') }
}

function Get-ManagerPort {
    param($Value)
    $number = 0
    if (-not [int]::TryParse([string]$Value, [ref]$number) -or $number -lt 1 -or $number -gt 65535) {
        throw 'The local port must be a whole number between 1 and 65535.'
    }
    return $number
}

function Read-ManagerLauncherConfiguration {
    param([string]$Path)
    try { $saved = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw ('Cannot read the saved desktop settings at ' + $Path + '. Keep that file and repair it before reinstalling.') }
    return [pscustomobject]@{
        ApplicationDirectory = Resolve-ManagerPath $saved.ApplicationDirectory 'The installed application folder'
        StateDirectory = Resolve-ManagerPath $saved.StateDirectory 'The saved data folder'
        Port = Get-ManagerPort $saved.Port
    }
}

function Write-ManagerAtomicText {
    param([string]$Path, [string]$Text)
    $temporary = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($false))
        if ([IO.File]::Exists($Path)) {
            # PowerShell 5.1 coerces $null to an empty string for this overload.
            [IO.File]::Replace($temporary, $Path, [System.Management.Automation.Language.NullString]::Value)
        } else {
            [IO.File]::Move($temporary, $Path)
        }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function ConvertTo-ManagerProcessArgument {
    param([AllowEmptyString()][string]$Value)
    # Start-Process joins ArgumentList on Windows PowerShell. Escape each word
    # using Windows argv rules, including a backslash before the closing quote.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Get-ManagerSetupMutexName {
    param([string]$InstallDirectory)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($InstallDirectory.TrimEnd([char[]]'\/').ToUpperInvariant())) }
    finally { $hash.Dispose() }
    return 'Local\ServerAppManager-Setup-' + ([BitConverter]::ToString($bytes)).Replace('-', '')
}
