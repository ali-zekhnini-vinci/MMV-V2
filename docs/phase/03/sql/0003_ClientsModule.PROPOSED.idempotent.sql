START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'clients') THEN
            CREATE SCHEMA clients;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE SEQUENCE clients.client_reference_seq START WITH 1 INCREMENT BY 1 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE TABLE clients.clients (
        id uuid NOT NULL,
        reference character varying(16) NOT NULL DEFAULT ('CLI-' || lpad(nextval('clients.client_reference_seq')::text, 6, '0')),
        first_name character varying(100) NOT NULL,
        last_name character varying(100) NOT NULL,
        birth_date date,
        phone character varying(32),
        email character varying(254),
        address_line character varying(200),
        city character varying(100),
        postal_code character varying(10),
        notes character varying(1000),
        search_text character varying(700) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        archived_at_utc timestamp with time zone,
        CONSTRAINT pk_clients PRIMARY KEY (id),
        CONSTRAINT ck_clients_archived_after_created CHECK (archived_at_utc IS NULL OR archived_at_utc >= created_at_utc),
        CONSTRAINT ck_clients_birth_date_range CHECK (birth_date IS NULL OR birth_date >= DATE '1900-01-01'),
        CONSTRAINT ck_clients_first_name_not_blank CHECK (btrim(first_name) <> ''),
        CONSTRAINT ck_clients_last_name_not_blank CHECK (btrim(last_name) <> ''),
        CONSTRAINT ck_clients_reference_format CHECK (reference ~ '^CLI-[0-9]{6,}$'),
        CONSTRAINT ck_clients_updated_after_created CHECK (updated_at_utc >= created_at_utc),
        CONSTRAINT fk_clients_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_clients_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE INDEX ix_clients_active_name ON clients.clients (last_name, first_name, id) WHERE archived_at_utc IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE INDEX ix_clients_created_at ON clients.clients (created_at_utc, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE INDEX ix_clients_created_by_user_id ON clients.clients (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE INDEX ix_clients_updated_by_user_id ON clients.clients (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    CREATE UNIQUE INDEX ux_clients_reference ON clients.clients (reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016223616_ClientsModule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016223616_ClientsModule', '10.0.4');
    END IF;
END $EF$;
COMMIT;

