START TRANSACTION;
CREATE TABLE imports.batch_analyses (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    attempt integer NOT NULL,
    based_on_revision integer NOT NULL,
    contract character varying(32) NOT NULL,
    configured_model character varying(100) NOT NULL,
    model character varying(100),
    model_digest character varying(100),
    ollama_version character varying(32),
    prompt_sha256 character(64),
    sample_sha256 character(64),
    status character varying(16) NOT NULL,
    queued_at_utc timestamp with time zone NOT NULL,
    started_at_utc timestamp with time zone,
    ended_at_utc timestamp with time zone,
    duration_ms bigint,
    eval_count integer,
    coverage jsonb NOT NULL,
    raw_output text,
    proposal jsonb,
    error character varying(200),
    late_result_discarded boolean NOT NULL,
    requested_by_user_id uuid NOT NULL,
    CONSTRAINT pk_batch_analyses PRIMARY KEY (id),
    CONSTRAINT ck_batch_analyses_attempt CHECK (attempt >= 1 AND based_on_revision >= 1),
    CONSTRAINT ck_batch_analyses_bounds CHECK ((raw_output IS NULL OR char_length(raw_output) <= 65536) AND (duration_ms IS NULL OR duration_ms >= 0) AND (eval_count IS NULL OR eval_count >= 0)),
    CONSTRAINT ck_batch_analyses_contract CHECK (contract = 'mmv.csvai.v2'),
    CONSTRAINT ck_batch_analyses_hashes CHECK ((prompt_sha256 IS NULL OR prompt_sha256 ~ '^[0-9a-f]{64}$') AND (sample_sha256 IS NULL OR sample_sha256 ~ '^[0-9a-f]{64}$')),
    CONSTRAINT ck_batch_analyses_json CHECK (jsonb_typeof(coverage) = 'object' AND (proposal IS NULL OR jsonb_typeof(proposal) = 'object')),
    CONSTRAINT ck_batch_analyses_late CHECK (NOT late_result_discarded OR status IN ('cancelled', 'interrupted')),
    CONSTRAINT ck_batch_analyses_lifecycle CHECK ((status <> 'queued' OR (started_at_utc IS NULL AND ended_at_utc IS NULL)) AND (status <> 'running' OR (started_at_utc IS NOT NULL AND ended_at_utc IS NULL)) AND (status IN ('queued', 'running') OR ended_at_utc IS NOT NULL) AND (status NOT IN ('done', 'incomplete') OR (model IS NOT NULL AND proposal IS NOT NULL AND started_at_utc IS NOT NULL))),
    CONSTRAINT ck_batch_analyses_status CHECK (status IN ('queued', 'running', 'done', 'incomplete', 'unavailable', 'timeout', 'invalid_output', 'cancelled', 'interrupted')),
    CONSTRAINT fk_batch_analyses_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_analyses_requested_by_users FOREIGN KEY (requested_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
);

CREATE TABLE imports.batch_assistant (
    batch_id uuid NOT NULL,
    pipeline character varying(16) NOT NULL,
    target character varying(16) NOT NULL,
    revision integer NOT NULL,
    checked_revision integer,
    rules_only_at_utc timestamp with time zone,
    rules_only_by_user_id uuid,
    rules_only_reason character varying(16),
    store_id uuid,
    check_summary jsonb,
    CONSTRAINT pk_batch_assistant PRIMARY KEY (batch_id),
    CONSTRAINT ck_batch_assistant_check_summary CHECK (check_summary IS NULL OR (jsonb_typeof(check_summary) = 'object' AND checked_revision IS NOT NULL)),
    CONSTRAINT ck_batch_assistant_pipeline CHECK (pipeline = 'assistant_v2'),
    CONSTRAINT ck_batch_assistant_revision CHECK (revision >= 1 AND (checked_revision IS NULL OR (checked_revision >= 1 AND checked_revision <= revision))),
    CONSTRAINT ck_batch_assistant_rules_only CHECK ((rules_only_at_utc IS NULL) = (rules_only_reason IS NULL) AND (rules_only_reason IS NULL OR rules_only_reason IN ('user', 'configuration', 'not_needed')) AND (rules_only_by_user_id IS NULL OR rules_only_at_utc IS NOT NULL)),
    CONSTRAINT ck_batch_assistant_target CHECK (target IN ('products', 'lenses')),
    CONSTRAINT fk_batch_assistant_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_assistant_rules_only_by_users FOREIGN KEY (rules_only_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_assistant_stores FOREIGN KEY (store_id) REFERENCES org.stores (id) ON DELETE RESTRICT
);

CREATE TABLE imports.batch_decisions (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    revision integer NOT NULL,
    type character varying(24) NOT NULL,
    target jsonb NOT NULL,
    value jsonb NOT NULL,
    examples_digest character(64),
    supersedes_id uuid,
    author_user_id uuid NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    request_id uuid NOT NULL,
    CONSTRAINT pk_batch_decisions PRIMARY KEY (id),
    CONSTRAINT ck_batch_decisions_digest CHECK (examples_digest IS NULL OR examples_digest ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_batch_decisions_json CHECK (jsonb_typeof(target) = 'object' AND jsonb_typeof(value) = 'object' AND octet_length(target::text) <= 4096 AND octet_length(value::text) <= 4096),
    CONSTRAINT ck_batch_decisions_revision CHECK (revision >= 2),
    CONSTRAINT ck_batch_decisions_type CHECK (type IN ('column_field', 'header_presence', 'family_class', 'continuation', 'cell_value', 'row_skip', 'row_restore', 'decimal_separator', 'file_default', 'create_reference', 'rules_only', 'revoke')),
    CONSTRAINT fk_batch_decisions_author_users FOREIGN KEY (author_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_decisions_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_decisions_supersedes FOREIGN KEY (supersedes_id) REFERENCES imports.batch_decisions (id) ON DELETE RESTRICT
);

CREATE TABLE imports.batch_originals (
    batch_id uuid NOT NULL,
    original_sha256 character(64) NOT NULL,
    byte_length integer NOT NULL,
    encoding character varying(16) NOT NULL,
    delimiter character varying(4) NOT NULL,
    bytes bytea,
    stored_at_utc timestamp with time zone NOT NULL,
    purged_at_utc timestamp with time zone,
    purged_by_user_id uuid,
    CONSTRAINT pk_batch_originals PRIMARY KEY (batch_id),
    CONSTRAINT ck_batch_originals_bytes CHECK (bytes IS NULL OR octet_length(bytes) = byte_length),
    CONSTRAINT ck_batch_originals_delimiter CHECK (delimiter IN (';', ',', 'tab', '|')),
    CONSTRAINT ck_batch_originals_encoding CHECK (encoding IN ('utf-8', 'utf-8-bom', 'utf-16le', 'utf-16be', 'windows-1252')),
    CONSTRAINT ck_batch_originals_purge CHECK ((bytes IS NULL) = (purged_at_utc IS NOT NULL) AND (purged_at_utc IS NULL) = (purged_by_user_id IS NULL)),
    CONSTRAINT ck_batch_originals_sha256 CHECK (original_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_batch_originals_size CHECK (byte_length >= 1 AND byte_length <= 2097152),
    CONSTRAINT fk_batch_originals_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_originals_purged_by_users FOREIGN KEY (purged_by_user_id) REFERENCES auth.users (id) ON DELETE RESTRICT
);

CREATE TABLE imports.batch_analysis_events (
    id bigint GENERATED ALWAYS AS IDENTITY,
    analysis_id uuid,
    batch_id uuid NOT NULL,
    at_utc timestamp with time zone NOT NULL,
    code character varying(32) NOT NULL,
    data jsonb NOT NULL,
    CONSTRAINT pk_batch_analysis_events PRIMARY KEY (id),
    CONSTRAINT ck_batch_analysis_events_code CHECK (code IN ('file_received', 'encoding_detected', 'structure_read', 'rules_validated', 'sample_prepared', 'ollama_queued', 'ollama_running', 'ollama_received', 'validation_running', 'analysis_done', 'ollama_unavailable', 'ollama_timeout', 'ollama_invalid_output', 'analysis_cancelled', 'analysis_interrupted', 'late_result_discarded', 'rules_only_chosen', 'analysis_disabled')),
    CONSTRAINT ck_batch_analysis_events_data CHECK (jsonb_typeof(data) = 'object' AND octet_length(data::text) <= 4096),
    CONSTRAINT fk_batch_analysis_events_analyses FOREIGN KEY (analysis_id) REFERENCES imports.batch_analyses (id) ON DELETE RESTRICT,
    CONSTRAINT fk_batch_analysis_events_batches FOREIGN KEY (batch_id) REFERENCES imports.batches (id) ON DELETE RESTRICT
);

CREATE INDEX ix_batch_analyses_requested_by_user_id ON imports.batch_analyses (requested_by_user_id);

CREATE INDEX ix_batch_analyses_status_queued_at ON imports.batch_analyses (status, queued_at_utc);

CREATE UNIQUE INDEX ux_batch_analyses_active ON imports.batch_analyses (batch_id) WHERE status IN ('queued', 'running');

CREATE UNIQUE INDEX ux_batch_analyses_batch_attempt ON imports.batch_analyses (batch_id, attempt);

CREATE INDEX ix_batch_analysis_events_analysis_id ON imports.batch_analysis_events (analysis_id, id);

CREATE INDEX ix_batch_analysis_events_batch_id ON imports.batch_analysis_events (batch_id, id);

CREATE INDEX ix_batch_assistant_rules_only_by_user_id ON imports.batch_assistant (rules_only_by_user_id);

CREATE INDEX ix_batch_assistant_store_id ON imports.batch_assistant (store_id);

CREATE INDEX ix_batch_decisions_author_user_id ON imports.batch_decisions (author_user_id);

CREATE INDEX ix_batch_decisions_supersedes_id ON imports.batch_decisions (supersedes_id);

CREATE UNIQUE INDEX ux_batch_decisions_batch_request ON imports.batch_decisions (batch_id, request_id);

CREATE UNIQUE INDEX ux_batch_decisions_batch_revision ON imports.batch_decisions (batch_id, revision);

CREATE INDEX ix_batch_originals_purged_by_user_id ON imports.batch_originals (purged_by_user_id);

CREATE FUNCTION imports.guard_batch_original() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.bytes IS NOT NULL AND NEW.bytes IS NULL AND NEW.purged_at_utc IS NOT NULL AND NEW.purged_by_user_id IS NOT NULL
       AND NEW.batch_id = OLD.batch_id AND NEW.original_sha256 = OLD.original_sha256 AND NEW.byte_length = OLD.byte_length
       AND NEW.encoding = OLD.encoding AND NEW.delimiter = OLD.delimiter AND NEW.stored_at_utc = OLD.stored_at_utc THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'imports.batch_originals : original immuable (purge tracee des octets seulement), suppression refusee (%)', TG_OP USING ERRCODE = '42501';
END;
$fn$;

CREATE FUNCTION imports.guard_batch_assistant() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.batch_id = OLD.batch_id AND NEW.pipeline = OLD.pipeline AND NEW.target = OLD.target
       AND NEW.store_id IS NOT DISTINCT FROM OLD.store_id AND NEW.revision >= OLD.revision
       AND (OLD.rules_only_at_utc IS NULL OR (NEW.rules_only_at_utc = OLD.rules_only_at_utc AND NEW.rules_only_reason = OLD.rules_only_reason))
       AND EXISTS (SELECT 1 FROM imports.batches b WHERE b.id = NEW.batch_id AND b.status IN ('uploaded', 'checked')) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'imports.batch_assistant : etat fige (lot clos, champ immuable ou revision decroissante), suppression refusee (%)', TG_OP USING ERRCODE = '42501';
END;
$fn$;

CREATE FUNCTION imports.guard_batch_analysis() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    IF TG_OP <> 'UPDATE' THEN
        RAISE EXCEPTION 'imports.batch_analyses : suppression refusee (%)', TG_OP USING ERRCODE = '42501';
    END IF;
    IF NEW.id <> OLD.id OR NEW.batch_id <> OLD.batch_id OR NEW.attempt <> OLD.attempt OR NEW.based_on_revision <> OLD.based_on_revision
       OR NEW.contract <> OLD.contract OR NEW.configured_model <> OLD.configured_model OR NEW.queued_at_utc <> OLD.queued_at_utc
       OR NEW.requested_by_user_id <> OLD.requested_by_user_id THEN
        RAISE EXCEPTION 'imports.batch_analyses : identite de la tentative immuable' USING ERRCODE = '42501';
    END IF;
    IF OLD.status = 'queued' AND NEW.status IN ('queued', 'running', 'cancelled', 'interrupted', 'unavailable') THEN
        RETURN NEW;
    END IF;
    IF OLD.status = 'running' AND NEW.status IN ('running', 'done', 'incomplete', 'unavailable', 'timeout', 'invalid_output', 'cancelled', 'interrupted') THEN
        RETURN NEW;
    END IF;
    IF OLD.status IN ('cancelled', 'interrupted') AND NEW.status = OLD.status AND NOT OLD.late_result_discarded AND NEW.late_result_discarded
       AND NEW.ended_at_utc = OLD.ended_at_utc AND NEW.proposal IS NOT DISTINCT FROM OLD.proposal THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'imports.batch_analyses : tentative terminee figee ou transition interdite (% -> %)', OLD.status, NEW.status USING ERRCODE = '42501';
END;
$fn$;

CREATE FUNCTION imports.reject_closed_batch_decision() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    IF EXISTS (SELECT 1 FROM imports.batches b WHERE b.id = NEW.batch_id AND b.status IN ('uploaded', 'checked')) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'imports.batch_decisions : lot clos, decision refusee' USING ERRCODE = '42501';
END;
$fn$;

CREATE TRIGGER trg_batch_originals_guard BEFORE UPDATE OR DELETE ON imports.batch_originals FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_original();

CREATE TRIGGER trg_batch_originals_no_truncate BEFORE TRUNCATE ON imports.batch_originals FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_batch_assistant_guard BEFORE UPDATE OR DELETE ON imports.batch_assistant FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_assistant();

CREATE TRIGGER trg_batch_assistant_no_truncate BEFORE TRUNCATE ON imports.batch_assistant FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_batch_analyses_guard BEFORE UPDATE OR DELETE ON imports.batch_analyses FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_analysis();

CREATE TRIGGER trg_batch_analyses_no_truncate BEFORE TRUNCATE ON imports.batch_analyses FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();

CREATE TRIGGER trg_batch_analysis_events_append_only BEFORE UPDATE OR DELETE ON imports.batch_analysis_events FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_batch_analysis_events_no_truncate BEFORE TRUNCATE ON imports.batch_analysis_events FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_batch_decisions_append_only BEFORE UPDATE OR DELETE ON imports.batch_decisions FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_batch_decisions_no_truncate BEFORE TRUNCATE ON imports.batch_decisions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();

CREATE TRIGGER trg_batch_decisions_open_batch BEFORE INSERT ON imports.batch_decisions FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_batch_decision();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261106154105_CsvImportAssistant', '10.0.4');

COMMIT;

