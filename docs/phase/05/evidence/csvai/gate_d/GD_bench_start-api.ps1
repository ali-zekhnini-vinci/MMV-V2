param([string] $Source, [string] $Target, [string] $Log, [int] $Https = 7367, [int] $Http = 5367, [string] $StoreCode = 'NATIF-GD',
      [string] $OllamaEndpoint = 'http://127.0.0.1:11434', [string] $Model = 'qwen3.5:4b')
# GATE D (07/11/2026) : API MMV.Api de BANC (copie du build de ce lot) sur mmv_v2_test UNIQUEMENT (port 5433, chaine lue dans
# les user-secrets mmv-v2-postgresql-tests, jamais affichee) ; API du proprietaire (7267) jamais touchee.
# Nouvel assistant d'import ACTIVE pour ce banc seulement (CatalogInterpreter:WizardEnabled=true par variable d'environnement
# du processus ; appsettings.json et .env.development du proprietaire inchanges). Modele qwen3.5:4b.
# -OllamaEndpoint permet de simuler une indisponibilite d'Ollama (adresse injoignable) sans arreter l'Ollama du proprietaire.
$ErrorActionPreference = 'Stop'
foreach ($p in $Https, $Http) { if (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue) { throw "port $p busy" } }
$secrets = Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft\UserSecrets\mmv-v2-postgresql-tests\secrets.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$raw = $secrets.'ConnectionStrings:MmvTest'
$b = [System.Data.Common.DbConnectionStringBuilder]::new(); $b.set_ConnectionString($raw)
if ([string]$b['Host'] -notin @('localhost', '127.0.0.1', '::1') -or [string]$b['Port'] -ne '5433' -or [string]$b['Database'] -ne 'mmv_v2_test' -or [string]$b['Username'] -ne 'mmv_v2_test') { throw 'target refused' }
if ($Source -ne $Target) {
    if (Test-Path $Target) { Remove-Item $Target -Recurse -Force }
    Copy-Item $Source $Target -Recurse
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Mmv = $raw
$env:Auth__LoginAttemptsPerMinute = '10000'
$env:Installation__StoreCode = $StoreCode
$env:ImportAssistant__Enabled = 'false'
$env:CatalogInterpreter__Enabled = 'true'
$env:CatalogInterpreter__WizardEnabled = 'true'
$env:CatalogInterpreter__Mode = 'always'
$env:CatalogInterpreter__Endpoint = $OllamaEndpoint
$env:CatalogInterpreter__Model = $Model
$env:Cors__AllowedOrigins__0 = 'http://tauri.localhost'
$p = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$Target\MMV.Api.dll`"", '--urls', "https://localhost:$Https;http://localhost:$Http") `
    -WorkingDirectory $Target -RedirectStandardOutput $Log -RedirectStandardError "$Log.err" -WindowStyle Hidden -PassThru
Remove-Item Env:ConnectionStrings__Mmv
$raw = $null; $b.Clear()
"API PID $($p.Id) https $Https http $Http store $StoreCode ollama $OllamaEndpoint model $Model log $Log"
