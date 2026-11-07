-- MMV V2 — G05-SQL-0009 : empreinte de cohérence d'une base (LECTURE SEULE), pour comparer une base source et sa
-- restauration sur une base NEUVE distincte (COR-CSVAI-05, mandat post-audit du 06/11/2026).
-- Usage : psql -X -A -t -v ON_ERROR_STOP=1 -h <hôte> -p <port> -U <rôle> -d <base> -f 0009_restore_check.sql > <base>.check.txt
--         puis comparer les deux sorties (fc /N sous Windows, diff ailleurs) : AUCUNE différence attendue.
-- Ne modifie rien : transaction en lecture seule, annulée à la fin. N'affiche aucune valeur métier (empreintes md5 seulement).
BEGIN TRANSACTION READ ONLY;

-- 1. Migrations enregistrées (ordre et identité).
SELECT 'migration|' || "MigrationId" || '|' || "ProductVersion" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";

-- 2. Chaque table : nombre de lignes et empreinte du contenu (lignes triées par leur texte ; indépendante de l'ordre physique).
SELECT 'table|' || n.nspname || '.' || c.relname
    || '|' || (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
    || '|' || (xpath('/row/h/text()', query_to_xml(format('SELECT md5(coalesce(string_agg(t::text, %L ORDER BY t::text), %L)) AS h FROM %I.%I t', E'\n', '', n.nspname, c.relname), false, true, '')))[1]::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY n.nspname, c.relname;

-- 3. Séquences : dernière valeur (identifiants croissants des événements, etc.).
SELECT 'sequence|' || schemaname || '.' || sequencename || '|' || coalesce(last_value::text, 'null') FROM pg_sequences
WHERE schemaname NOT IN ('pg_catalog', 'information_schema') ORDER BY schemaname, sequencename;

-- 4. Déclencheurs utilisateur : présence et ÉTAT (O = actif) ; une restauration ne doit en laisser aucun désactivé.
SELECT 'trigger|' || n.nspname || '.' || c.relname || '|' || t.tgname || '|' || t.tgenabled::text
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname NOT IN ('pg_catalog', 'information_schema') ORDER BY n.nspname, c.relname, t.tgname;

-- 5. Contraintes (validées), index, fonctions : définitions résumées par empreinte.
--    Contraintes : définition NORMALISÉE (conversions « ::type », parenthèses et espaces retirés) — pg_dump / pg_restore
--    réécrit les CHECK « IN (...) » sur des varchar (ARRAY[...]::text[] → ARRAY[(...)::text, ...]) sans en changer le sens
--    (constaté le 06/11/2026 sur 25 contraintes de mmv_v2_dev / mmv_v2_test). Limite : les regroupements logiques
--    (parenthèses) ne sont pas comparés ; la définition exacte se relit par pg_get_constraintdef.
SELECT 'constraint|' || n.nspname || '.' || c.relname || '|' || k.conname || '|' || k.contype::text || '|' || k.convalidated::text || '|'
    || md5(regexp_replace(pg_get_constraintdef(k.oid), '::[a-z_ ]+(\[\])?|[() ]', '', 'g'))
FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') ORDER BY n.nspname, c.relname, k.conname;
SELECT 'index|' || schemaname || '.' || indexname || '|' || md5(indexdef) FROM pg_indexes
WHERE schemaname NOT IN ('pg_catalog', 'information_schema') ORDER BY schemaname, indexname;
SELECT 'function|' || n.nspname || '.' || p.proname || '|' || md5(pg_get_functiondef(p.oid))
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') AND p.prokind = 'f' ORDER BY n.nspname, p.proname;

-- 6. Propriétaires et droits des schémas et tables (une restauration --no-owner les perdrait).
SELECT 'owner|' || n.nspname || '.' || c.relname || '|' || pg_get_userbyid(c.relowner) || '|' || coalesce(c.relacl::text, 'default')
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p', 'S', 'v') AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY n.nspname, c.relname;

ROLLBACK;
