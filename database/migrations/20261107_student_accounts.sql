BEGIN;
LOCK TABLE users, students, user_roles, roles IN SHARE ROW EXCLUSIVE MODE;

DO $$
DECLARE conflicts text;
BEGIN
    SELECT string_agg(s.control_number, ', ') INTO conflicts
    FROM students s LEFT JOIN users u ON u.id=s.user_id
    WHERE s.deleted_at IS NULL AND s.user_id IS NOT NULL AND (
        u.id IS NULL OR u.deleted_at IS NOT NULL
        OR s.institutional_email IS NULL OR length(btrim(s.institutional_email)) NOT BETWEEN 3 AND 254
        OR btrim(s.institutional_email) !~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$'
        OR (SELECT count(*) FROM students other WHERE other.user_id=s.user_id AND other.deleted_at IS NULL) <> 1
        OR (SELECT count(*) FROM user_roles ur JOIN roles r ON r.id=ur.role_id
            WHERE ur.user_id=u.id AND ur.deleted_at IS NULL AND r.deleted_at IS NULL) <> 1
        OR NOT EXISTS (SELECT 1 FROM user_roles ur JOIN roles r ON r.id=ur.role_id
            WHERE ur.user_id=u.id AND ur.deleted_at IS NULL AND r.deleted_at IS NULL AND r.name='student'));
    IF conflicts IS NOT NULL THEN RAISE EXCEPTION 'Cuentas de alumno inconsistentes: %', conflicts; END IF;

    IF EXISTS (SELECT lower(btrim(institutional_email)) FROM students
        WHERE deleted_at IS NULL AND institutional_email IS NOT NULL
        GROUP BY lower(btrim(institutional_email)) HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'Correos institucionales duplicados; corregir antes de migrar';
    END IF;

    -- Compare the intended final identities, including unchanged staff accounts.
    IF EXISTS (SELECT login FROM (
        SELECT lower(btrim(coalesce(s.institutional_email,u.username))) AS login
        FROM users u LEFT JOIN students s ON s.user_id=u.id AND s.deleted_at IS NULL
        WHERE u.deleted_at IS NULL) intended GROUP BY login HAVING count(*)>1)
    OR EXISTS (SELECT email FROM (
        SELECT lower(btrim(coalesce(s.institutional_email,u.email))) AS email
        FROM users u LEFT JOIN students s ON s.user_id=u.id AND s.deleted_at IS NULL
        WHERE u.deleted_at IS NULL) intended GROUP BY email HAVING count(*)>1) THEN
        RAISE EXCEPTION 'Colisión de usuarios o correos; ninguna cuenta fue modificada';
    END IF;
END $$;

ALTER TABLE users ALTER COLUMN username TYPE varchar(254);
ALTER TABLE users ADD COLUMN session_version integer NOT NULL DEFAULT 0;
UPDATE users u SET username=lower(btrim(s.institutional_email)),email=lower(btrim(s.institutional_email)),
    session_version=session_version+1,updated_at=now()
FROM students s WHERE s.user_id=u.id AND s.deleted_at IS NULL AND u.deleted_at IS NULL;
CREATE UNIQUE INDEX uq_users__username_ci ON users(lower(username)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX uq_users__email_ci ON users(lower(email)) WHERE deleted_at IS NULL;
ALTER TABLE student_import_batches ADD COLUMN created_accounts integer NOT NULL DEFAULT 0,
    ADD COLUMN existing_accounts integer NOT NULL DEFAULT 0;
COMMIT;
