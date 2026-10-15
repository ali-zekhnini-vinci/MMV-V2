START TRANSACTION;
DROP TABLE auth.bootstrap_record;

DROP TABLE auth.sessions;

DROP TABLE auth.user_permissions;

DROP TABLE auth.users;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261015223849_AuthIdentity';

COMMIT;

