# MMV V2 - saisie des chaines PostgreSQL de developpement (user-secrets), par le PROPRIETAIRE uniquement.
# Usage (PowerShell du proprietaire) :
#   . .\scripts\Set-MmvSecret.ps1
#   Set-MmvSecret -Id mmv-v2-api              -Key ConnectionStrings:Mmv     -Name mmv_v2_dev
#   Set-MmvSecret -Id mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Name mmv_v2_test
#
# Le mot de passe est saisi masque (Read-Host -AsSecureString) : il n'apparait ni dans une ligne de commande,
# ni dans l'historique PSReadLine, ni dans une transcription, ni dans les arguments ou l'entree standard d'un
# processus. La fonction ecrit directement secrets.json (UTF-8 sans BOM) en conservant les autres cles.
# Ne jamais utiliser `dotnet user-secrets list` (affiche les valeurs) ni `dotnet user-secrets set <cle> <valeur>`.
# Voir docs/phase/01b/IMPLEMENTATION_SPEC.md, section Secrets.

function Set-MmvSecret {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('mmv-v2-api', 'mmv-v2-postgresql-tests')][string]$Id,
        [Parameter(Mandatory)][ValidateSet('ConnectionStrings:Mmv', 'ConnectionStrings:MmvTest')][string]$Key,
        [Parameter(Mandatory)][ValidateSet('mmv_v2_dev', 'mmv_v2_test')][string]$Name
    )
    $ErrorActionPreference = 'Stop'

    # Seules deux combinaisons sont autorisees : l'API lit mmv_v2_dev, les tests PG lisent mmv_v2_test.
    $allowed = @('mmv-v2-api|ConnectionStrings:Mmv|mmv_v2_dev', 'mmv-v2-postgresql-tests|ConnectionStrings:MmvTest|mmv_v2_test')
    if ($allowed -notcontains "$Id|$Key|$Name") { throw "Combinaison refusee : $Id / $Key / $Name. Rien n'a ete ecrit." }

    $secure = Read-Host -AsSecureString "Mot de passe du role $Name"
    if ($secure.Length -eq 0) { $secure.Dispose(); throw "Mot de passe vide. Rien n'a ete ecrit." }

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    try {
        # Le builder echappe ; ' " = et les caracteres non ASCII (aller-retour verifie, Q-B11).
        $builder['Host'] = 'localhost'; $builder['Port'] = '5433'
        $builder['Database'] = $Name; $builder['Username'] = $Name
        $builder['Password'] = [System.Net.NetworkCredential]::new('', $secure).Password

        $dir = Join-Path $env:APPDATA "Microsoft\UserSecrets\$Id"
        $file = Join-Path $dir 'secrets.json'
        New-Item -ItemType Directory -Force $dir | Out-Null

        $data = [ordered]@{}
        if ((Test-Path $file) -and (Get-Item $file).Length -gt 0) {
            # En cas de JSON invalide, l'erreur d'origine (qui peut citer le contenu) n'est pas relayee.
            try { $existing = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json }
            catch { throw "secrets.json illisible pour '$Id'. Rien n'a ete ecrit." }
            $existing.PSObject.Properties | ForEach-Object { $data[$_.Name] = $_.Value }
        }
        $data[$Key] = $builder.ConnectionString
        [System.IO.File]::WriteAllText($file, ($data | ConvertTo-Json -Depth 5), [System.Text.UTF8Encoding]::new($false))
        "OK : cle '$Key' ecrite pour '$Id' (valeur non affichee)."
    }
    finally {
        $builder.Clear(); $secure.Dispose()
        Remove-Variable builder, secure, data, existing -ErrorAction SilentlyContinue
    }
}

# Controle sans afficher de valeur : noms de cles uniquement.
function Show-MmvSecretKeys {
    foreach ($id in 'mmv-v2-api', 'mmv-v2-postgresql-tests') {
        $file = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"
        if (-not (Test-Path $file)) { "$id : absent"; continue }
        # L'erreur de ConvertFrom-Json cite le contenu du fichier : elle n'est jamais relayee.
        try { "$id : " + ((Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop).PSObject.Properties.Name -join ', ') }
        catch { "$id : secrets.json illisible (contenu non affiche)" }
    }
}
