BEGIN;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM companies WHERE company_type IS NULL) THEN
        RAISE EXCEPTION 'Clasifica las empresas pendientes antes de finalizar company_type.';
    END IF;
END $$;
ALTER TABLE companies ALTER COLUMN company_type SET NOT NULL;
CREATE INDEX idx_companies__company_type ON companies (company_type);
CREATE INDEX idx_social_services__linkage ON social_services (period_id, company_id, student_id) INCLUDE (status)
 WHERE deleted_at IS NULL AND status IN ('approved','accepted','active','completed_pending_review');
CREATE INDEX idx_internships__linkage ON internships (period_id, company_id, student_id) INCLUDE (status)
 WHERE deleted_at IS NULL AND status IN ('approved','accepted','active','completed_pending_review');

CREATE VIEW vw_linkage_company_metrics AS
WITH assignments AS (
 SELECT 'social_service'::text AS tramite_type, student_id, period_id, company_id, status
 FROM social_services WHERE deleted_at IS NULL
 UNION ALL
 SELECT 'internship'::text AS tramite_type, student_id, period_id, company_id, status
 FROM internships WHERE deleted_at IS NULL
)
SELECT a.tramite_type, a.period_id, c.id AS company_id, c.name AS company_name, c.company_type,
 COUNT(*) AS projects, COUNT(DISTINCT a.student_id) AS assigned_students,
 COUNT(DISTINCT a.student_id) FILTER (WHERE a.status = 'active') AS active_students
FROM assignments a
JOIN companies c ON c.id = a.company_id AND c.deleted_at IS NULL
JOIN students s ON s.id = a.student_id AND s.deleted_at IS NULL
WHERE a.status IN ('approved','accepted','active','completed_pending_review')
GROUP BY a.tramite_type, a.period_id, c.id, c.name, c.company_type;

CREATE VIEW vw_linkage_company_type_metrics AS
SELECT tramite_type, period_id, company_type,
 SUM(projects)::bigint AS projects, SUM(assigned_students)::bigint AS assigned_students,
 SUM(active_students)::bigint AS active_students
FROM vw_linkage_company_metrics
GROUP BY tramite_type, period_id, company_type;
COMMIT;
