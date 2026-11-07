-- 0008_CatalogCorrections — contrôle de schéma APRÈS application (revue SQL des corrections Phase 05). LECTURE SEULE.
-- Usage (opérateur, après GO explicite du propriétaire et application par scripts/Invoke-MmvSqlScript.ps1) :
--   psql -h localhost -p 5433 -U <rôle> -d <mmv_v2_dev|mmv_v2_test> -X -A -v ON_ERROR_STOP=1 -f docs/phase/05/sql/0008_CatalogCorrections.verify.sql
-- Chaque ligne « check|attendu|obtenu|OK/ECART » ; toute ligne ECART = arrêt, aucune correction improvisée.
-- L'inventaire EXACT est en outre comparé par tests/MMV.PostgreSql.Tests (Phase05SchemaTests ↔
-- docs/phase/05/sql/0008_CatalogCorrections.schema-inventory.txt).
SET default_transaction_read_only = on;
BEGIN READ ONLY;

WITH checks(name, expected, actual) AS (
    SELECT 'server_version_num >= 180000', 'true', (current_setting('server_version_num')::int >= 180000)::text
    UNION ALL SELECT 'migrations (8, dernière 0008)', '8|20261102183203_CatalogCorrections',
        (SELECT count(*)::text || '|' || max("MigrationId") FROM public."__EFMigrationsHistory")
    UNION ALL SELECT '0007 toujours présente', '1',
        (SELECT count(*)::text FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule')
    UNION ALL SELECT 'tables catalog (nouvelles)', 'lens_offer_revisions,lens_offer_treatments,lens_offers',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'catalog' AND relkind = 'r' AND relname LIKE 'lens_offer%')
    UNION ALL SELECT 'tables imports', 'batches,rows,supplier_profiles',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'imports' AND relkind = 'r')
    UNION ALL SELECT 'colonne imports.batches.supplier_id (nullable)', 'uuid|YES',
        (SELECT data_type || '|' || is_nullable FROM information_schema.columns WHERE table_schema = 'imports' AND table_name = 'batches' AND column_name = 'supplier_id')
    UNION ALL SELECT 'ck_batches_store inchangée', 'CHECK ((((kind)::text = ''prices''::text) = (store_id IS NOT NULL)))',
        (SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_batches_store')
    UNION ALL SELECT 'contraintes CHECK lens_offers', '8',
        (SELECT count(*)::text FROM pg_constraint WHERE conrelid = 'catalog.lens_offers'::regclass AND contype = 'c')
    UNION ALL SELECT 'clés étrangères nouvelles (lens_offers 6, treatments 2, revisions 2, profiles 3, batches +1)', '14',
        (SELECT count(*)::text FROM pg_constraint WHERE contype = 'f' AND (conrelid IN ('catalog.lens_offers'::regclass, 'catalog.lens_offer_treatments'::regclass,
            'catalog.lens_offer_revisions'::regclass, 'imports.supplier_profiles'::regclass) OR conname = 'fk_batches_suppliers'))
    UNION ALL SELECT 'déclencheurs de garde 0008', '6',
        (SELECT count(*)::text FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('trg_lens_offers_no_delete', 'trg_lens_offers_no_truncate',
            'trg_lens_offer_revisions_append_only', 'trg_lens_offer_revisions_no_truncate', 'trg_supplier_profiles_no_delete', 'trg_supplier_profiles_no_truncate'))
    UNION ALL SELECT 'fonctions de garde réutilisées (aucune nouvelle fonction)', '0',
        (SELECT count(*)::text FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname IN ('catalog', 'imports') AND proname LIKE '%lens%')
    UNION ALL SELECT 'index uniques nouveaux', 'catalog.ux_lens_offer_revisions_offer_revision,catalog.ux_lens_offers_creation_request_id,imports.ux_supplier_profiles_supplier_kind',
        (SELECT string_agg(indexrelid::regclass::text, ',' ORDER BY indexrelid::regclass::text) FROM pg_index
         WHERE indisunique AND NOT indisprimary AND indrelid IN ('catalog.lens_offers'::regclass, 'catalog.lens_offer_revisions'::regclass, 'imports.supplier_profiles'::regclass))
    UNION ALL SELECT 'propriétaire des nouvelles tables = propriétaire de catalog.product_models', 'true',
        (SELECT bool_and(c.relowner = (SELECT relowner FROM pg_class WHERE oid = 'catalog.product_models'::regclass))::text FROM pg_class c
         WHERE c.oid IN ('catalog.lens_offers'::regclass, 'catalog.lens_offer_treatments'::regclass, 'catalog.lens_offer_revisions'::regclass, 'imports.supplier_profiles'::regclass))
    UNION ALL SELECT 'lignes existantes : aucune donnée créée par 0008', '0|0|0|0',
        (SELECT (SELECT count(*) FROM catalog.lens_offers)::text || '|' || (SELECT count(*) FROM catalog.lens_offer_revisions)::text || '|'
            || (SELECT count(*) FROM imports.supplier_profiles)::text || '|' || (SELECT count(*) FROM imports.batches WHERE supplier_id IS NOT NULL)::text)
)
SELECT name || '|' || expected || '|' || coalesce(actual, '∅') || '|' || CASE WHEN expected = actual THEN 'OK' ELSE 'ECART' END FROM checks;

ROLLBACK;
