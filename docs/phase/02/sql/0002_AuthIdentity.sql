START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'auth') THEN
        CREATE SCHEMA auth;
    END IF;
END $EF$;

CREATE TABLE auth.bootstrap_record (
    id smallint NOT NULL,
    administrator_id uuid NOT NULL,
    completed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_bootstrap_record PRIMARY KEY (id),
    CONSTRAINT ck_bootstrap_record_singleton CHECK (id = 1)
);

CREATE TABLE auth.users (
    id uuid NOT NULL,
    user_name character varying(64) NOT NULL,
    normalized_user_name character varying(64) NOT NULL,
    display_name character varying(128) NOT NULL,
    password_hash text NOT NULL,
    is_active boolean NOT NULL,
    failed_login_count integer NOT NULL,
    lockout_end_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT ck_users_failed_login_count CHECK (failed_login_count >= 0)
);

CREATE TABLE auth.sessions (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    token_hash bytea NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    last_seen_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    revoked_at_utc timestamp with time zone,
    revocation_reason character varying(32),
    CONSTRAINT pk_sessions PRIMARY KEY (id),
    CONSTRAINT ck_sessions_expiry_after_creation CHECK (expires_at_utc > created_at_utc),
    CONSTRAINT ck_sessions_token_hash_length CHECK (octet_length(token_hash) = 32),
    CONSTRAINT fk_sessions_users FOREIGN KEY (user_id) REFERENCES auth.users (id) ON DELETE CASCADE
);

CREATE TABLE auth.user_permissions (
    user_id uuid NOT NULL,
    permission character varying(64) NOT NULL,
    CONSTRAINT pk_user_permissions PRIMARY KEY (user_id, permission),
    CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin')),
    CONSTRAINT fk_user_permissions_users FOREIGN KEY (user_id) REFERENCES auth.users (id) ON DELETE CASCADE
);

CREATE INDEX ix_sessions_user_id ON auth.sessions (user_id);

CREATE UNIQUE INDEX ux_sessions_token_hash ON auth.sessions (token_hash);

CREATE UNIQUE INDEX ux_users_normalized_user_name ON auth.users (normalized_user_name);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015223849_AuthIdentity', '10.0.4');

COMMIT;

