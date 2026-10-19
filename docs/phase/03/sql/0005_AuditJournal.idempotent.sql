START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'audit') THEN
            CREATE SCHEMA audit;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE TABLE audit.events (
        id uuid NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        actor_user_id uuid NOT NULL,
        client_id uuid,
        module character varying(32) NOT NULL,
        action character varying(64) NOT NULL,
        entity_type character varying(32) NOT NULL,
        entity_id uuid NOT NULL,
        changes jsonb,
        session_id uuid,
        trace_id character varying(64),
        CONSTRAINT pk_events PRIMARY KEY (id),
        CONSTRAINT ck_events_action_format CHECK (action ~ '^[a-z][a-z_]{0,30}\.[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_events_changes_array CHECK (changes IS NULL OR jsonb_typeof(changes) = 'array'),
        CONSTRAINT ck_events_entity_type_format CHECK (entity_type ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_events_module_format CHECK (module ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT fk_events_actor_users FOREIGN KEY (actor_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_events_clients FOREIGN KEY (client_id) REFERENCES clients.clients (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE FUNCTION audit.reject_event_mutation() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        RAISE EXCEPTION 'audit.events : journal en ajout seul (% refusé)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE TRIGGER trg_events_append_only BEFORE UPDATE OR DELETE ON audit.events FOR EACH ROW EXECUTE FUNCTION audit.reject_event_mutation();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE TRIGGER trg_events_no_truncate BEFORE TRUNCATE ON audit.events FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_event_mutation();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE INDEX ix_events_actor_user_id ON audit.events (actor_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE INDEX ix_events_client_occurred ON audit.events (client_id, occurred_at_utc DESC, id DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    CREATE INDEX ix_events_entity ON audit.events (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018163108_AuditJournal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018163108_AuditJournal', '10.0.4');
    END IF;
END $EF$;
COMMIT;

