-- 0009_CsvImportAssistant — contrôle de schéma APRÈS application (G05-SQL-0009, lot CSV-IMPORT-AI-01). LECTURE SEULE.
-- Usage (opérateur, après GO SQL explicite du propriétaire et application par scripts/Invoke-MmvSqlScript.ps1) :
--   psql -h localhost -p 5433 -U <rôle> -d <mmv_v2_dev|mmv_v2_test> -X -A -v ON_ERROR_STOP=1 -f docs/phase/05/sql/0009_CsvImportAssistant.verify.sql
-- Chaque ligne « check|attendu|obtenu|OK/ECART » ; toute ligne ECART = arrêt, aucune correction improvisée.
-- L'inventaire EXACT est en outre comparé par tests/MMV.PostgreSql.Tests (Phase05SchemaTests ↔
-- docs/phase/05/sql/0009_CsvImportAssistant.schema-inventory.txt).
SET default_transaction_read_only = on;
BEGIN READ ONLY;

WITH checks(name, expected, actual) AS (
    SELECT 'server_version_num >= 180000', 'true', (current_setting('server_version_num')::int >= 180000)::text
    UNION ALL SELECT 'migrations (9, dernière 0009)', '9|20261106154105_CsvImportAssistant',
        (SELECT count(*)::text || '|' || max("MigrationId") FROM public."__EFMigrationsHistory")
    UNION ALL SELECT '0007 et 0008 toujours présentes', '2',
        (SELECT count(*)::text FROM public."__EFMigrationsHistory" WHERE "MigrationId" IN ('20261030005557_CatalogStockModule', '20261102183203_CatalogCorrections'))
    UNION ALL SELECT 'tables imports', 'batch_analyses,batch_analysis_events,batch_assistant,batch_decisions,batch_originals,batches,rows,supplier_profiles',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'imports' AND relkind = 'r')
    UNION ALL SELECT 'colonnes imports.batches inchangées (21, inventaire 0008)', '21',
        (SELECT count(*)::text FROM information_schema.columns WHERE table_schema = 'imports' AND table_name = 'batches')
    UNION ALL SELECT 'ux_batches_applied_file inchangé',
        'CREATE UNIQUE INDEX ux_batches_applied_file ON imports.batches USING btree (file_sha256, kind, store_id) NULLS NOT DISTINCT WHERE ((status)::text = ''applied''::text)',
        (SELECT pg_get_indexdef('imports.ux_batches_applied_file'::regclass))
    -- Comparaison SÉMANTIQUE (trois valeurs, rien d'autre) : pg_restore réécrit le texte de la contrainte sans en changer le sens.
    UNION ALL SELECT 'ck_batches_kind inchangée (catalog, suppliers, prices seulement)', 'true',
        (SELECT (d LIKE '%''catalog''%' AND d LIKE '%''suppliers''%' AND d LIKE '%''prices''%' AND length(d) - length(replace(d, '''', '')) = 6)::text
         FROM (SELECT pg_get_constraintdef(oid) AS d FROM pg_constraint WHERE conname = 'ck_batches_kind') k)
    UNION ALL SELECT 'contraintes CHECK des nouvelles tables (originals 6, assistant 5, analyses 8, events 2, decisions 4)', '6|5|8|2|4',
        (SELECT (SELECT count(*) FROM pg_constraint WHERE conrelid = 'imports.batch_originals'::regclass AND contype = 'c')::text || '|'
            || (SELECT count(*) FROM pg_constraint WHERE conrelid = 'imports.batch_assistant'::regclass AND contype = 'c')::text || '|'
            || (SELECT count(*) FROM pg_constraint WHERE conrelid = 'imports.batch_analyses'::regclass AND contype = 'c')::text || '|'
            || (SELECT count(*) FROM pg_constraint WHERE conrelid = 'imports.batch_analysis_events'::regclass AND contype = 'c')::text || '|'
            || (SELECT count(*) FROM pg_constraint WHERE conrelid = 'imports.batch_decisions'::regclass AND contype = 'c')::text)
    UNION ALL SELECT 'clés étrangères nouvelles (originals 2, assistant 3, analyses 2, events 2, decisions 3)', '12',
        (SELECT count(*)::text FROM pg_constraint WHERE contype = 'f' AND conrelid IN ('imports.batch_originals'::regclass, 'imports.batch_assistant'::regclass,
            'imports.batch_analyses'::regclass, 'imports.batch_analysis_events'::regclass, 'imports.batch_decisions'::regclass))
    UNION ALL SELECT 'déclencheurs de garde 0009', '11',
        (SELECT count(*)::text FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('trg_batch_originals_guard', 'trg_batch_originals_no_truncate',
            'trg_batch_assistant_guard', 'trg_batch_assistant_no_truncate', 'trg_batch_analyses_guard', 'trg_batch_analyses_no_truncate',
            'trg_batch_analysis_events_append_only', 'trg_batch_analysis_events_no_truncate', 'trg_batch_decisions_append_only',
            'trg_batch_decisions_no_truncate', 'trg_batch_decisions_open_batch'))
    UNION ALL SELECT 'fonctions de garde nouvelles (4, schéma imports)', 'guard_batch_analysis,guard_batch_assistant,guard_batch_original,reject_closed_batch_decision',
        (SELECT string_agg(proname, ',' ORDER BY proname) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'imports' AND proname IN ('guard_batch_analysis', 'guard_batch_assistant', 'guard_batch_original', 'reject_closed_batch_decision'))
    UNION ALL SELECT 'index uniques nouveaux',
        'imports.ux_batch_analyses_active,imports.ux_batch_analyses_batch_attempt,imports.ux_batch_decisions_batch_request,imports.ux_batch_decisions_batch_revision',
        (SELECT string_agg(indexrelid::regclass::text, ',' ORDER BY indexrelid::regclass::text) FROM pg_index
         WHERE indisunique AND NOT indisprimary AND indrelid IN ('imports.batch_analyses'::regclass, 'imports.batch_decisions'::regclass,
            'imports.batch_originals'::regclass, 'imports.batch_assistant'::regclass, 'imports.batch_analysis_events'::regclass))
    UNION ALL SELECT 'propriétaire des nouvelles tables = propriétaire de imports.batches', 'true',
        (SELECT bool_and(c.relowner = (SELECT relowner FROM pg_class WHERE oid = 'imports.batches'::regclass))::text FROM pg_class c
         WHERE c.oid IN ('imports.batch_originals'::regclass, 'imports.batch_assistant'::regclass, 'imports.batch_analyses'::regclass,
            'imports.batch_analysis_events'::regclass, 'imports.batch_decisions'::regclass))
    UNION ALL SELECT 'lignes : aucune donnée créée par 0009', '0|0|0|0|0',
        (SELECT (SELECT count(*) FROM imports.batch_originals)::text || '|' || (SELECT count(*) FROM imports.batch_assistant)::text || '|'
            || (SELECT count(*) FROM imports.batch_analyses)::text || '|' || (SELECT count(*) FROM imports.batch_analysis_events)::text || '|'
            || (SELECT count(*) FROM imports.batch_decisions)::text)
)
SELECT name || '|' || expected || '|' || coalesce(actual, '∅') || '|' || CASE WHEN expected = actual THEN 'OK' ELSE 'ECART' END FROM checks;

ROLLBACK;
