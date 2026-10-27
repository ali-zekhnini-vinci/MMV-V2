<#
.SYNOPSIS
  Tests de scripts/Invoke-MmvSqlScript.ps1 (O-03-16) avec un psql SIMULÉ : aucune connexion, aucune base touchée.

.DESCRIPTION
  Un faux psql.exe (compilé à la volée, analyse de la ligne de commande identique à celle d'un exécutable natif) journalise
  les arguments et l'entrée standard qu'il reçoit réellement, puis imite psql : identité du serveur, application (-f),
  lecture des migrations. Comme le vrai PostgreSQL, il refuse « __EFMigrationsHistory » sans guillemets.
  Le script testé est exécuté dans un processus Windows PowerShell 5.1 distinct, avec un APPDATA temporaire contenant
  des user-secrets fictifs (aucun secret réel lu).

.EXAMPLE
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Invoke-MmvSqlScript.Tests.ps1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Invoke-MmvSqlScript.Tests.ps1 -Target <autre script>
#>
param([string] $Target = (Join-Path $PSScriptRoot '..\Invoke-MmvSqlScript.ps1'))
$ErrorActionPreference = 'Stop'
$Target = (Resolve-Path -LiteralPath $Target).Path

$work = Join-Path ([IO.Path]::GetTempPath()) ("mmv-sqlscript-tests-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$fakePsql = Join-Path $work 'psql.exe'
Add-Type -OutputType ConsoleApplication -OutputAssembly $fakePsql -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Text;
public static class FakePsql {
    public static int Main(string[] args) {
        string log = Environment.GetEnvironmentVariable("FAKE_PSQL_LOG");
        int c = Array.IndexOf(args, "-c"), f = Array.IndexOf(args, "-f");
        string sql = c >= 0 ? args[c + 1] : (f >= 0 ? null : Console.In.ReadToEnd());
        File.AppendAllText(log, "ARGS " + string.Join(" | ", args) + "\n" + "PGOPTIONS " + Environment.GetEnvironmentVariable("PGOPTIONS") + "\n" + (sql == null ? "" : "SQL " + sql.Trim() + "\n"), new UTF8Encoding(false));
        if (f >= 0) { Console.WriteLine("START TRANSACTION"); Console.WriteLine("COMMIT"); return 0; }
        if (sql.Contains("current_database()")) { Console.WriteLine(Environment.GetEnvironmentVariable("FAKE_PSQL_IDENTITY")); return 0; }
        if (Environment.GetEnvironmentVariable("FAKE_PSQL_FAIL_READ") == "1" || !sql.Contains("\"__EFMigrationsHistory\"") || !sql.Contains("\"MigrationId\"")) {
            Console.Error.WriteLine("ERREUR:  la relation « __efmigrationshistory » n'existe pas");
            return 1;
        }
        Console.WriteLine("0001_Initial, 0002_AuthIdentity");
        return 0;
    }
}
'@

$sqlFile = Join-Path $work 'migration.sql'
Set-Content -LiteralPath $sqlFile -Value 'SELECT 1;' -Encoding ASCII
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sqlFile).Hash
$secretsDir = Join-Path $work 'appdata\Microsoft\UserSecrets\mmv-fake-secrets'
New-Item -ItemType Directory -Path $secretsDir | Out-Null

function Set-FakeConnection([string] $connection) {
    @{ 'ConnectionStrings:MmvTest' = $connection } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $secretsDir 'secrets.json') -Encoding UTF8
}

function Invoke-Target([hashtable] $environment, [string] $sha = $hash) {
    $logFile = Join-Path $work ("psql-" + [Guid]::NewGuid().ToString('N') + '.log')
    # Processus distinct lancé comme par l'opérateur (-File) : sorties capturées au niveau du processus, sans
    # redirection PowerShell qui changerait le traitement du flux d'erreur de psql.
    $info = New-Object Diagnostics.ProcessStartInfo 'powershell.exe'
    $info.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$Target`" -SecretsId mmv-fake-secrets -Key ConnectionStrings:MmvTest -Database mmv_v2_test -Script `"$sqlFile`" -Sha256 $sha -Psql `"$fakePsql`""
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.RedirectStandardInput = $true
    $vars = @{ APPDATA = (Join-Path $work 'appdata'); FAKE_PSQL_LOG = $logFile; FAKE_PSQL_IDENTITY = 'mmv_v2_test|mmv_v2_test|5433|180006'; FAKE_PSQL_FAIL_READ = '0' }
    foreach ($k in $environment.Keys) { $vars[$k] = $environment[$k] }
    foreach ($k in $vars.Keys) { $info.EnvironmentVariables[$k] = $vars[$k] }
    $process = [Diagnostics.Process]::Start($info)
    $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Délai dépassé.' }
    $calls = if (Test-Path -LiteralPath $logFile) { Get-Content -LiteralPath $logFile -Encoding UTF8 } else { @() }
    [pscustomobject]@{ Output = ($stdout.Result + $stderr.Result).Trim(); Exit = $process.ExitCode; Calls = $calls }
}

