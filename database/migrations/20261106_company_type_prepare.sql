BEGIN;
ALTER TABLE companies ADD COLUMN company_type varchar(16) NULL;
ALTER TABLE companies ADD CONSTRAINT ck_companies__type CHECK
    (company_type IN ('public', 'micro', 'small', 'medium', 'large', 'social'));
ALTER TABLE companies ADD CONSTRAINT ck_companies__type_dimensions CHECK
    (sector = 'educational' OR sector = 'public' AND company_type = 'public'
     OR sector = 'social' AND company_type = 'social'
     OR sector = 'private' AND company_type = company_size);
UPDATE companies SET company_type = CASE
    WHEN sector IN ('public', 'social') THEN sector
    WHEN sector = 'private' AND company_size IN ('micro', 'small', 'medium', 'large') THEN company_size
    ELSE NULL END;
COMMIT;
-- Clasificar individualmente estos registros antes de ejecutar la migración final.
SELECT id, name, sector, company_size FROM companies WHERE company_type IS NULL ORDER BY id;
