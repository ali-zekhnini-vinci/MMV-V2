START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'stock') THEN
            CREATE SCHEMA stock;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'imports') THEN
            CREATE SCHEMA imports;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'catalog') THEN
            CREATE SCHEMA catalog;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'org') THEN
            CREATE SCHEMA org;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'pricing') THEN
            CREATE SCHEMA pricing;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE SEQUENCE stock.inventory_number_seq START WITH 1 INCREMENT BY 1 MAXVALUE 999999999999999999 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE SEQUENCE stock.receipt_number_seq START WITH 1 INCREMENT BY 1 MAXVALUE 999999999999999999 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.families (
        code character varying(32) NOT NULL,
        label_fr character varying(60) NOT NULL,
        allowed_policies text[] NOT NULL,
        sort_order integer NOT NULL,
        CONSTRAINT pk_families PRIMARY KEY (code),
        CONSTRAINT ck_families_code_format CHECK (code ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_families_policies CHECK (allowed_policies <@ ARRAY['stocked', 'order_only', 'service']::text[] AND cardinality(allowed_policies) >= 1)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.lens_references (
        id uuid NOT NULL,
        kind character varying(16) NOT NULL,
        name character varying(120) NOT NULL,
        name_key character varying(120) NOT NULL,
        manufacturer_id uuid,
        index_value numeric(4,3),
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        archived_at_utc timestamp with time zone,
        archived_by_user_id uuid,
        archive_reason character varying(500),
        last_request_id uuid,
        CONSTRAINT pk_lens_references PRIMARY KEY (id),
        CONSTRAINT ck_lens_references_index_value CHECK ((kind = 'index') = (index_value IS NOT NULL) AND (index_value IS NULL OR (index_value > 1 AND index_value < 2.5))),
        CONSTRAINT ck_lens_references_kind CHECK (kind IN ('manufacturer', 'range', 'index', 'treatment')),
        CONSTRAINT ck_lens_references_name CHECK (btrim(name) <> ''),
        CONSTRAINT ck_lens_references_parent CHECK ((kind = 'range' AND manufacturer_id IS NOT NULL) OR kind = 'treatment' OR (kind IN ('manufacturer', 'index') AND manufacturer_id IS NULL)),
        CONSTRAINT fk_lens_references_archived_by_users FOREIGN KEY (archived_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_lens_references_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_lens_references_manufacturer FOREIGN KEY (manufacturer_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
        CONSTRAINT fk_lens_references_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE org.stores (
        id uuid NOT NULL,
        code character varying(32) NOT NULL,
        name character varying(120) NOT NULL,
        country_code character(2) NOT NULL,
        currency_code character(3) NOT NULL,
        fiscal_profile_status character varying(16) NOT NULL,
        fiscal_validated_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        archived_at_utc timestamp with time zone,
        CONSTRAINT pk_stores PRIMARY KEY (id),
        CONSTRAINT ak_stores_id_currency UNIQUE (id, currency_code),
        CONSTRAINT ck_stores_code_format CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'),
        CONSTRAINT ck_stores_country_format CHECK (country_code ~ '^[A-Z]{2}$'),
        CONSTRAINT ck_stores_currency CHECK (currency_code IN ('EUR', 'MAD')),
        CONSTRAINT ck_stores_fiscal_status CHECK (fiscal_profile_status IN ('not_validated', 'validated')),
        CONSTRAINT ck_stores_fiscal_validation CHECK ((fiscal_profile_status = 'validated') = (fiscal_validated_at_utc IS NOT NULL))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.suppliers (
        id uuid NOT NULL,
        code character varying(32) NOT NULL,
        code_key character varying(32) NOT NULL,
        name character varying(200) NOT NULL,
        country_code character(2) NOT NULL,
        contact_name character varying(120),
        email character varying(254),
        phone character varying(32),
        address_line character varying(200),
        postal_code character varying(16),
        city character varying(100),
        legal_identifiers jsonb NOT NULL,
        notes character varying(2000),
        creation_request_id uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        archived_at_utc timestamp with time zone,
        archived_by_user_id uuid,
        archive_reason character varying(500),
        last_request_id uuid,
        CONSTRAINT pk_suppliers PRIMARY KEY (id),
        CONSTRAINT ck_suppliers_code CHECK (btrim(code) <> '' AND code_key = lower(btrim(code))),
        CONSTRAINT ck_suppliers_country_format CHECK (country_code ~ '^[A-Z]{2}$'),
        CONSTRAINT ck_suppliers_legal_identifiers CHECK (jsonb_typeof(legal_identifiers) = 'object'),
        CONSTRAINT ck_suppliers_name CHECK (btrim(name) <> ''),
        CONSTRAINT fk_suppliers_archived_by_users FOREIGN KEY (archived_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_suppliers_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_suppliers_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.product_models (
        id uuid NOT NULL,
        family_code character varying(32) NOT NULL,
        brand character varying(100),
        name character varying(200) NOT NULL,
        description character varying(2000),
        lens_range_id uuid,
        attributes jsonb NOT NULL,
        creation_request_id uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        archived_at_utc timestamp with time zone,
        archived_by_user_id uuid,
        archive_reason character varying(500),
        last_request_id uuid,
        CONSTRAINT pk_product_models PRIMARY KEY (id),
        CONSTRAINT ck_product_models_attributes CHECK (jsonb_typeof(attributes) = 'object'),
        CONSTRAINT ck_product_models_lens_range CHECK ((family_code = 'ophthalmic_lenses') = (lens_range_id IS NOT NULL)),
        CONSTRAINT ck_product_models_name CHECK (btrim(name) <> ''),
        CONSTRAINT fk_product_models_archived_by_users FOREIGN KEY (archived_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_models_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_models_families FOREIGN KEY (family_code) REFERENCES catalog.families (code) ON DELETE RESTRICT,
        CONSTRAINT fk_product_models_lens_range FOREIGN KEY (lens_range_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_models_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE imports.batches (
        id uuid NOT NULL,
        kind character varying(16) NOT NULL,
        store_id uuid,
        file_name character varying(200) NOT NULL,
        file_sha256 character(64) NOT NULL,
        content text NOT NULL,
        row_count integer NOT NULL,
        mapping jsonb NOT NULL,
        status character varying(16) NOT NULL,
        contains_costs boolean NOT NULL,
        contains_manual_prices boolean NOT NULL,
        error_count integer,
        uploaded_at_utc timestamp with time zone NOT NULL,
        uploaded_by_user_id uuid NOT NULL,
        checked_at_utc timestamp with time zone,
        checked_by_user_id uuid,
        applied_at_utc timestamp with time zone,
        applied_by_user_id uuid,
        creation_request_id uuid NOT NULL,
        last_request_id uuid,
        CONSTRAINT pk_batches PRIMARY KEY (id),
        CONSTRAINT ck_batches_kind CHECK (kind IN ('catalog', 'suppliers', 'prices')),
        CONSTRAINT ck_batches_lifecycle CHECK ((status = 'applied') = (applied_at_utc IS NOT NULL) AND (applied_at_utc IS NULL) = (applied_by_user_id IS NULL) AND (checked_at_utc IS NULL) = (checked_by_user_id IS NULL) AND (applied_at_utc IS NULL OR checked_at_utc IS NOT NULL)),
        CONSTRAINT ck_batches_mapping CHECK (jsonb_typeof(mapping) = 'object'),
        CONSTRAINT ck_batches_sha256 CHECK (file_sha256 ~ '^[0-9a-f]{64}$'),
        CONSTRAINT ck_batches_size CHECK (octet_length(content) <= 2097152 AND row_count >= 0 AND row_count <= 5000),
        CONSTRAINT ck_batches_status CHECK (status IN ('uploaded', 'checked', 'applied', 'cancelled')),
        CONSTRAINT ck_batches_store CHECK ((kind = 'prices') = (store_id IS NOT NULL)),
        CONSTRAINT fk_batches_applied_by_users FOREIGN KEY (applied_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_batches_checked_by_users FOREIGN KEY (checked_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_batches_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT,
        CONSTRAINT fk_batches_uploaded_by_users FOREIGN KEY (uploaded_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.inventory_sessions (
        id uuid NOT NULL,
        number character varying(24) NOT NULL DEFAULT ('INV-' || to_char(nextval('stock.inventory_number_seq'), 'FM999999999999000000')),
        store_id uuid NOT NULL,
        location_ids uuid[] NOT NULL,
        scope_type character varying(16) NOT NULL,
        scope_family_code character varying(32),
        blind boolean NOT NULL,
        status character varying(16) NOT NULL,
        opened_at_utc timestamp with time zone NOT NULL,
        opened_by_user_id uuid NOT NULL,
        validated_at_utc timestamp with time zone,
        validated_by_user_id uuid,
        uncounted_resolution character varying(16),
        abandoned_at_utc timestamp with time zone,
        abandoned_by_user_id uuid,
        abandon_reason character varying(500),
        creation_request_id uuid NOT NULL,
        last_request_id uuid,
        CONSTRAINT pk_inventory_sessions PRIMARY KEY (id),
        CONSTRAINT ck_inventory_sessions_lifecycle CHECK ((status = 'validated') = (validated_at_utc IS NOT NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL) AND (validated_at_utc IS NULL) = (uncounted_resolution IS NULL) AND (status = 'abandoned') = (abandoned_at_utc IS NOT NULL) AND (abandoned_at_utc IS NULL) = (abandoned_by_user_id IS NULL) AND (abandoned_at_utc IS NULL) = (abandon_reason IS NULL)),
        CONSTRAINT ck_inventory_sessions_locations CHECK (cardinality(location_ids) >= 1),
        CONSTRAINT ck_inventory_sessions_number_format CHECK (number ~ '^INV-[0-9]{6,18}$'),
        CONSTRAINT ck_inventory_sessions_scope CHECK (scope_type IN ('full', 'family', 'selection') AND (scope_type = 'family') = (scope_family_code IS NOT NULL)),
        CONSTRAINT ck_inventory_sessions_status CHECK (status IN ('open', 'validated', 'abandoned')),
        CONSTRAINT ck_inventory_sessions_uncounted CHECK (uncounted_resolution IS NULL OR uncounted_resolution IN ('exclude', 'set_to_zero')),
        CONSTRAINT fk_inventory_sessions_abandoned_by_users FOREIGN KEY (abandoned_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_sessions_families FOREIGN KEY (scope_family_code) REFERENCES catalog.families (code) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_sessions_opened_by_users FOREIGN KEY (opened_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_sessions_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_sessions_validated_by_users FOREIGN KEY (validated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE org.locations (
        id uuid NOT NULL,
        store_id uuid NOT NULL,
        code character varying(32) NOT NULL,
        name character varying(120) NOT NULL,
        is_default boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        archived_at_utc timestamp with time zone,
        CONSTRAINT pk_locations PRIMARY KEY (id),
        CONSTRAINT ck_locations_code_format CHECK (code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'),
        CONSTRAINT fk_locations_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE pricing.tax_categories (
        store_id uuid NOT NULL,
        code character varying(32) NOT NULL,
        label character varying(120) NOT NULL,
        rate numeric(7,4),
        legal_reference character varying(200),
        validated_at_utc timestamp with time zone,
        validated_by_user_id uuid,
        CONSTRAINT pk_tax_categories PRIMARY KEY (store_id, code),
        CONSTRAINT ck_tax_categories_code_format CHECK (code ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_tax_categories_rate CHECK (rate IS NULL OR (rate >= 0 AND rate < 1)),
        CONSTRAINT ck_tax_categories_validation CHECK ((rate IS NULL) = (validated_at_utc IS NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL)),
        CONSTRAINT fk_tax_categories_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT,
        CONSTRAINT fk_tax_categories_validated_by_users FOREIGN KEY (validated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.product_variants (
        id uuid NOT NULL,
        model_id uuid NOT NULL,
        sku character varying(64) NOT NULL,
        sku_key character varying(64) NOT NULL,
        manufacturer_reference character varying(64),
        barcode character varying(14),
        label character varying(200) NOT NULL,
        attributes jsonb NOT NULL,
        lens_index_id uuid,
        stock_policy character varying(16) NOT NULL,
        tax_category_code character varying(32),
        creation_request_id uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        archived_at_utc timestamp with time zone,
        archived_by_user_id uuid,
        archive_reason character varying(500),
        last_request_id uuid,
        CONSTRAINT pk_product_variants PRIMARY KEY (id),
        CONSTRAINT ck_product_variants_attributes CHECK (jsonb_typeof(attributes) = 'object'),
        CONSTRAINT ck_product_variants_barcode CHECK (barcode IS NULL OR barcode ~ '^([0-9]{8}|[0-9]{12,14})$'),
        CONSTRAINT ck_product_variants_label CHECK (btrim(label) <> ''),
        CONSTRAINT ck_product_variants_policy CHECK (stock_policy IN ('stocked', 'order_only', 'service')),
        CONSTRAINT ck_product_variants_sku CHECK (btrim(sku) <> '' AND sku_key = lower(btrim(sku))),
        CONSTRAINT ck_product_variants_tax_category_format CHECK (tax_category_code IS NULL OR tax_category_code ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT fk_product_variants_archived_by_users FOREIGN KEY (archived_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_variants_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_variants_lens_index FOREIGN KEY (lens_index_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_variants_models FOREIGN KEY (model_id) REFERENCES catalog.product_models (id) ON DELETE RESTRICT,
        CONSTRAINT fk_product_variants_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE imports.rows (
        batch_id uuid NOT NULL,
        line_no integer NOT NULL,
        values_json jsonb NOT NULL,
        outcome character varying(16) NOT NULL,
        errors jsonb NOT NULL,
        target_key character varying(200),
        CONSTRAINT pk_rows PRIMARY KEY (batch_id, line_no),
        CONSTRAINT ck_rows_json CHECK (jsonb_typeof(values_json) = 'object' AND jsonb_typeof(errors) = 'array'),
        CONSTRAINT ck_rows_line_no CHECK (line_no >= 1),
        CONSTRAINT ck_rows_outcome CHECK (outcome IN ('create', 'update', 'unchanged', 'error')),
        CONSTRAINT fk_rows_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.receipts (
        id uuid NOT NULL,
        number character varying(24) NOT NULL DEFAULT ('REC-' || to_char(nextval('stock.receipt_number_seq'), 'FM999999999999000000')),
        store_id uuid NOT NULL,
        location_id uuid NOT NULL,
        supplier_id uuid NOT NULL,
        currency_code character(3) NOT NULL,
        supplier_document_reference character varying(64),
        notes character varying(500),
        completes_receipt_id uuid,
        received_at_utc timestamp with time zone NOT NULL,
        received_by_user_id uuid NOT NULL,
        request_id uuid NOT NULL,
        CONSTRAINT pk_receipts PRIMARY KEY (id),
        CONSTRAINT ck_receipts_number_format CHECK (number ~ '^REC-[0-9]{6,18}$'),
        CONSTRAINT fk_receipts_completes FOREIGN KEY (completes_receipt_id) REFERENCES stock.receipts (id) ON DELETE RESTRICT,
        CONSTRAINT fk_receipts_locations FOREIGN KEY (location_id) REFERENCES org.locations (id) ON DELETE RESTRICT,
        CONSTRAINT fk_receipts_received_by_users FOREIGN KEY (received_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_receipts_store_currency FOREIGN KEY (store_id, currency_code) REFERENCES org.stores (id, currency_code) ON DELETE RESTRICT,
        CONSTRAINT fk_receipts_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.balances (
        variant_id uuid NOT NULL,
        location_id uuid NOT NULL,
        on_hand integer NOT NULL,
        reserved integer NOT NULL DEFAULT 0,
        last_movement_seq bigint,
        updated_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_balances PRIMARY KEY (variant_id, location_id),
        CONSTRAINT ck_balances_on_hand CHECK (on_hand >= 0),
        CONSTRAINT ck_balances_reserved CHECK (reserved >= 0 AND reserved <= on_hand),
        CONSTRAINT fk_balances_locations FOREIGN KEY (location_id) REFERENCES org.locations (id) ON DELETE RESTRICT,
        CONSTRAINT fk_balances_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.movements (
        id uuid NOT NULL,
        seq bigint GENERATED ALWAYS AS IDENTITY,
        variant_id uuid NOT NULL,
        location_id uuid NOT NULL,
        movement_type character varying(32) NOT NULL,
        quantity_delta integer NOT NULL,
        quantity_before integer NOT NULL,
        quantity_after integer NOT NULL,
        reason_code character varying(32) NOT NULL,
        comment character varying(500),
        supplier_id uuid,
        unit_cost_ht numeric(14,4),
        currency_code character(3),
        source_type character varying(32),
        source_id uuid,
        performed_by_user_id uuid NOT NULL,
        performed_at_utc timestamp with time zone NOT NULL,
        request_id uuid NOT NULL,
        CONSTRAINT pk_movements PRIMARY KEY (id),
        CONSTRAINT ck_movements_delta CHECK (quantity_delta <> 0 AND quantity_after = quantity_before + quantity_delta AND quantity_before >= 0 AND quantity_after >= 0),
        CONSTRAINT ck_movements_reason CHECK (reason_code ~ '^[a-z][a-z_]{0,31}$' AND (reason_code <> 'other' OR comment IS NOT NULL)),
        CONSTRAINT ck_movements_receipt CHECK ((movement_type = 'receipt') = (supplier_id IS NOT NULL) AND (unit_cost_ht IS NULL OR (movement_type = 'receipt' AND unit_cost_ht >= 0)) AND (unit_cost_ht IS NULL) = (currency_code IS NULL)),
        CONSTRAINT ck_movements_sign CHECK ((movement_type NOT IN ('receipt', 'manual_in') OR quantity_delta > 0) AND (movement_type <> 'manual_out' OR quantity_delta < 0)),
        CONSTRAINT ck_movements_source CHECK ((source_type IS NULL) = (source_id IS NULL) AND (source_type IS NULL OR source_type IN ('receipt', 'inventory_session'))),
        CONSTRAINT ck_movements_type CHECK (movement_type IN ('receipt', 'manual_in', 'manual_out', 'adjustment', 'inventory_adjustment')),
        CONSTRAINT fk_movements_locations FOREIGN KEY (location_id) REFERENCES org.locations (id) ON DELETE RESTRICT,
        CONSTRAINT fk_movements_performed_by_users FOREIGN KEY (performed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_movements_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT,
        CONSTRAINT fk_movements_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE pricing.price_history (
        id uuid NOT NULL,
        variant_id uuid NOT NULL,
        store_id uuid,
        currency_code character(3) NOT NULL,
        field character varying(32) NOT NULL,
        supplier_id uuid,
        old_value numeric(14,4),
        new_value numeric(14,4),
        old_mode character varying(16),
        new_mode character varying(16),
        origin character varying(16) NOT NULL,
        reason character varying(500),
        changed_at_utc timestamp with time zone NOT NULL,
        changed_by_user_id uuid NOT NULL,
        request_id uuid NOT NULL,
        CONSTRAINT pk_price_history PRIMARY KEY (id),
        CONSTRAINT ck_price_history_currency CHECK (currency_code ~ '^[A-Z]{3}$'),
        CONSTRAINT ck_price_history_field CHECK (field IN ('purchase_price_ht', 'coefficient', 'pricing_mode', 'sale_price_ht', 'supplier_price_ht')),
        CONSTRAINT ck_price_history_mode CHECK ((field = 'pricing_mode') = (new_mode IS NOT NULL) AND (old_mode IS NULL OR field = 'pricing_mode')),
        CONSTRAINT ck_price_history_origin CHECK (origin IN ('edit', 'recalculation', 'manual_price', 'import', 'receipt')),
        CONSTRAINT ck_price_history_scope CHECK ((field = 'supplier_price_ht') = (supplier_id IS NOT NULL) AND (field = 'supplier_price_ht' OR store_id IS NOT NULL)),
        CONSTRAINT fk_price_history_changed_by_users FOREIGN KEY (changed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_price_history_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT,
        CONSTRAINT fk_price_history_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT,
        CONSTRAINT fk_price_history_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.variant_lens_treatments (
        variant_id uuid NOT NULL,
        treatment_id uuid NOT NULL,
        CONSTRAINT pk_variant_lens_treatments PRIMARY KEY (variant_id, treatment_id),
        CONSTRAINT fk_variant_lens_treatments_treatment FOREIGN KEY (treatment_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_lens_treatments_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE pricing.variant_store_prices (
        variant_id uuid NOT NULL,
        store_id uuid NOT NULL,
        currency_code character(3) NOT NULL,
        purchase_price_ht numeric(14,4),
        coefficient numeric(8,4),
        pricing_mode character varying(16) NOT NULL,
        sale_price_ht numeric(12,2),
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        last_request_id uuid,
        CONSTRAINT pk_variant_store_prices PRIMARY KEY (variant_id, store_id),
        CONSTRAINT ck_variant_store_prices_coefficient CHECK (coefficient IS NULL OR (coefficient > 0 AND coefficient <= 100)),
        CONSTRAINT ck_variant_store_prices_coefficient_mode CHECK (pricing_mode = 'manual' OR sale_price_ht IS NULL OR (purchase_price_ht IS NOT NULL AND coefficient IS NOT NULL)),
        CONSTRAINT ck_variant_store_prices_mode CHECK (pricing_mode IN ('coefficient', 'manual')),
        CONSTRAINT ck_variant_store_prices_purchase CHECK (purchase_price_ht IS NULL OR purchase_price_ht >= 0),
        CONSTRAINT ck_variant_store_prices_sale CHECK (sale_price_ht IS NULL OR sale_price_ht >= 0),
        CONSTRAINT fk_variant_store_prices_store_currency FOREIGN KEY (store_id, currency_code) REFERENCES org.stores (id, currency_code) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_store_prices_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_store_prices_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.variant_store_settings (
        variant_id uuid NOT NULL,
        store_id uuid NOT NULL,
        low_stock_threshold integer,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        last_request_id uuid,
        CONSTRAINT pk_variant_store_settings PRIMARY KEY (variant_id, store_id),
        CONSTRAINT ck_variant_store_settings_threshold CHECK (low_stock_threshold IS NULL OR (low_stock_threshold >= 0 AND low_stock_threshold <= 100000)),
        CONSTRAINT fk_variant_store_settings_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_store_settings_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_store_settings_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE catalog.variant_suppliers (
        variant_id uuid NOT NULL,
        supplier_id uuid NOT NULL,
        supplier_reference character varying(64),
        supplier_price_ht numeric(14,4),
        supplier_currency character(3),
        lead_time_days integer,
        is_primary boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        removed_at_utc timestamp with time zone,
        removed_by_user_id uuid,
        last_request_id uuid,
        CONSTRAINT pk_variant_suppliers PRIMARY KEY (variant_id, supplier_id),
        CONSTRAINT ck_variant_suppliers_currency CHECK ((supplier_price_ht IS NULL) = (supplier_currency IS NULL) AND (supplier_currency IS NULL OR supplier_currency ~ '^[A-Z]{3}$')),
        CONSTRAINT ck_variant_suppliers_lead_time CHECK (lead_time_days IS NULL OR (lead_time_days >= 0 AND lead_time_days <= 365)),
        CONSTRAINT ck_variant_suppliers_price CHECK (supplier_price_ht IS NULL OR supplier_price_ht >= 0),
        CONSTRAINT ck_variant_suppliers_removed CHECK ((removed_at_utc IS NULL) = (removed_by_user_id IS NULL) AND (removed_at_utc IS NULL OR NOT is_primary)),
        CONSTRAINT fk_variant_suppliers_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_suppliers_removed_by_users FOREIGN KEY (removed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_suppliers_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_suppliers_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_variant_suppliers_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.inventory_lines (
        session_id uuid NOT NULL,
        variant_id uuid NOT NULL,
        location_id uuid NOT NULL,
        expected_at_open integer NOT NULL,
        expected_at_count integer,
        counted_quantity integer,
        counted_by_user_id uuid,
        counted_at_utc timestamp with time zone,
        last_request_id uuid,
        reason_code character varying(32),
        comment character varying(500),
        resolution character varying(16),
        applied_delta integer,
        movement_id uuid,
        CONSTRAINT pk_inventory_lines PRIMARY KEY (session_id, variant_id, location_id),
        CONSTRAINT ck_inventory_lines_applied CHECK ((movement_id IS NOT NULL) = (applied_delta IS NOT NULL AND applied_delta <> 0) AND (movement_id IS NULL OR resolution IN ('applied', 'set_to_zero'))),
        CONSTRAINT ck_inventory_lines_count CHECK ((counted_quantity IS NULL) = (expected_at_count IS NULL) AND (counted_quantity IS NULL) = (counted_at_utc IS NULL) AND (counted_at_utc IS NULL) = (counted_by_user_id IS NULL)),
        CONSTRAINT ck_inventory_lines_quantities CHECK (expected_at_open >= 0 AND (counted_quantity IS NULL OR (counted_quantity >= 0 AND counted_quantity <= 1000000))),
        CONSTRAINT ck_inventory_lines_reason CHECK ((reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$') AND (comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS DISTINCT FROM 'other' OR comment IS NOT NULL)),
        CONSTRAINT ck_inventory_lines_resolution CHECK (resolution IS NULL OR resolution IN ('applied', 'no_difference', 'excluded', 'set_to_zero')),
        CONSTRAINT fk_inventory_lines_counted_by_users FOREIGN KEY (counted_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_lines_locations FOREIGN KEY (location_id) REFERENCES org.locations (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_lines_movements FOREIGN KEY (movement_id) REFERENCES stock.movements (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_lines_sessions FOREIGN KEY (session_id) REFERENCES stock.inventory_sessions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_inventory_lines_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TABLE stock.receipt_lines (
        receipt_id uuid NOT NULL,
        line_no integer NOT NULL,
        variant_id uuid NOT NULL,
        announced_quantity integer,
        received_quantity integer NOT NULL,
        unit_cost_ht numeric(14,4),
        movement_id uuid,
        CONSTRAINT pk_receipt_lines PRIMARY KEY (receipt_id, line_no),
        CONSTRAINT ck_receipt_lines_cost CHECK (unit_cost_ht IS NULL OR unit_cost_ht >= 0),
        CONSTRAINT ck_receipt_lines_line_no CHECK (line_no >= 1),
        CONSTRAINT ck_receipt_lines_movement CHECK ((received_quantity > 0) = (movement_id IS NOT NULL)),
        CONSTRAINT ck_receipt_lines_quantities CHECK (received_quantity >= 0 AND (announced_quantity IS NULL OR announced_quantity >= 0) AND (received_quantity > 0 OR COALESCE(announced_quantity, 0) > 0)),
        CONSTRAINT fk_receipt_lines_movements FOREIGN KEY (movement_id) REFERENCES stock.movements (id) ON DELETE RESTRICT,
        CONSTRAINT fk_receipt_lines_receipts FOREIGN KEY (receipt_id) REFERENCES stock.receipts (id) ON DELETE RESTRICT,
        CONSTRAINT fk_receipt_lines_variants FOREIGN KEY (variant_id) REFERENCES catalog.product_variants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('accessories', ARRAY['stocked','order_only']::text[], 'Accessoires', 5);
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('contact_lenses_solutions', ARRAY['stocked','order_only']::text[], 'Lentilles et solutions', 4);
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('ophthalmic_lenses', ARRAY['stocked','order_only']::text[], 'Verres correcteurs', 3);
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('optical_frames', ARRAY['stocked','order_only']::text[], 'Montures optiques', 1);
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('services', ARRAY['service']::text[], 'Prestations', 6);
    INSERT INTO catalog.families (code, allowed_policies, label_fr, sort_order)
    VALUES ('sunglasses', ARRAY['stocked','order_only']::text[], 'Lunettes de soleil', 2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read', 'optical.read', 'optical.prescriptions.write', 'optical.measurements.write', 'optical.invalidate', 'catalog.read', 'catalog.write', 'catalog.archive', 'catalog.configure', 'catalog.import', 'pricing.write', 'pricing.manual', 'costs.read', 'costs.write', 'suppliers.read', 'suppliers.write', 'stock.read', 'stock.movements.write', 'stock.adjust', 'inventory.count', 'inventory.validate'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_balances_location_id ON stock.balances (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_batches_applied_by_user_id ON imports.batches (applied_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_batches_checked_by_user_id ON imports.batches (checked_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_batches_store_id ON imports.batches (store_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_batches_uploaded_at ON imports.batches (uploaded_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_batches_uploaded_by_user_id ON imports.batches (uploaded_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_batches_applied_file ON imports.batches (file_sha256, kind, store_id) NULLS NOT DISTINCT WHERE status = 'applied';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_batches_creation_request_id ON imports.batches (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_lines_counted_by_user_id ON stock.inventory_lines (counted_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_lines_location_id ON stock.inventory_lines (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_lines_variant_location ON stock.inventory_lines (variant_id, location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_inventory_lines_movement_id ON stock.inventory_lines (movement_id) WHERE movement_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_sessions_abandoned_by_user_id ON stock.inventory_sessions (abandoned_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_sessions_opened_by_user_id ON stock.inventory_sessions (opened_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_sessions_scope_family_code ON stock.inventory_sessions (scope_family_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_sessions_store_status ON stock.inventory_sessions (store_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_inventory_sessions_validated_by_user_id ON stock.inventory_sessions (validated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_inventory_sessions_creation_request_id ON stock.inventory_sessions (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_inventory_sessions_number ON stock.inventory_sessions (number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_lens_references_archived_by_user_id ON catalog.lens_references (archived_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_lens_references_created_by_user_id ON catalog.lens_references (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_lens_references_manufacturer_id ON catalog.lens_references (manufacturer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_lens_references_updated_by_user_id ON catalog.lens_references (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_lens_references_kind_parent_name ON catalog.lens_references (kind, manufacturer_id, name_key) NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_locations_one_default ON org.locations (store_id) WHERE is_default AND archived_at_utc IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_locations_store_code ON org.locations (store_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_location_id ON stock.movements (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_performed_at ON stock.movements (performed_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_performed_by_user_id ON stock.movements (performed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_source ON stock.movements (source_type, source_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_supplier_id ON stock.movements (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_movements_variant_location_seq ON stock.movements (variant_id, location_id, seq DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_movements_request_variant_location ON stock.movements (request_id, variant_id, location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_movements_seq ON stock.movements (seq);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_price_history_changed_by_user_id ON pricing.price_history (changed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_price_history_store_id ON pricing.price_history (store_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_price_history_supplier_id ON pricing.price_history (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_price_history_variant_store_changed ON pricing.price_history (variant_id, store_id, changed_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_models_archived_by_user_id ON catalog.product_models (archived_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_models_created_by_user_id ON catalog.product_models (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_models_family_name ON catalog.product_models (family_code, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_models_lens_range_id ON catalog.product_models (lens_range_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_models_updated_by_user_id ON catalog.product_models (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_product_models_creation_request_id ON catalog.product_models (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_variants_archived_by_user_id ON catalog.product_variants (archived_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_variants_created_by_user_id ON catalog.product_variants (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_variants_lens_index_id ON catalog.product_variants (lens_index_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_variants_model_id ON catalog.product_variants (model_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_product_variants_updated_by_user_id ON catalog.product_variants (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_product_variants_barcode ON catalog.product_variants (barcode) WHERE barcode IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_product_variants_creation_request_id ON catalog.product_variants (creation_request_id) WHERE creation_request_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_product_variants_sku ON catalog.product_variants (sku_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipt_lines_variant_id ON stock.receipt_lines (variant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_receipt_lines_movement_id ON stock.receipt_lines (movement_id) WHERE movement_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_completes_receipt_id ON stock.receipts (completes_receipt_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_location_id ON stock.receipts (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_received_by_user_id ON stock.receipts (received_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_store_currency ON stock.receipts (store_id, currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_store_received ON stock.receipts (store_id, received_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_receipts_supplier_id ON stock.receipts (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_receipts_number ON stock.receipts (number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_receipts_request_id ON stock.receipts (request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_stores_code ON org.stores (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_suppliers_archived_by_user_id ON catalog.suppliers (archived_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_suppliers_created_by_user_id ON catalog.suppliers (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_suppliers_name ON catalog.suppliers (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_suppliers_updated_by_user_id ON catalog.suppliers (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_suppliers_code ON catalog.suppliers (code_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_suppliers_creation_request_id ON catalog.suppliers (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_tax_categories_validated_by_user_id ON pricing.tax_categories (validated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_lens_treatments_treatment_id ON catalog.variant_lens_treatments (treatment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_store_prices_store_currency ON pricing.variant_store_prices (store_id, currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_store_prices_updated_by_user_id ON pricing.variant_store_prices (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_store_settings_store_id ON stock.variant_store_settings (store_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_store_settings_updated_by_user_id ON stock.variant_store_settings (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_suppliers_created_by_user_id ON catalog.variant_suppliers (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_suppliers_removed_by_user_id ON catalog.variant_suppliers (removed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_suppliers_supplier_id ON catalog.variant_suppliers (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE INDEX ix_variant_suppliers_updated_by_user_id ON catalog.variant_suppliers (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE UNIQUE INDEX ux_variant_suppliers_one_primary ON catalog.variant_suppliers (variant_id) WHERE is_primary;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION catalog.reject_delete() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        RAISE EXCEPTION '%.% : suppression refusee (%)', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION catalog.reject_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        RAISE EXCEPTION '%.% : journal en ajout seul, modification refusee (%)', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION org.reject_store_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' AND NEW.currency_code = OLD.currency_code AND NEW.id = OLD.id THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION 'org.stores : devise immuable, suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION org.reject_location_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' AND NEW.store_id = OLD.store_id AND NEW.id = OLD.id THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION 'org.locations : magasin immuable, suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION stock.reject_closed_session_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' AND OLD.status = 'open' THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION 'stock.inventory_sessions : session close figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION stock.reject_resolved_line_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'INSERT' THEN
            IF EXISTS (SELECT 1 FROM stock.inventory_sessions s WHERE s.id = NEW.session_id AND s.status = 'open') THEN
                RETURN NEW;
            END IF;
        ELSIF TG_OP = 'UPDATE' AND OLD.resolution IS NULL
            AND NEW.session_id = OLD.session_id AND NEW.variant_id = OLD.variant_id AND NEW.location_id = OLD.location_id
            AND NOT EXISTS (SELECT 1 FROM stock.inventory_sessions s WHERE s.id = OLD.session_id AND s.status = 'abandoned') THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION 'stock.inventory_lines : ligne figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION imports.reject_closed_batch_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' AND OLD.status IN ('uploaded', 'checked') THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION 'imports.batches : lot clos fige ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE FUNCTION imports.reject_closed_row_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP IN ('INSERT', 'DELETE') AND EXISTS (
            SELECT 1 FROM imports.batches b
            WHERE b.id = CASE WHEN TG_OP = 'INSERT' THEN NEW.batch_id ELSE OLD.batch_id END AND b.status IN ('uploaded', 'checked')) THEN
            RETURN CASE WHEN TG_OP = 'INSERT' THEN NEW ELSE OLD END;
        END IF;
        RAISE EXCEPTION 'imports.rows : rapport d''un lot clos fige (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_stores_guard BEFORE UPDATE OR DELETE ON org.stores FOR EACH ROW EXECUTE FUNCTION org.reject_store_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_locations_guard BEFORE UPDATE OR DELETE ON org.locations FOR EACH ROW EXECUTE FUNCTION org.reject_location_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_stores_no_truncate BEFORE TRUNCATE ON org.stores FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_locations_no_truncate BEFORE TRUNCATE ON org.locations FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_families_no_delete BEFORE DELETE ON catalog.families FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_families_no_truncate BEFORE TRUNCATE ON catalog.families FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_lens_references_no_delete BEFORE DELETE ON catalog.lens_references FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_lens_references_no_truncate BEFORE TRUNCATE ON catalog.lens_references FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_product_models_no_delete BEFORE DELETE ON catalog.product_models FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_product_models_no_truncate BEFORE TRUNCATE ON catalog.product_models FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_product_variants_no_delete BEFORE DELETE ON catalog.product_variants FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_product_variants_no_truncate BEFORE TRUNCATE ON catalog.product_variants FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_suppliers_no_delete BEFORE DELETE ON catalog.suppliers FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_suppliers_no_truncate BEFORE TRUNCATE ON catalog.suppliers FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_suppliers_no_delete BEFORE DELETE ON catalog.variant_suppliers FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_suppliers_no_truncate BEFORE TRUNCATE ON catalog.variant_suppliers FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_store_prices_no_delete BEFORE DELETE ON pricing.variant_store_prices FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_store_prices_no_truncate BEFORE TRUNCATE ON pricing.variant_store_prices FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_tax_categories_no_delete BEFORE DELETE ON pricing.tax_categories FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_tax_categories_no_truncate BEFORE TRUNCATE ON pricing.tax_categories FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_store_settings_no_delete BEFORE DELETE ON stock.variant_store_settings FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_variant_store_settings_no_truncate BEFORE TRUNCATE ON stock.variant_store_settings FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_balances_no_delete BEFORE DELETE ON stock.balances FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_balances_no_truncate BEFORE TRUNCATE ON stock.balances FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_price_history_append_only BEFORE UPDATE OR DELETE ON pricing.price_history FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_price_history_no_truncate BEFORE TRUNCATE ON pricing.price_history FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_movements_append_only BEFORE UPDATE OR DELETE ON stock.movements FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_movements_no_truncate BEFORE TRUNCATE ON stock.movements FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_receipts_append_only BEFORE UPDATE OR DELETE ON stock.receipts FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_receipts_no_truncate BEFORE TRUNCATE ON stock.receipts FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_receipt_lines_append_only BEFORE UPDATE OR DELETE ON stock.receipt_lines FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_receipt_lines_no_truncate BEFORE TRUNCATE ON stock.receipt_lines FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_inventory_sessions_frozen BEFORE UPDATE OR DELETE ON stock.inventory_sessions FOR EACH ROW EXECUTE FUNCTION stock.reject_closed_session_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_inventory_sessions_no_truncate BEFORE TRUNCATE ON stock.inventory_sessions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_inventory_lines_frozen BEFORE INSERT OR UPDATE OR DELETE ON stock.inventory_lines FOR EACH ROW EXECUTE FUNCTION stock.reject_resolved_line_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_inventory_lines_no_truncate BEFORE TRUNCATE ON stock.inventory_lines FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_batches_frozen BEFORE UPDATE OR DELETE ON imports.batches FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_batch_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_batches_no_truncate BEFORE TRUNCATE ON imports.batches FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_rows_frozen BEFORE INSERT OR UPDATE OR DELETE ON imports.rows FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_row_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    CREATE TRIGGER trg_rows_no_truncate BEFORE TRUNCATE ON imports.rows FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005557_CatalogStockModule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030005557_CatalogStockModule', '10.0.4');
    END IF;
END $EF$;
COMMIT;

