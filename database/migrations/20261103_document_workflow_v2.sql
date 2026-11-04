BEGIN;
ALTER TABLE expedient_documents ADD COLUMN document_date date NULL,
 ADD COLUMN origin varchar(16) NOT NULL DEFAULT 'uploaded',
 ADD COLUMN content_sha256 varchar(64) NULL, ADD COLUMN template_version varchar(100) NULL;
ALTER TABLE expedient_documents ADD CONSTRAINT ck_documents_origin CHECK (origin IN ('uploaded','system'));
ALTER TABLE social_services ADD COLUMN document_workflow_version integer NOT NULL DEFAULT 1,
 ADD COLUMN admission_imss_document_id bigint NULL REFERENCES expedient_documents(id) ON DELETE RESTRICT;
ALTER TABLE internships ADD COLUMN document_workflow_version integer NOT NULL DEFAULT 1,
 ADD COLUMN admission_imss_document_id bigint NULL REFERENCES expedient_documents(id) ON DELETE RESTRICT,
 ADD COLUMN evaluation_format_start_date date NULL, ADD COLUMN evaluation_format_end_date date NULL;
ALTER TABLE internships ADD CONSTRAINT ck_internships_evaluation_format_dates CHECK (evaluation_format_end_date >= evaluation_format_start_date);
ALTER TABLE social_service_commitments ALTER COLUMN signed_at DROP NOT NULL,
 ALTER COLUMN signed_at DROP DEFAULT, ALTER COLUMN is_signed_by_student SET DEFAULT false;
UPDATE internships i SET evaluation_format_start_date=e.evaluation_start_date,
 evaluation_format_end_date=e.evaluation_end_date FROM internship_evaluations e
 WHERE e.internship_id=i.id AND e.deleted_at IS NULL;
-- Preserve duplicates for audit; latest approved wins, otherwise latest submission.
WITH ranked AS (SELECT id, row_number() OVER (
 PARTITION BY student_id, tramite_type, tramite_id, document_code
 ORDER BY (status='approved') DESC, created_at DESC, id DESC) AS n
 FROM expedient_documents WHERE deleted_at IS NULL AND status <> 'superseded')
UPDATE expedient_documents SET status='superseded' WHERE id IN (SELECT id FROM ranked WHERE n>1);
CREATE UNIQUE INDEX uq_expedient_documents__current ON expedient_documents
 (student_id,tramite_type,tramite_id,document_code) NULLS NOT DISTINCT
 WHERE deleted_at IS NULL AND status <> 'superseded';
-- Old profile file paths remain untouched. Re-upload/confirm PDF and date before new admission.
CREATE UNIQUE INDEX uq_social_services__student_open ON social_services(student_id) WHERE deleted_at IS NULL AND status NOT IN ('cancelled','non_compliant');
CREATE UNIQUE INDEX uq_internships__student_open ON internships(student_id) WHERE deleted_at IS NULL AND status NOT IN ('cancelled','non_compliant');
COMMIT;
