#!/usr/bin/env bash
# COR-CSVAI-05 — essai de sauvegarde / restauration sur base NEUVE distincte, puis bascule, sur le cluster JETABLE (port 55434).
# Jamais 5432 / 5433. Usage : restore_trial.sh <repo> <workdir>
set -u
REPO="$1"; W="$2"; mkdir -p "$W"
BIN="/c/Program Files/PostgreSQL/18/bin"
H=127.0.0.1; P=55434; SU=postgres; SRC=mmv_v2_scratch; FRESH=mmv_v2_scratch_rest_pa; OLD=mmv_v2_scratch_avant_pa
CHECK="$REPO/docs/phase/05/sql/0009_restore_check.sql"
[ "$P" = 5432 ] || [ "$P" = 5433 ] && { echo "REFUS : port du propriétaire"; exit 2; }
q() { "$BIN/psql.exe" -X -A -t -v ON_ERROR_STOP=1 -h $H -p $P -U $SU -d "$1" -c "$2"; }
step() { echo; echo "=== $* ($(date -u +%Y-%m-%dT%H:%M:%SZ))"; }
STAMP=$(date -u +%Y%m%d_%H%M%S)
DUMP="$W/${SRC}_postaudit_$STAMP.dump"

step "0. Précontrôles de la base source (identité exacte, version, migrations, sessions)"
q $SRC "SELECT 'version=' || current_setting('server_version') || ' port=' || current_setting('port') || ' base=' || current_database() || ' role=' || current_user || ' cluster=' || (SELECT system_identifier FROM pg_control_system())"
q $SRC "SELECT 'migrations=' || count(*) || ' derniere=' || max(\"MigrationId\") FROM public.\"__EFMigrationsHistory\""
q $SRC "SELECT 'autres_sessions_sur_la_base=' || count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()"
q $SRC "SELECT 'donnees_assistant originals=' || (SELECT count(*) FROM imports.batch_originals) || ' etats=' || (SELECT count(*) FROM imports.batch_assistant) || ' analyses=' || (SELECT count(*) FROM imports.batch_analyses) || ' evenements=' || (SELECT count(*) FROM imports.batch_analysis_events) || ' decisions=' || (SELECT count(*) FROM imports.batch_decisions) || ' lots_appliques=' || (SELECT count(*) FROM imports.batch_assistant a JOIN imports.batches b ON b.id = a.batch_id WHERE b.status = 'applied') || ' resultats_enregistres=' || (SELECT count(*) FROM imports.batch_assistant WHERE check_summary ? 'applied') || ' offres_verres=' || (SELECT count(*) FROM catalog.lens_offers)"

step "1. Sauvegarde (format personnalisé), empreinte, lisibilité"
"$BIN/pg_dump.exe" -h $H -p $P -U $SU -d $SRC -Fc -f "$DUMP"; echo "pg_dump exit=$?"
sha256sum "$DUMP" | sed "s#$W/##"
echo "taille_octets=$(stat -c %s "$DUMP")"
"$BIN/pg_restore.exe" --list "$DUMP" > "$W/list.txt"; echo "pg_restore --list exit=$? entrees=$(grep -vc '^;' "$W/list.txt") donnees_de_table=$(grep -c 'TABLE DATA' "$W/list.txt")"
for t in batch_originals batch_assistant batch_analyses batch_analysis_events batch_decisions; do grep -q "TABLE DATA imports $t " "$W/list.txt" && echo "contient TABLE DATA imports.$t"; done
grep -c " TRIGGER " "$W/list.txt" | sed 's/^/declencheurs_dans_la_sauvegarde=/'

step "2. Empreinte de cohérence de la source"
"$BIN/psql.exe" -X -A -t -v ON_ERROR_STOP=1 -h $H -p $P -U $SU -d $SRC -f "$CHECK" > "$W/source.check.txt"; echo "check source exit=$? lignes=$(wc -l < "$W/source.check.txt")"

step "3. Base NEUVE distincte (jamais la base active), même propriétaire, encodage et collation"
q postgres "SELECT 'existe_deja=' || count(*) FROM pg_database WHERE datname IN ('$FRESH', '$OLD')"
COLL=$(q postgres "SELECT datcollate FROM pg_database WHERE datname = '$SRC'"); CTYPE=$(q postgres "SELECT datctype FROM pg_database WHERE datname = '$SRC'"); OWNER=$(q postgres "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = '$SRC'")
q postgres "CREATE DATABASE $FRESH WITH OWNER $OWNER TEMPLATE template0 ENCODING 'UTF8' LC_COLLATE '$COLL' LC_CTYPE '$CTYPE'" && echo "base neuve $FRESH creee (proprietaire=$OWNER, collation=$COLL)"

step "4. Restauration dans la base neuve : transaction unique, arrêt à la première erreur, propriétaires et droits conservés"
"$BIN/pg_restore.exe" -h $H -p $P -U $SU -d $FRESH --exit-on-error --single-transaction "$DUMP"; echo "pg_restore exit=$?"

step "5. Contrôles de cohérence : empreinte de la base restaurée comparée à la source"
"$BIN/psql.exe" -X -A -t -v ON_ERROR_STOP=1 -h $H -p $P -U $SU -d $FRESH -f "$CHECK" > "$W/restored.check.txt"; echo "check restauree exit=$? lignes=$(wc -l < "$W/restored.check.txt")"
if diff -q "$W/source.check.txt" "$W/restored.check.txt" >/dev/null; then echo "COMPARAISON : IDENTIQUE ($(grep -c '^table|' "$W/source.check.txt") tables, $(grep -c '^trigger|' "$W/source.check.txt") declencheurs, $(grep -c '^migration|' "$W/source.check.txt") migrations)"; else echo "COMPARAISON : ECART"; diff "$W/source.check.txt" "$W/restored.check.txt" | head -20; fi
echo "declencheurs_inactifs_apres_restauration=$(grep '^trigger|' "$W/restored.check.txt" | grep -vc '|O$')"
sha256sum "$W/source.check.txt" "$W/restored.check.txt" | sed "s#$W/##"

step "6. Bascule explicite (décision du propriétaire en réel) : aucune session, renommage, ancienne base CONSERVÉE"
q postgres "SELECT 'sessions_source=' || count(*) FILTER (WHERE datname = '$SRC') || ' sessions_neuve=' || count(*) FILTER (WHERE datname = '$FRESH') FROM pg_stat_activity"
q postgres "ALTER DATABASE $SRC RENAME TO $OLD" && echo "renomme $SRC -> $OLD"
q postgres "ALTER DATABASE $FRESH RENAME TO $SRC" && echo "renomme $FRESH -> $SRC"
"$BIN/psql.exe" -X -A -t -v ON_ERROR_STOP=1 -h $H -p $P -U $SU -d $SRC -f "$CHECK" > "$W/switched.check.txt"
if diff -q "$W/source.check.txt" "$W/switched.check.txt" >/dev/null; then echo "APRES BASCULE : IDENTIQUE a la source"; else echo "APRES BASCULE : ECART"; fi
q postgres "SELECT 'bases=' || string_agg(datname, ',' ORDER BY datname) FROM pg_database WHERE datname LIKE 'mmv_v2_scratch%'"
echo "FIN"
