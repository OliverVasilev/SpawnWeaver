<#
.SYNOPSIS
    SpawnWeaver one-command quickstart for Windows / PowerShell.

.DESCRIPTION
    Gets you from a fresh clone to "two players moving" with a single command. It:
      1. Starts the SpawnWeaver API (via `dotnet run`, or `-Docker` for Docker Compose).
      2. Waits for /health to report ok.
      3. Signs in (or signs up) a local developer account.
      4. Creates a project and grabs its public key (pk_...).
      5. Writes the Godot SDK config (spawnweaver.cfg) so the bundled examples
         auto-connect - no copy/paste of keys required.
      6. Leaves the server running until you press Ctrl+C.

    Re-running reuses the same account and project (idempotent), so the public key
    stays stable. Generated credentials are stored in .quickstart/credentials.json
    (git-ignored) - local dev only.

.PARAMETER Docker
    Start the API with Docker Compose (deploy/docker-compose.yml, port 8080)
    instead of `dotnet run` (port 5159).

.PARAMETER Port
    Override the HTTP port to talk to. Defaults to 5159 (dotnet) or 8080 (Docker).

.PARAMETER NoServe
    Provision only, then exit instead of keeping the server in the foreground.
    Useful for CI or when you start the server some other way.

.EXAMPLE
    ./quickstart.ps1

.EXAMPLE
    ./quickstart.ps1 -Docker
