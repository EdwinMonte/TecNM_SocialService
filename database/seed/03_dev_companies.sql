BEGIN;

-- ============================================================================
-- 1. EMPRESAS Y DEPENDENCIAS RECEPTORAS (Región Centro de Coahuila)
-- ============================================================================
INSERT INTO companies (
    id, rfc, name, commercial_name, company_type, sector, company_size, is_tecnm_internal,
    address, city, state, postal_code, phone_number, email, is_active
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1,
        NULL,
        'Instituto Tecnológico Superior de Monclova Ejército Mexicano',
        'TecNM Campus Monclova',
        'public', -- Clasificación explícita de esta institución pública demo.
        'educational',
        'not_applicable',
        true,
        'Av. Tecnológico No. 4801, Col. Tecnológico',
        'Monclova',
        'Coahuila',
        '25716',
        '8666491200',
        'vinculacion@monclova.tecnm.mx',
        true
    ),
    (
        2,
        'PMM850101ABC',
        'Presidencia Municipal de Monclova',
        'Ayuntamiento de Monclova',
        'public',
        'public',
        'large',
        false,
        'Zaragoza No. 402, Zona Centro',
        'Monclova',
        'Coahuila',
        '25700',
        '8666490000',
        'atencion@monclova.gob.mx',
        true
    ),
    (
        3,
        'GGI890101XYZ',
        'Gunderson Gimsa S.A. de C.V.',
        'Gunderson',
        'large',
        'private',
        'large',
        false,
        'Carretera 57 Km 12.5',
        'Frontera',
        'Coahuila',
        '25600',
        '8666498000',
        'rh@gunderson-gimsa.com.mx',
        true
    ),
    (
        4,
        'SIM150620AA1',
        'Soluciones Industriales de Monclova S.A. de C.V.',
        'SIMSA Industrial',
        'medium',
        'private',
        'medium',
        false,
        'Blvd. Harold R. Pape No. 1200',
        'Monclova',
        'Coahuila',
        '25750',
        '8666324400',
        'contacto@simsa-ind.com',
        true
    )
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    rfc = EXCLUDED.rfc,
    commercial_name = EXCLUDED.commercial_name,
    company_type = EXCLUDED.company_type,
    sector = EXCLUDED.sector,
    company_size = EXCLUDED.company_size,
    is_tecnm_internal = EXCLUDED.is_tecnm_internal,
    address = EXCLUDED.address,
    city = EXCLUDED.city,
    state = EXCLUDED.state,
    postal_code = EXCLUDED.postal_code,
    phone_number = EXCLUDED.phone_number,
    email = EXCLUDED.email,
    is_active = EXCLUDED.is_active;

SELECT setval(pg_get_serial_sequence('companies', 'id'), (SELECT MAX(id) FROM companies));

-- ============================================================================
-- 2. SUPERVISORES DE EMPRESA
-- ============================================================================
INSERT INTO company_supervisors (
    id, company_id, full_name, position, department, email, phone_number, is_signatory, is_active
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1,
        1,
        'Ing. Marco Antonio Salazar Silva',
        'Jefe de Departamento de Sistemas y Computación',
        'Sistemas y Computación',
        'marco.salazar@monclova.tecnm.mx',
        '8666491200 Ext. 110',
        true,
        true
    ),
    (
        2,
        2,
        'Lic. Cynthia Elizabeth Ramos Vega',
        'Directora de Recursos Humanos',
        'Recursos Humanos',
        'cynthia.ramos@monclova.gob.mx',
        '8666490015',
        true,
        true
    ),
    (
        3,
        3,
        'Ing. Roberto Garza Villarreal',
        'Gerente de Aseguramiento de Calidad',
        'Calidad y Procesos',
        'roberto.garza@gunderson-gimsa.com.mx',
        '8666498050',
        true,
        true
    ),
    (
        4,
        4,
        'Ing. Sofía Valdés Cantú',
        'Líder de Desarrollo de Software',
        'Tecnologías de la Información',
        'sofia.valdes@simsa-ind.com',
        '8666324420',
        true,
        true
    )
ON CONFLICT (id) DO UPDATE SET
    company_id = EXCLUDED.company_id,
    full_name = EXCLUDED.full_name,
    position = EXCLUDED.position,
    department = EXCLUDED.department,
    email = EXCLUDED.email,
    phone_number = EXCLUDED.phone_number,
    is_signatory = EXCLUDED.is_signatory,
    is_active = EXCLUDED.is_active;

SELECT setval(pg_get_serial_sequence('company_supervisors', 'id'), (SELECT MAX(id) FROM company_supervisors));

-- ============================================================================
-- 3. CONVENIOS DE COLABORACIÓN INSTITUCIONAL
-- ============================================================================
INSERT INTO agreements (
    id, company_id, agreement_number, agreement_type, description,
    signed_date, expiration_date, is_active, document_url
)
OVERRIDING SYSTEM VALUE
VALUES
    (
        1,
        2,
        'CONV-ITSM-MUN-2025-01',
        'general_framework',
        'Convenio Marco de Vinculación y Colaboración entre el R. Ayuntamiento de Monclova y el ITSM.',
        '2025-01-15',
        '2027-01-15',
        true,
        NULL
    ),
    (
        2,
        3,
        'CONV-ITSM-GG-2024-03',
        'collaboration',
        'Convenio de colaboración para realización de Servicio Social y Prácticas Profesionales.',
        '2024-03-01',
        '2026-11-20',
        true,
        NULL
    ),
    (
        3,
        4,
        'CONV-ITSM-SIMSA-2023-01',
        'internship',
        'Convenio específico para residencia profesional y prácticas en áreas tecnológicas.',
        '2023-08-10',
        '2024-08-10',
        true,
        NULL
    )
ON CONFLICT (id) DO UPDATE SET
    company_id = EXCLUDED.company_id,
    agreement_number = EXCLUDED.agreement_number,
    agreement_type = EXCLUDED.agreement_type,
    description = EXCLUDED.description,
    signed_date = EXCLUDED.signed_date,
    expiration_date = EXCLUDED.expiration_date,
    is_active = EXCLUDED.is_active,
    document_url = EXCLUDED.document_url;

SELECT setval(pg_get_serial_sequence('agreements', 'id'), (SELECT MAX(id) FROM agreements));

COMMIT;