$failures = 0
function Assert([bool] $condition, [string] $label, [string] $detail = '') {
    if ($condition) { Write-Output "PASS $label" }
    else { $script:failures++; Write-Output "FAIL $label$(if ($detail) { "`n----`n$detail`n----" })" }
}

$expectedRead = 'SQL SELECT string_agg("MigrationId", '', '' ORDER BY "MigrationId") FROM "__EFMigrationsHistory";'
try {
    Write-Output "Cible : $Target (PowerShell $($PSVersionTable.PSVersion))"
    Set-FakeConnection 'Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test;Password=fictif'

    # 1. Application normale : lecture de contrôle reçue intacte par psql, liste affichée, aucun message d'erreur.
    $r = Invoke-Target @{}
    Assert ($r.Exit -eq 0) 'application : code de sortie 0' $r.Output
    Assert ($r.Output -match 'PRECHECK OK : mmv_v2_test / mmv_v2_test / port 5433') 'application : contrôle d''identité' $r.Output
    Assert (@($r.Calls | Where-Object { $_ -like 'ARGS *-f*' }).Count -eq 1) 'application : script transmis une seule fois par -f' ($r.Calls -join "`n")
    Assert ($r.Output -match 'APPLIQUE : exit 0') 'application : COMMIT signalé' $r.Output
    Assert ($r.Calls -contains $expectedRead) 'O-03-16 : requête de lecture reçue avec ses guillemets doubles' ($r.Calls -join "`n")
    Assert ($r.Output -match 'MIGRATIONS : 0001_Initial, 0002_AuthIdentity') 'O-03-16 : liste des migrations affichée' $r.Output
    Assert ($r.Output -notmatch 'ERREUR|n''existe pas|AVERTISSEMENT') 'O-03-16 : aucune erreur après COMMIT' $r.Output
    Assert (($r.Calls -contains 'PGOPTIONS -c lock_timeout=5s') -and ($r.Output -match 'LOCK_TIMEOUT : 5s')) '04-B : délai de verrou transmis à psql (défaut 5s)' (($r.Calls -join "`n") + "`n" + $r.Output)

    # 2. Lecture de contrôle en échec après COMMIT : avertissement explicite, application non présentée comme annulée.
    $r = Invoke-Target @{ FAKE_PSQL_FAIL_READ = '1' }
    Assert ($r.Exit -eq 0) 'lecture en échec : code de sortie 0 (COMMIT déjà effectué)' $r.Output
    Assert ($r.Output -match 'AVERTISSEMENT : script applique \(COMMIT\), lecture de controle impossible \(psql exit 1\)') 'lecture en échec : avertissement' $r.Output
    Assert ($r.Output -notmatch 'transaction annulee') 'lecture en échec : pas d''annonce d''annulation' $r.Output

    # 3. Garde-fous existants (non-régression) : refus AVANT tout appel à psql.
    $r = Invoke-Target @{} ('0' * 64)
    Assert ($r.Exit -ne 0 -and $r.Output -match 'Empreinte refusee' -and $r.Calls.Count -eq 0) 'garde-fou : empreinte différente, psql jamais appelé' "$($r.Output)`n$($r.Calls -join "`n")"
    Set-FakeConnection 'Host=localhost;Port=5432;Database=mmv_v2_test;Username=mmv_v2_test;Password=fictif'
    $r = Invoke-Target @{}
    Assert ($r.Exit -ne 0 -and $r.Output -match 'Port refuse' -and $r.Calls.Count -eq 0) 'garde-fou : port 5432 refusé, psql jamais appelé' "$($r.Output)`n$($r.Calls -join "`n")"
    Set-FakeConnection 'Host=db.example.org;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test;Password=fictif'
    $r = Invoke-Target @{}
    Assert ($r.Exit -ne 0 -and $r.Output -match 'Hote refuse' -and $r.Calls.Count -eq 0) 'garde-fou : hôte non loopback refusé, psql jamais appelé' "$($r.Output)`n$($r.Calls -join "`n")"
    Set-FakeConnection 'Host=localhost;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev;Password=fictif'
    $r = Invoke-Target @{}
    Assert ($r.Exit -ne 0 -and $r.Output -match 'Cible refusee' -and $r.Calls.Count -eq 0) 'garde-fou : base différente de -Database refusée, psql jamais appelé' "$($r.Output)`n$($r.Calls -join "`n")"
    Set-FakeConnection 'Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test;Password=fictif'
    $r = Invoke-Target @{ FAKE_PSQL_IDENTITY = 'mmv_v2_test|mmv_v2_test|5433|170004' }
    Assert ($r.Exit -ne 0 -and $r.Output -match 'Identite serveur refusee' -and @($r.Calls | Where-Object { $_ -like 'ARGS *-f*' }).Count -eq 0) 'garde-fou : PostgreSQL 17 refusé, script jamais transmis' "$($r.Output)`n$($r.Calls -join "`n")"
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures) { Write-Output "ÉCHECS : $failures"; exit 1 }
Write-Output 'TOUS LES TESTS RÉUSSIS'
exit 0
