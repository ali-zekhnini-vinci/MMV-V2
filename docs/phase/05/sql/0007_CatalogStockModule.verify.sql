-- 0007_CatalogStockModule — contrôle de schéma APRÈS application (G05-SQL). LECTURE SEULE : aucune écriture.
-- Usage (opérateur, après application par scripts/Invoke-MmvSqlScript.ps1) :
--   psql -h localhost -p 5433 -U <rôle> -d <mmv_v2_dev|mmv_v2_test> -X -A -v ON_ERROR_STOP=1 -f docs/phase/05/sql/0007_CatalogStockModule.verify.sql
-- Chaque ligne « check|attendu|obtenu|OK/ECART » ; toute ligne ECART = arrêt, aucune correction improvisée.
-- L'inventaire EXACT (colonnes, contraintes, index, séquences, fonctions, déclencheurs, familles) est en outre comparé par
-- tests/MMV.PostgreSql.Tests (Phase05SchemaTests ↔ docs/phase/05/sql/0007_CatalogStockModule.schema-inventory.txt, et
-- SchemaState.CatalogStockModuleDifferences pour les objets 0001 à 0006 inchangés).
SET default_transaction_read_only = on;
BEGIN READ ONLY;

WITH phase05 AS (SELECT oid, nspname FROM pg_namespace WHERE nspname IN ('org', 'catalog', 'pricing', 'stock', 'imports')),
checks(name, expected, actual) AS (
    SELECT 'server_version_num >= 180000', 'true', (current_setting('server_version_num')::int >= 180000)::text
    UNION ALL SELECT 'port', '5433', inet_server_port()::text
    UNION ALL SELECT 'migrations (7, dernière 0007)', '7|20261030005557_CatalogStockModule',
        (SELECT count(*)::text || '|' || max("MigrationId") FROM public."__EFMigrationsHistory")
    UNION ALL SELECT 'schémas Phase 05', 'catalog,imports,org,pricing,stock', (SELECT string_agg(nspname, ',' ORDER BY nspname) FROM phase05)
    UNION ALL SELECT 'tables org', 'locations,stores',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE n.nspname = 'org' AND relkind = 'r')
    UNION ALL SELECT 'tables catalog', 'families,lens_references,product_models,product_variants,suppliers,variant_lens_treatments,variant_suppliers',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE n.nspname = 'catalog' AND relkind = 'r')
    UNION ALL SELECT 'tables pricing', 'price_history,tax_categories,variant_store_prices',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE n.nspname = 'pricing' AND relkind = 'r')
    UNION ALL SELECT 'tables stock', 'balances,inventory_lines,inventory_sessions,movements,receipt_lines,receipts,variant_store_settings',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE n.nspname = 'stock' AND relkind = 'r')
    UNION ALL SELECT 'tables imports', 'batches,rows',
        (SELECT string_agg(relname, ',' ORDER BY relname) FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE n.nspname = 'imports' AND relkind = 'r')
    UNION ALL SELECT 'index (5 schémas)', '102', (SELECT count(*)::text FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE relkind = 'i')
    UNION ALL SELECT 'séquences stock (réceptions, inventaires, identité des mouvements)', '3',
        (SELECT count(*)::text FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE relkind = 'S')
    UNION ALL SELECT 'contraintes CHECK', '77', (SELECT count(*)::text FROM pg_constraint k JOIN phase05 n ON n.oid = k.connamespace WHERE contype = 'c')
    UNION ALL SELECT 'clés étrangères (65 RESTRICT, 1 CASCADE variant_lens_treatments)', '66|65|1',
        (SELECT count(*)::text || '|' || count(*) FILTER (WHERE confdeltype = 'r') || '|' || count(*) FILTER (WHERE confdeltype = 'c')
         FROM pg_constraint k JOIN phase05 n ON n.oid = k.connamespace WHERE contype = 'f')
    UNION ALL SELECT 'déclencheurs utilisateur activés', '40|40',
        (SELECT count(*)::text || '|' || count(*) FILTER (WHERE tgenabled = 'O')
         FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN phase05 n ON n.oid = c.relnamespace WHERE NOT tgisinternal)
    UNION ALL SELECT 'fonctions', 'catalog.reject_change,catalog.reject_delete,imports.reject_closed_batch_change,imports.reject_closed_row_change,org.reject_location_change,org.reject_store_change,stock.reject_closed_session_change,stock.reject_resolved_line_change',
        (SELECT string_agg(n.nspname || '.' || proname, ',' ORDER BY n.nspname, proname) FROM pg_proc p JOIN phase05 n ON n.oid = p.pronamespace)
    UNION ALL SELECT 'aucun montant en flottant (real, double precision, money)', '0',
        (SELECT count(*)::text FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN phase05 n ON n.oid = c.relnamespace
         WHERE c.relkind = 'r' AND a.attnum > 0 AND format_type(a.atttypid, a.atttypmod) IN ('real', 'double precision', 'money'))
    UNION ALL SELECT 'montants numeric à échelle fixe', 'numeric(12,2):1,numeric(14,4):6,numeric(4,3):1,numeric(7,4):1,numeric(8,4):1',
        (SELECT string_agg(t || ':' || n, ',' ORDER BY t) FROM (SELECT format_type(a.atttypid, a.atttypmod) t, count(*)::text n
         FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN phase05 s ON s.oid = c.relnamespace
         WHERE c.relkind = 'r' AND a.attnum > 0 AND format_type(a.atttypid, a.atttypmod) LIKE 'numeric%' GROUP BY 1) x)
    UNION ALL SELECT 'stock négatif interdit (contrainte)', 'CHECK ((on_hand >= 0))',
        (SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_balances_on_hand')
    UNION ALL SELECT 'devise du prix = devise du magasin (clé composite)', 'FOREIGN KEY (store_id, currency_code) REFERENCES org.stores(id, currency_code) ON DELETE RESTRICT',
        (SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_variant_store_prices_store_currency')
    UNION ALL SELECT 'devises admises', 'CHECK ((currency_code = ANY (ARRAY[''EUR''::bpchar, ''MAD''::bpchar])))',
        (SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_stores_currency')
    UNION ALL SELECT 'six familles insérées', 'accessories,contact_lenses_solutions,ophthalmic_lenses,optical_frames,services,sunglasses',
        (SELECT string_agg(code, ',' ORDER BY code) FROM catalog.families)
    UNION ALL SELECT 'prestations : politique service seulement', '{service}', (SELECT allowed_policies::text FROM catalog.families WHERE code = 'services')
    UNION ALL SELECT 'aucun magasin, aucun taux, aucune donnée pilote', '0|0|0|0|0',
        (SELECT (SELECT count(*) FROM org.stores) || '|' || (SELECT count(*) FROM pricing.tax_categories) || '|' || (SELECT count(*) FROM catalog.product_variants)
             || '|' || (SELECT count(*) FROM stock.movements) || '|' || (SELECT count(*) FROM catalog.suppliers))
    UNION ALL SELECT 'contrainte des permissions : 16 droits Phase 05 ajoutés', '16',
        (SELECT ((d LIKE '%''catalog.read''%')::int + (d LIKE '%''catalog.write''%')::int + (d LIKE '%''catalog.archive''%')::int + (d LIKE '%''catalog.configure''%')::int
               + (d LIKE '%''catalog.import''%')::int + (d LIKE '%''pricing.write''%')::int + (d LIKE '%''pricing.manual''%')::int + (d LIKE '%''costs.read''%')::int
               + (d LIKE '%''costs.write''%')::int + (d LIKE '%''suppliers.read''%')::int + (d LIKE '%''suppliers.write''%')::int + (d LIKE '%''stock.read''%')::int
               + (d LIKE '%''stock.movements.write''%')::int + (d LIKE '%''stock.adjust''%')::int + (d LIKE '%''inventory.count''%')::int + (d LIKE '%''inventory.validate''%')::int)::text
         FROM (SELECT pg_get_constraintdef(oid) AS d FROM pg_constraint WHERE conname = 'ck_user_permissions_permission') x)
    UNION ALL SELECT 'droits existants conservés (12)', '12',
        (SELECT ((d LIKE '%''system.admin''%')::int + (d LIKE '%''clients.read''%')::int + (d LIKE '%''clients.write''%')::int + (d LIKE '%''clients.archive''%')::int
               + (d LIKE '%''appointments.read''%')::int + (d LIKE '%''appointments.write''%')::int + (d LIKE '%''appointments.cancel''%')::int + (d LIKE '%''audit.read''%')::int
               + (d LIKE '%''optical.read''%')::int + (d LIKE '%''optical.prescriptions.write''%')::int + (d LIKE '%''optical.measurements.write''%')::int
               + (d LIKE '%''optical.invalidate''%')::int)::text
         FROM (SELECT pg_get_constraintdef(oid) AS d FROM pg_constraint WHERE conname = 'ck_user_permissions_permission') x)
    UNION ALL SELECT 'propriétaire des objets = rôle applicatif', '0',
        (SELECT count(*)::text FROM pg_class c JOIN phase05 n ON n.oid = c.relnamespace WHERE c.relowner <> (SELECT datdba FROM pg_database WHERE datname = current_database()))
    UNION ALL SELECT 'aucune extension', '0', (SELECT count(*)::text FROM pg_extension WHERE extname <> 'plpgsql')
)
SELECT name || '|' || expected || '|' || coalesce(actual, '∅') || '|' || CASE WHEN actual = expected THEN 'OK' ELSE 'ECART' END FROM checks;

COMMIT;
