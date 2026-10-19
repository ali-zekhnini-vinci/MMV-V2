START TRANSACTION;
DROP TABLE appointments.appointments;

DROP TABLE appointments.responsibles;

ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;

ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive'));

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261017150504_AppointmentsModule';

COMMIT;

