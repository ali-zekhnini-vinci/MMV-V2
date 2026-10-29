-- 0006_OpticalModule — contrôle de schéma APRÈS application (G04-SQL, étape B). LECTURE SEULE : aucune écriture.
-- Usage (opérateur, après application par scripts/Invoke-MmvSqlScript.ps1) :
--   psql -h localhost -p 5433 -U <rôle> -d <mmv_v2_dev|mmv_v2_test> -X -A -v ON_ERROR_STOP=1 -f docs/phase/04/sql/0006_OpticalModule.verify.sql
-- Chaque ligne « check|attendu|obtenu|OK/ECART » ; toute ligne ECART = arrêt, aucune correction improvisée.
-- L'inventaire EXACT (colonnes, contraintes, index, séquences, déclencheurs) est en outre comparé par
-- tests/MMV.PostgreSql.Tests (SchemaState.OpticalModuleDifferences, test TestDatabase_AllMigrationsApplied).
SET default_transaction_read_only = on;
BEGIN READ ONLY;

WITH checks(name, expected, actual) AS (
    SELECT 'server_version_num >= 180000', 'true', (current_setting('server_version_num')::int >= 180000)::text
    UNION ALL SELECT 'port', '5433', inet_server_port()::text
    UNION ALL SELECT 'migrations (6, dernière 0006)', '6|20261027134615_OpticalModule',
        (SELECT count(*)::text || '|' || max("MigrationId") FROM public."__EFMigrationsHistory")
    UNION ALL SELECT 'schéma optical', '1', (SELECT count(*)::text FROM pg_namespace WHERE nspname = 'optical')
    UNION ALL SELECT 'tables optical', 'measurement_revisions,measurements,prescription_revisions,prescriptions',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'optical' AND relkind = 'r')
    UNION ALL SELECT 'séquences optical', 'measurement_reference_seq,prescription_reference_seq',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'optical' AND relkind = 'S')
    UNION ALL SELECT 'index optical', '31',
        (SELECT count(*)::text FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'optical')
    UNION ALL SELECT 'index uniques partiels (brouillon unique, numéro)', '4',
        (SELECT count(*)::text FROM pg_indexes WHERE schemaname = 'optical' AND indexdef LIKE 'CREATE UNIQUE INDEX%WHERE%')
    UNION ALL SELECT 'contraintes CHECK optical (2 × 5 en-têtes + 2 × 18 révisions)', '46',
        (SELECT count(*)::text FROM pg_constraint k JOIN pg_namespace n ON n.oid = k.connamespace WHERE n.nspname = 'optical' AND contype = 'c')
    UNION ALL SELECT 'clés étrangères optical (toutes RESTRICT)', '19|19',
        (SELECT count(*)::text || '|' || count(*) FILTER (WHERE confdeltype = 'r')::text FROM pg_constraint k JOIN pg_namespace n ON n.oid = k.connamespace WHERE n.nspname = 'optical' AND contype = 'f')
    UNION ALL SELECT 'colonnes numériques générées (numeric sans échelle)', '20|20',
        (SELECT count(*)::text || '|' || count(*) FILTER (WHERE format_type(atttypid, atttypmod) = 'numeric')::text
         FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'optical' AND a.attgenerated = 's')
    UNION ALL SELECT 'déclencheurs optical activés', '8|8',
        (SELECT count(*)::text || '|' || count(*) FILTER (WHERE tgenabled = 'O')::text FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'optical' AND NOT tgisinternal)
    UNION ALL SELECT 'fonctions optical', 'reject_frozen_record_change,reject_frozen_revision_change',
        (SELECT string_agg(proname, ',' ORDER BY proname) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'optical')
    UNION ALL SELECT 'contrainte des permissions : 4 droits optiques, aucun droit documentaire', '4|0',
        (SELECT (length(d) - length(replace(d, '''optical.', '')))::int / length('''optical.') || '|' || (d LIKE '%optical.documents%')::int
         FROM (SELECT pg_get_constraintdef(oid) AS d FROM pg_constraint WHERE conname = 'ck_user_permissions_permission') x)
    UNION ALL SELECT 'droits existants conservés (8)', '8',
        (SELECT ((d LIKE '%''system.admin''%')::int + (d LIKE '%''clients.read''%')::int + (d LIKE '%''clients.write''%')::int + (d LIKE '%''clients.archive''%')::int
               + (d LIKE '%''appointments.read''%')::int + (d LIKE '%''appointments.write''%')::int + (d LIKE '%''appointments.cancel''%')::int + (d LIKE '%''audit.read''%')::int)::text
         FROM (SELECT pg_get_constraintdef(oid) AS d FROM pg_constraint WHERE conname = 'ck_user_permissions_permission') x)
    UNION ALL SELECT 'aucune donnée optique (base fraîchement migrée)', '0',
        (SELECT ((SELECT count(*) FROM optical.prescriptions) + (SELECT count(*) FROM optical.measurements)
               + (SELECT count(*) FROM optical.prescription_revisions) + (SELECT count(*) FROM optical.measurement_revisions))::text)
    UNION ALL SELECT 'aucun droit optique attribué', '0',
        (SELECT count(*)::text FROM auth.user_permissions WHERE permission LIKE 'optical.%')
    UNION ALL SELECT 'journal d''audit inchangé (déclencheurs 0005 actifs)', '2',
        (SELECT count(*)::text FROM pg_trigger WHERE tgrelid = 'audit.events'::regclass AND NOT tgisinternal AND tgenabled = 'O')
)
SELECT name || '|' || expected || '|' || coalesce(actual, '∅') || '|' || CASE WHEN expected = actual THEN 'OK' ELSE 'ECART' END AS controle
FROM checks;

ROLLBACK;
