START TRANSACTION;
DROP TABLE audit.events;

ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission;

ALTER TABLE auth.user_permissions ADD CONSTRAINT ck_user_permissions_permission CHECK (permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel'));

DROP FUNCTION audit.reject_event_mutation();

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261018163108_AuditJournal';

COMMIT;

