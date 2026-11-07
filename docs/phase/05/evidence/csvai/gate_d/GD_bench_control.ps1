param([switch] $Stop, [switch] $Start, [switch] $Down, [switch] $Up)
# GATE D : controle de l'API de BANC (port 7367) pour la recette native : -Stop / -Start (coupure puis reprise de l'API),
# -Down (API relancee avec un point de terminaison Ollama INJOIGNABLE : simule l'indisponibilite d'Ollama sans arreter
# l'Ollama du proprietaire) / -Up (API relancee avec Ollama 127.0.0.1:11434). N'arrete QUE le processus dotnet qui sert
# MMV.Api.dll depuis le dossier du banc (jamais l'API du proprietaire sur 7267).
$ErrorActionPreference = 'Stop'
$bench = Join-Path $PSScriptRoot 'bench'
if ($env:MMV_GD_BENCH) { $bench = $env:MMV_GD_BENCH }
$api = Join-Path $bench 'api'
function Stop-Bench {
    $listen = Get-NetTCPConnection -LocalPort 7367 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $listen) { return }
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($listen.OwningProcess)"
    if ($proc.CommandLine -notlike "*$api*MMV.Api.dll*") { throw "processus 7367 inattendu : refus" }
    Stop-Process -Id $listen.OwningProcess -Force
    for ($i = 0; $i -lt 40 -and (Get-NetTCPConnection -LocalPort 7367 -State Listen -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 250 }
}
function Start-Bench([string] $endpoint) {
    $script = Join-Path $PSScriptRoot 'GD_bench_start-api.ps1'
    $log = Join-Path $bench ("api_" + (Get-Date -Format 'HHmmss') + ".log")
    Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"",
        '-Source', "`"$api`"", '-Target', "`"$api`"", '-Log', "`"$log`"", '-OllamaEndpoint', $endpoint) | Out-Null
    for ($i = 0; $i -lt 120; $i++) {
        try { $r = Invoke-WebRequest -UseBasicParsing -Uri 'https://localhost:7367/api/store' -TimeoutSec 2 -ErrorAction Stop; break } catch { if ($_.Exception.Response) { break } }
        Start-Sleep -Milliseconds 500
    }
    "API de banc relancee (ollama $endpoint)"
}
if ($Stop -or $Down -or $Up) { Stop-Bench; "API de banc arretee" }
if ($Start -or $Up) { Start-Bench 'http://127.0.0.1:11434' }
if ($Down) { Start-Bench 'http://127.0.0.1:11999' }
