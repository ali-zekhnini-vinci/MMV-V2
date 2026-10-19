START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'appointments') THEN
            CREATE SCHEMA appointments;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE TABLE appointments.appointments (
        id uuid NOT NULL,
        client_id uuid NOT NULL,
        responsible_user_id uuid,
        starts_at_utc timestamp with time zone NOT NULL,
        ends_at_utc timestamp with time zone NOT NULL,
        reason_code character varying(32) NOT NULL,
        notes character varying(1000),
        status character varying(16) NOT NULL,
        cancelled_at_utc timestamp with time zone,
        cancelled_by_user_id uuid,
        cancellation_reason character varying(200),
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        CONSTRAINT pk_appointments PRIMARY KEY (id),
        CONSTRAINT ck_appointments_cancellation_consistency CHECK ((status = 'cancelled') = (cancelled_at_utc IS NOT NULL AND cancelled_by_user_id IS NOT NULL)),
        CONSTRAINT ck_appointments_cancellation_reason_planned CHECK (status = 'cancelled' OR cancellation_reason IS NULL),
        CONSTRAINT ck_appointments_duration_range CHECK (ends_at_utc - starts_at_utc <= interval '12 hours'),
        CONSTRAINT ck_appointments_ends_after_starts CHECK (ends_at_utc > starts_at_utc),
        CONSTRAINT ck_appointments_reason_code_format CHECK (reason_code ~ '^[a-z0-9_-]{1,32}$'),
        CONSTRAINT ck_appointments_status CHECK (status IN ('planned', 'cancelled')),
        CONSTRAINT ck_appointments_updated_after_created CHECK (updated_at_utc >= created_at_utc),
        CONSTRAINT fk_appointments_cancelled_by_users FOREIGN KEY (cancelled_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_clients FOREIGN KEY (client_id) REFERENCES clients.clients (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_responsible_users FOREIGN KEY (responsible_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_cancelled_by_user_id ON appointments.appointments (cancelled_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_client_starts_at ON appointments.appointments (client_id, starts_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_created_by_user_id ON appointments.appointments (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_responsible_planned ON appointments.appointments (responsible_user_id, starts_at_utc) WHERE status = 'planned';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_starts_at ON appointments.appointments (starts_at_utc, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    CREATE INDEX ix_appointments_updated_by_user_id ON appointments.appointments (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017141136_AppointmentsModule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017141136_AppointmentsModule', '10.0.4');
    END IF;
END $EF$;
COMMIT;