#>
[CmdletBinding()]
param(
    [switch] $Docker,
    [int]    $Port = 0,
    [switch] $NoServe
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot   = $PSScriptRoot
$CfgPath    = Join-Path $RepoRoot 'sdk/godot-gdscript/addons/multiplayer_service/spawnweaver.cfg'
$StateDir   = Join-Path $RepoRoot '.quickstart'
$CredPath   = Join-Path $StateDir 'credentials.json'

if ($Port -le 0) { $Port = if ($Docker) { 8080 } else { 5159 } }
$BaseUrl = "http://localhost:$Port"
$WsUrl   = "ws://127.0.0.1:$Port/connect"

function Write-Step([string] $Message) { Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok([string]   $Message) { Write-Host "    $Message" -ForegroundColor Green }

# --- 1. Start the server -----------------------------------------------------
$serverProc = $null
$dockerStarted = $false

function Test-Health {
    try {
        $r = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 3 -ErrorAction Stop
        return ($null -ne $r -and $r.status -eq 'ok')
    } catch { return $false }
}

if (Test-Health) {
    Write-Step "API already healthy at $BaseUrl - reusing it."
}
elseif ($Docker) {
    Write-Step "Starting API with Docker Compose (this builds on first run)..."
    & docker compose -f (Join-Path $RepoRoot 'deploy/docker-compose.yml') up --build -d
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed (is Docker running?)." }
    $dockerStarted = $true
}
else {
    Write-Step "Starting API with 'dotnet run' on port $Port..."
    $logDir = $StateDir
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $logFile = Join-Path $logDir 'server.log'
    $serverProc = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', (Join-Path $RepoRoot 'src/Platform.Api'),
                        '--urls', "http://localhost:$Port") `
        -PassThru -NoNewWindow -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err"
    Write-Ok "Server PID $($serverProc.Id) - logs: $logFile"
}

try {
    # --- 2. Wait for health --------------------------------------------------
    Write-Step "Waiting for $BaseUrl/health ..."
    $deadline = (Get-Date).AddSeconds(120)
    while (-not (Test-Health)) {
        if ((Get-Date) -gt $deadline) { throw "API did not become healthy within 120s. Check the server log." }
        if ($null -ne $serverProc -and $serverProc.HasExited) { throw "Server process exited early. Check $logFile.err" }
        Start-Sleep -Milliseconds 800
    }
    Write-Ok "API is healthy."

    # --- 3. Sign in or sign up a local dev account ---------------------------
    New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
    if (Test-Path $CredPath) {
        $creds = Get-Content $CredPath -Raw | ConvertFrom-Json
    } else {
        $suffix = ([guid]::NewGuid().ToString('N')).Substring(0, 8)
        $creds = [pscustomobject]@{
            email     = "dev-$suffix@spawnweaver.local"
            password  = "sw-" + ([guid]::NewGuid().ToString('N'))
            projectId = ""
            publicKey = ""
        }
    }

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    function Invoke-Api([string] $Method, [string] $Path, $Body) {
        $args = @{ Uri = "$BaseUrl$Path"; Method = $Method; WebSession = $session; ErrorAction = 'Stop' }
        if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Compress); $args.ContentType = 'application/json' }
        return Invoke-RestMethod @args
    }

    Write-Step "Authenticating local developer account ($($creds.email))..."
    $signedIn = $false
    try {
        Invoke-Api 'Post' '/api/auth/signin' @{ email = $creds.email; password = $creds.password } | Out-Null
        $signedIn = $true
        Write-Ok "Signed in."
    } catch { }

    if (-not $signedIn) {
        Invoke-Api 'Post' '/api/auth/signup' @{ email = $creds.email; displayName = 'Quickstart Dev'; password = $creds.password } | Out-Null
        Write-Ok "Created account."
    }

    # --- 4. Reuse or create a project ----------------------------------------
    $publicKey = $null
    if ($creds.projectId) {
        try {
            $existing = Invoke-Api 'Get' "/api/projects/$($creds.projectId)" $null
            $publicKey = $existing.publicKey
            Write-Ok "Reusing project $($creds.projectId)."
        } catch { }
    }

    if (-not $publicKey) {
        Write-Step "Creating project 'Quickstart Game'..."
        $proj = Invoke-Api 'Post' '/api/projects' @{ name = 'Quickstart Game' }
        $publicKey = $proj.publicKey
        $creds.projectId = $proj.id
        $creds.publicKey = $publicKey
        Write-Ok "Project $($proj.id) created."
    }

    $creds | ConvertTo-Json | Set-Content -Path $CredPath -Encoding utf8

    # --- 5. Write the Godot SDK config ---------------------------------------
    Write-Step "Writing Godot SDK config..."
    $cfg = @"
[project]

public_key="$publicKey"
server_url="$WsUrl"
environment="Development"
debug_enabled=false
"@
    Set-Content -Path $CfgPath -Value $cfg -Encoding utf8
    Write-Ok "Wrote $CfgPath"

    # --- Summary -------------------------------------------------------------
    Write-Host ""
    Write-Host "SpawnWeaver is ready." -ForegroundColor Green
    Write-Host "  API:        $BaseUrl"
    Write-Host "  WebSocket:  $WsUrl"
    Write-Host "  Public key: $publicKey"
    Write-Host "  Dashboard:  $BaseUrl/dashboard"
    Write-Host ""
    Write-Host "Next:" -ForegroundColor Cyan
    Write-Host "  1. Open  sdk/godot-gdscript  in Godot 4.3+"
    Write-Host "  2. Debug -> Run Multiple Instances -> 2 instances, then press Play"
    Write-Host "  3. Both windows are pre-configured - just click Connect."
    Write-Host ""

    if ($NoServe) {
        Write-Ok "Provisioning done (-NoServe). Server left as-is."
        return
    }

    if ($dockerStarted) {
        Write-Host "Server runs in Docker. Stop it with:" -ForegroundColor DarkGray
        Write-Host "  docker compose -f deploy/docker-compose.yml down" -ForegroundColor DarkGray
        return
    }

    if ($null -ne $serverProc) {
        Write-Host "Server is running. Press Ctrl+C to stop it." -ForegroundColor DarkGray
        Wait-Process -Id $serverProc.Id
    }
}
finally {
    if ($null -ne $serverProc -and -not $serverProc.HasExited) {
        Write-Host "`nStopping server (PID $($serverProc.Id))..." -ForegroundColor DarkGray
        try { Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue } catch { }
    }
}
