START TRANSACTION;
ALTER TABLE imports.batches ADD supplier_id uuid;

CREATE TABLE catalog.lens_offers (
    id uuid NOT NULL,
    range_id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    lens_type character varying(16) NOT NULL,
    material character varying(60),
    index_id uuid,
    production character varying(16) NOT NULL,
    lead_time_days integer,
    price_ht numeric(12,4),
    price_currency character(3),
    diameters jsonb NOT NULL,
    sphere_min numeric(5,2),
    sphere_max numeric(5,2),
    cylinder_min numeric(5,2),
    cylinder_max numeric(5,2),
    addition_min numeric(4,2),
    addition_max numeric(4,2),
    rules jsonb NOT NULL,
    rules_complete boolean NOT NULL,
    source_reference character varying(200),
    valid_from date,
    valid_until date,
    notes character varying(1000),
    revision integer NOT NULL,
    creation_request_id uuid NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by_user_id uuid NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_user_id uuid NOT NULL,
    archived_at_utc timestamp with time zone,
    archived_by_user_id uuid,
    archive_reason character varying(500),
    last_request_id uuid,
    CONSTRAINT pk_lens_offers PRIMARY KEY (id),
    CONSTRAINT ck_lens_offers_json CHECK (jsonb_typeof(diameters) = 'array' AND jsonb_typeof(rules) = 'array'),
    CONSTRAINT ck_lens_offers_lead_time CHECK (lead_time_days IS NULL OR (lead_time_days >= 0 AND lead_time_days <= 365)),
    CONSTRAINT ck_lens_offers_price CHECK ((price_ht IS NULL) = (price_currency IS NULL) AND (price_ht IS NULL OR price_ht >= 0) AND (price_currency IS NULL OR price_currency ~ '^[A-Z]{3}$')),
    CONSTRAINT ck_lens_offers_production CHECK (production IN ('stocked', 'on_order')),
    CONSTRAINT ck_lens_offers_ranges CHECK ((sphere_min IS NULL) = (sphere_max IS NULL) AND (sphere_min IS NULL OR sphere_min <= sphere_max) AND (cylinder_min IS NULL) = (cylinder_max IS NULL) AND (cylinder_min IS NULL OR cylinder_min <= cylinder_max) AND (addition_min IS NULL) = (addition_max IS NULL) AND (addition_min IS NULL OR (addition_min >= 0 AND addition_min <= addition_max))),
    CONSTRAINT ck_lens_offers_revision CHECK (revision >= 1),
    CONSTRAINT ck_lens_offers_type CHECK (lens_type IN ('single_vision', 'progressive', 'degressive', 'bifocal', 'other')),
    CONSTRAINT ck_lens_offers_validity CHECK (valid_from IS NULL OR valid_until IS NULL OR valid_from <= valid_until),
    CONSTRAINT fk_lens_offers_archived_by_users FOREIGN KEY (archived_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offers_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offers_index FOREIGN KEY (index_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offers_range FOREIGN KEY (range_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offers_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offers_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
);

CREATE TABLE imports.supplier_profiles (
    id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    kind character varying(16) NOT NULL,
    mapping jsonb NOT NULL,
    headers jsonb NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by_user_id uuid NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_user_id uuid NOT NULL,
    last_request_id uuid,
    CONSTRAINT pk_supplier_profiles PRIMARY KEY (id),
    CONSTRAINT ck_supplier_profiles_json CHECK (jsonb_typeof(mapping) = 'object' AND jsonb_typeof(headers) = 'array'),
    CONSTRAINT ck_supplier_profiles_kind CHECK (kind IN ('catalog', 'prices')),
    CONSTRAINT fk_supplier_profiles_created_by_users FOREIGN KEY (created_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_supplier_profiles_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT,
    CONSTRAINT fk_supplier_profiles_updated_by_users FOREIGN KEY (updated_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
);

CREATE TABLE catalog.lens_offer_revisions (
    id uuid NOT NULL,
    offer_id uuid NOT NULL,
    revision integer NOT NULL,
    snapshot jsonb NOT NULL,
    reason character varying(500),
    changed_at_utc timestamp with time zone NOT NULL,
    changed_by_user_id uuid NOT NULL,
    CONSTRAINT pk_lens_offer_revisions PRIMARY KEY (id),
    CONSTRAINT ck_lens_offer_revisions_revision CHECK (revision >= 1),
    CONSTRAINT ck_lens_offer_revisions_snapshot CHECK (jsonb_typeof(snapshot) = 'object'),
    CONSTRAINT fk_lens_offer_revisions_changed_by_users FOREIGN KEY (changed_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lens_offer_revisions_offers FOREIGN KEY (offer_id) REFERENCES catalog.lens_offers (id) ON DELETE RESTRICT
);

CREATE TABLE catalog.lens_offer_treatments (
    offer_id uuid NOT NULL,
    treatment_id uuid NOT NULL,
    CONSTRAINT pk_lens_offer_treatments PRIMARY KEY (offer_id, treatment_id),
    CONSTRAINT fk_lens_offer_treatments_offers FOREIGN KEY (offer_id) REFERENCES catalog.lens_offers (id) ON DELETE CASCADE,
    CONSTRAINT fk_lens_offer_treatments_treatment FOREIGN KEY (treatment_id) REFERENCES catalog.lens_references (id) ON DELETE RESTRICT
);

CREATE INDEX ix_batches_supplier_id ON imports.batches (supplier_id);

CREATE INDEX ix_lens_offer_revisions_changed_by_user_id ON catalog.lens_offer_revisions (changed_by_user_id);

CREATE UNIQUE INDEX ux_lens_offer_revisions_offer_revision ON catalog.lens_offer_revisions (offer_id, revision);

CREATE INDEX ix_lens_offer_treatments_treatment_id ON catalog.lens_offer_treatments (treatment_id);

CREATE INDEX ix_lens_offers_archived_by_user_id ON catalog.lens_offers (archived_by_user_id);

CREATE INDEX ix_lens_offers_created_by_user_id ON catalog.lens_offers (created_by_user_id);

CREATE INDEX ix_lens_offers_index_id ON catalog.lens_offers (index_id);

CREATE INDEX ix_lens_offers_range_id ON catalog.lens_offers (range_id);

CREATE INDEX ix_lens_offers_supplier_id ON catalog.lens_offers (supplier_id);

CREATE INDEX ix_lens_offers_updated_by_user_id ON catalog.lens_offers (updated_by_user_id);

CREATE UNIQUE INDEX ux_lens_offers_creation_request_id ON catalog.lens_offers (creation_request_id);

CREATE INDEX ix_supplier_profiles_created_by_user_id ON imports.supplier_profiles (created_by_user_id);

CREATE INDEX ix_supplier_profiles_updated_by_user_id ON imports.supplier_profiles (updated_by_user_id);

CREATE UNIQUE INDEX ux_supplier_profiles_supplier_kind ON imports.supplier_profiles (supplier_id, kind);

ALTER TABLE imports.batches ADD CONSTRAINT fk_batches_suppliers FOREIGN KEY (supplier_id) REFERENCES catalog.suppliers (id) ON DELETE RESTRICT;

CREATE TRIGGER trg_lens_offers_no_delete BEFORE DELETE ON catalog.lens_offers FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_lens_offers_no_truncate BEFORE TRUNCATE ON catalog.lens_offers FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_lens_offer_revisions_append_only BEFORE UPDATE OR DELETE ON catalog.lens_offer_revisions FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_lens_offer_revisions_no_truncate BEFORE TRUNCATE ON catalog.lens_offer_revisions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_supplier_profiles_no_delete BEFORE DELETE ON imports.supplier_profiles FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_supplier_profiles_no_truncate BEFORE TRUNCATE ON imports.supplier_profiles FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261102183203_CatalogCorrections', '10.0.4');

COMMIT;

