START TRANSACTION;
DROP TABLE clients.clients;

ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;

DROP SEQUENCE clients.client_reference_seq;

ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin'));

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261016223616_ClientsModule';

COMMIT;

