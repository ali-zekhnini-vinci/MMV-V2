START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'optical') THEN
            CREATE SCHEMA optical;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE SEQUENCE optical.measurement_reference_seq START WITH 1 INCREMENT BY 1 MAXVALUE 999999999999999999 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE SEQUENCE optical.prescription_reference_seq START WITH 1 INCREMENT BY 1 MAXVALUE 999999999999999999 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TABLE optical.measurements (
        id uuid NOT NULL,
        reference character varying(24) NOT NULL DEFAULT ('MES-' || to_char(nextval('optical.measurement_reference_seq'), 'FM999999999999000000')),
        client_id uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        invalidated_at_utc timestamp with time zone,
        invalidated_by_user_id uuid,
        invalidation_reason_code character varying(32),
        invalidation_comment character varying(500),
        creation_request_id uuid NOT NULL,
        last_request_id uuid NOT NULL,
        CONSTRAINT pk_measurements PRIMARY KEY (id),
        CONSTRAINT ck_measurements_invalidated_after_created CHECK (invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc),
        CONSTRAINT ck_measurements_invalidation_complete CHECK ((invalidated_at_utc IS NULL) = (invalidated_by_user_id IS NULL) AND (invalidated_at_utc IS NULL) = (invalidation_reason_code IS NULL) AND (invalidation_comment IS NULL OR invalidated_at_utc IS NOT NULL)),
        CONSTRAINT ck_measurements_invalidation_reason_format CHECK (invalidation_reason_code IS NULL OR invalidation_reason_code ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_measurements_reference_format CHECK (reference ~ '^MES-[0-9]{6,18}$'),
        CONSTRAINT ck_measurements_updated_after_created CHECK (updated_at_utc >= created_at_utc),
        CONSTRAINT fk_measurements_clients FOREIGN KEY (client_id) REFERENCES clients.clients (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurements_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurements_invalidated_by_users FOREIGN KEY (invalidated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurements_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TABLE optical.prescriptions (
        id uuid NOT NULL,
        reference character varying(24) NOT NULL DEFAULT ('RX-' || to_char(nextval('optical.prescription_reference_seq'), 'FM999999999999000000')),
        client_id uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        invalidated_at_utc timestamp with time zone,
        invalidated_by_user_id uuid,
        invalidation_reason_code character varying(32),
        invalidation_comment character varying(500),
        creation_request_id uuid NOT NULL,
        last_request_id uuid NOT NULL,
        CONSTRAINT pk_prescriptions PRIMARY KEY (id),
        CONSTRAINT ck_prescriptions_invalidated_after_created CHECK (invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc),
        CONSTRAINT ck_prescriptions_invalidation_complete CHECK ((invalidated_at_utc IS NULL) = (invalidated_by_user_id IS NULL) AND (invalidated_at_utc IS NULL) = (invalidation_reason_code IS NULL) AND (invalidation_comment IS NULL OR invalidated_at_utc IS NOT NULL)),
        CONSTRAINT ck_prescriptions_invalidation_reason_format CHECK (invalidation_reason_code IS NULL OR invalidation_reason_code ~ '^[a-z][a-z_]{0,31}$'),
        CONSTRAINT ck_prescriptions_reference_format CHECK (reference ~ '^RX-[0-9]{6,18}$'),
        CONSTRAINT ck_prescriptions_updated_after_created CHECK (updated_at_utc >= created_at_utc),
        CONSTRAINT fk_prescriptions_clients FOREIGN KEY (client_id) REFERENCES clients.clients (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescriptions_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescriptions_invalidated_by_users FOREIGN KEY (invalidated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescriptions_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TABLE optical.measurement_revisions (
        id uuid NOT NULL,
        measured_on date,
        measured_by_user_id uuid,
        category character varying(120),
        od_addition_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_addition, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_axis_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_axis, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_cylinder_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_cylinder, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_prism_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_prism, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_sphere_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_sphere, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_addition_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_addition, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_axis_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_axis, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_cylinder_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_cylinder, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_prism_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_prism, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_sphere_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_sphere, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        measurement_id uuid NOT NULL,
        status character varying(16) NOT NULL,
        revision_no integer,
        base_revision_no integer,
        reason_code character varying(32),
        reason_comment character varying(500),
        od_sphere character varying(16),
        od_cylinder character varying(16),
        od_axis character varying(16),
        od_addition character varying(16),
        od_prism character varying(16),
        od_base character varying(16),
        od_acuity character varying(16),
        og_sphere character varying(16),
        og_cylinder character varying(16),
        og_axis character varying(16),
        og_addition character varying(16),
        og_prism character varying(16),
        og_base character varying(16),
        og_acuity character varying(16),
        notes character varying(2000),
        started_at_utc timestamp with time zone NOT NULL,
        started_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        confirmed_at_utc timestamp with time zone,
        confirmed_by_user_id uuid,
        discarded_at_utc timestamp with time zone,
        discarded_by_user_id uuid,
        CONSTRAINT pk_measurement_revisions PRIMARY KEY (id),
        CONSTRAINT ck_measurement_revisions_confirmation CHECK ((status = 'confirmed') = (revision_no IS NOT NULL) AND (revision_no IS NULL) = (confirmed_at_utc IS NULL) AND (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)),
        CONSTRAINT ck_measurement_revisions_confirmed_provenance CHECK (status <> 'confirmed' OR (measured_on IS NOT NULL AND measured_by_user_id IS NOT NULL)),
        CONSTRAINT ck_measurement_revisions_dates CHECK ((measured_on IS NULL OR measured_on >= DATE '1900-01-01')),
        CONSTRAINT ck_measurement_revisions_discard CHECK ((status = 'discarded') = (discarded_at_utc IS NOT NULL) AND (discarded_at_utc IS NULL) = (discarded_by_user_id IS NULL) AND (status <> 'discarded' OR base_revision_no IS NOT NULL)),
        CONSTRAINT ck_measurement_revisions_numbers CHECK ((revision_no IS NULL OR revision_no >= 1) AND (base_revision_no IS NULL OR base_revision_no >= 1) AND (revision_no IS NULL OR base_revision_no IS NULL OR revision_no > base_revision_no)),
        CONSTRAINT ck_measurement_revisions_od_addition_number CHECK (od_addition IS NULL OR od_addition ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_od_axis_number CHECK (od_axis IS NULL OR od_axis ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_od_cylinder_number CHECK (od_cylinder IS NULL OR od_cylinder ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_od_prism_number CHECK (od_prism IS NULL OR od_prism ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_od_sphere_number CHECK (od_sphere IS NULL OR od_sphere ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_og_addition_number CHECK (og_addition IS NULL OR og_addition ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_og_axis_number CHECK (og_axis IS NULL OR og_axis ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_og_cylinder_number CHECK (og_cylinder IS NULL OR og_cylinder ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_og_prism_number CHECK (og_prism IS NULL OR og_prism ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_og_sphere_number CHECK (og_sphere IS NULL OR og_sphere ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_measurement_revisions_reason CHECK ((base_revision_no IS NULL) = (reason_code IS NULL) AND (reason_comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$')),
        CONSTRAINT ck_measurement_revisions_status CHECK (status IN ('draft', 'confirmed', 'discarded')),
        CONSTRAINT ck_measurement_revisions_updated_after_started CHECK (updated_at_utc >= started_at_utc),
        CONSTRAINT fk_measurement_revisions_confirmed_by_users FOREIGN KEY (confirmed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurement_revisions_discarded_by_users FOREIGN KEY (discarded_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurement_revisions_measured_by_users FOREIGN KEY (measured_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurement_revisions_measurements FOREIGN KEY (measurement_id) REFERENCES optical.measurements (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurement_revisions_started_by_users FOREIGN KEY (started_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_measurement_revisions_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TABLE optical.prescription_revisions (
        id uuid NOT NULL,
        prescriber_name character varying(120),
        prescriber_qualification character varying(120),
        prescriber_identifier character varying(120),
        prescribed_on date,
        received_on date,
        od_addition_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_addition, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_axis_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_axis, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_cylinder_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_cylinder, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_prism_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_prism, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        od_sphere_value numeric GENERATED ALWAYS AS ((replace(replace(replace(od_sphere, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_addition_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_addition, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_axis_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_axis, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_cylinder_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_cylinder, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_prism_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_prism, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        og_sphere_value numeric GENERATED ALWAYS AS ((replace(replace(replace(og_sphere, ',', '.'), U&'\2212', '-'), ' ', ''))::numeric) STORED,
        prescription_id uuid NOT NULL,
        status character varying(16) NOT NULL,
        revision_no integer,
        base_revision_no integer,
        reason_code character varying(32),
        reason_comment character varying(500),
        od_sphere character varying(16),
        od_cylinder character varying(16),
        od_axis character varying(16),
        od_addition character varying(16),
        od_prism character varying(16),
        od_base character varying(16),
        od_acuity character varying(16),
        og_sphere character varying(16),
        og_cylinder character varying(16),
        og_axis character varying(16),
        og_addition character varying(16),
        og_prism character varying(16),
        og_base character varying(16),
        og_acuity character varying(16),
        notes character varying(2000),
        started_at_utc timestamp with time zone NOT NULL,
        started_by_user_id uuid NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        updated_by_user_id uuid NOT NULL,
        confirmed_at_utc timestamp with time zone,
        confirmed_by_user_id uuid,
        discarded_at_utc timestamp with time zone,
        discarded_by_user_id uuid,
        CONSTRAINT pk_prescription_revisions PRIMARY KEY (id),
        CONSTRAINT ck_prescription_revisions_confirmation CHECK ((status = 'confirmed') = (revision_no IS NOT NULL) AND (revision_no IS NULL) = (confirmed_at_utc IS NULL) AND (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)),
        CONSTRAINT ck_prescription_revisions_confirmed_provenance CHECK (status <> 'confirmed' OR (prescribed_on IS NOT NULL AND prescriber_name IS NOT NULL)),
        CONSTRAINT ck_prescription_revisions_dates CHECK ((prescribed_on IS NULL OR prescribed_on >= DATE '1900-01-01') AND (received_on IS NULL OR received_on >= DATE '1900-01-01')),
        CONSTRAINT ck_prescription_revisions_discard CHECK ((status = 'discarded') = (discarded_at_utc IS NOT NULL) AND (discarded_at_utc IS NULL) = (discarded_by_user_id IS NULL) AND (status <> 'discarded' OR base_revision_no IS NOT NULL)),
        CONSTRAINT ck_prescription_revisions_numbers CHECK ((revision_no IS NULL OR revision_no >= 1) AND (base_revision_no IS NULL OR base_revision_no >= 1) AND (revision_no IS NULL OR base_revision_no IS NULL OR revision_no > base_revision_no)),
        CONSTRAINT ck_prescription_revisions_od_addition_number CHECK (od_addition IS NULL OR od_addition ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_od_axis_number CHECK (od_axis IS NULL OR od_axis ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_od_cylinder_number CHECK (od_cylinder IS NULL OR od_cylinder ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_od_prism_number CHECK (od_prism IS NULL OR od_prism ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_od_sphere_number CHECK (od_sphere IS NULL OR od_sphere ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_og_addition_number CHECK (og_addition IS NULL OR og_addition ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_og_axis_number CHECK (og_axis IS NULL OR og_axis ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_og_cylinder_number CHECK (og_cylinder IS NULL OR og_cylinder ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_og_prism_number CHECK (og_prism IS NULL OR og_prism ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_og_sphere_number CHECK (og_sphere IS NULL OR og_sphere ~ ('^[-+' || U&'\2212' || ']? ?[0-9]+([.,][0-9]+)?$')),
        CONSTRAINT ck_prescription_revisions_reason CHECK ((base_revision_no IS NULL) = (reason_code IS NULL) AND (reason_comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$')),
        CONSTRAINT ck_prescription_revisions_status CHECK (status IN ('draft', 'confirmed', 'discarded')),
        CONSTRAINT ck_prescription_revisions_updated_after_started CHECK (updated_at_utc >= started_at_utc),
        CONSTRAINT fk_prescription_revisions_confirmed_by_users FOREIGN KEY (confirmed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescription_revisions_discarded_by_users FOREIGN KEY (discarded_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescription_revisions_prescriptions FOREIGN KEY (prescription_id) REFERENCES optical.prescriptions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescription_revisions_started_by_users FOREIGN KEY (started_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_prescription_revisions_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read', 'optical.read', 'optical.prescriptions.write', 'optical.measurements.write', 'optical.invalidate'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_confirmed_by_user_id ON optical.measurement_revisions (confirmed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_discarded_by_user_id ON optical.measurement_revisions (discarded_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_measured_by_user_id ON optical.measurement_revisions (measured_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_measurement_started ON optical.measurement_revisions (measurement_id, started_at_utc, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_started_by_user_id ON optical.measurement_revisions (started_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurement_revisions_updated_by_user_id ON optical.measurement_revisions (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_measurement_revisions_number ON optical.measurement_revisions (measurement_id, revision_no) WHERE revision_no IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_measurement_revisions_open_draft ON optical.measurement_revisions (measurement_id) WHERE status = 'draft';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurements_client_created ON optical.measurements (client_id, created_at_utc DESC, id DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurements_created_by_user_id ON optical.measurements (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurements_invalidated_by_user_id ON optical.measurements (invalidated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_measurements_updated_by_user_id ON optical.measurements (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_measurements_creation_request_id ON optical.measurements (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_measurements_reference ON optical.measurements (reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescription_revisions_confirmed_by_user_id ON optical.prescription_revisions (confirmed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescription_revisions_discarded_by_user_id ON optical.prescription_revisions (discarded_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescription_revisions_prescription_started ON optical.prescription_revisions (prescription_id, started_at_utc, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescription_revisions_started_by_user_id ON optical.prescription_revisions (started_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescription_revisions_updated_by_user_id ON optical.prescription_revisions (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_prescription_revisions_number ON optical.prescription_revisions (prescription_id, revision_no) WHERE revision_no IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_prescription_revisions_open_draft ON optical.prescription_revisions (prescription_id) WHERE status = 'draft';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescriptions_client_created ON optical.prescriptions (client_id, created_at_utc DESC, id DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescriptions_created_by_user_id ON optical.prescriptions (created_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescriptions_invalidated_by_user_id ON optical.prescriptions (invalidated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE INDEX ix_prescriptions_updated_by_user_id ON optical.prescriptions (updated_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_prescriptions_creation_request_id ON optical.prescriptions (creation_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE UNIQUE INDEX ux_prescriptions_reference ON optical.prescriptions (reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE FUNCTION optical.reject_frozen_revision_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' THEN
            IF OLD.status = 'draft' THEN
                RETURN NEW;
            END IF;
        END IF;
        RAISE EXCEPTION 'optical : revision figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE FUNCTION optical.reject_frozen_record_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
        IF TG_OP = 'UPDATE' THEN
            IF OLD.invalidated_at_utc IS NULL THEN
                RETURN NEW;
            END IF;
        END IF;
        RAISE EXCEPTION 'optical : enregistrement invalide fige ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END;
    $fn$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_prescriptions_frozen BEFORE UPDATE OR DELETE ON optical.prescriptions FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_record_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_prescriptions_no_truncate BEFORE TRUNCATE ON optical.prescriptions FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_record_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_prescription_revisions_frozen BEFORE UPDATE OR DELETE ON optical.prescription_revisions FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_revision_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_prescription_revisions_no_truncate BEFORE TRUNCATE ON optical.prescription_revisions FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_revision_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_measurements_frozen BEFORE UPDATE OR DELETE ON optical.measurements FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_record_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_measurements_no_truncate BEFORE TRUNCATE ON optical.measurements FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_record_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_measurement_revisions_frozen BEFORE UPDATE OR DELETE ON optical.measurement_revisions FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_revision_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    CREATE TRIGGER trg_measurement_revisions_no_truncate BEFORE TRUNCATE ON optical.measurement_revisions FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_revision_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027134615_OpticalModule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027134615_OpticalModule', '10.0.4');
    END IF;
END $EF$;
COMMIT;

