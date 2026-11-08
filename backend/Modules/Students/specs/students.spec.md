> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../docs/specs/document-workflow-v2.spec.md). Este archivo conserva contexto previo compatible.

# Students backend

Contrato compartido: [spec raíz de estudiantes](../../../../docs/specs/students.spec.md).

## Responsabilidades
- `StudentsController`: Padrón de alumnos, alta individual con botón para casos específicos, consulta de elegibilidad (`/eligibility`), expediente `/me` y carga de Constancia de Vigencia de Derechos del IMSS.
- `StudentImportsController`: Procesamiento de lotes de importación masiva desde Excel (`student_import_batches`) para cargar los alumnos que pueden hacer el servicio y reporte de filas con error.

## Seguimiento semestral SS

El backend calcula `socialServiceFollowUp` independientemente del estado del trámite. Sin PDF aportado por el alumno y aprobado en el expediente SS vigente: periodo inicial = `first_notice`, uno posterior = `second_notice`, dos = `third_notice`, tres o más = `non_compliant`. Contar periodos por fecha; no usar semestre escolar. IMSS, origen system, borrados y sustituidos no acreditan avance. Con evidencia aprobada: `in_progress`, incluso antes de activar. Liberado prevalece; PNC formal se conserva si no existe nuevo trámite vigente. Cancelados no aportan evidencia. PNC del padrón no crea casos de Calidad ni cierra trámites.

Guardar `socialServiceEligiblePeriodId` al crear: periodo del Excel o selección manual (default activo). Reimportar nunca lo modifica. Admin puede corregirlo; registrar autor y fecha. Registros anteriores sin origen comprobado quedan `pending_setup`; no inferir origen del último lote. Sin periodo activo tampoco calcular avisos.

`GET /api/students` agrega `socialServiceFollowUp` (status, eligiblePeriodId/Code, trackingPeriodId/Code, elapsedSemesters, socialServiceId, approvedDocumentsCount, lastApprovedAt, progressAssessment, notes, reviewedAt). Filtros `followUpStatus` y `followUpProgress` antes de paginar. Valores de valoración: `consistent`, `low_progress`; ausencia = pendiente. Valoración por expediente/periodo con autor y fecha. No heredar entre semestres. Poco avance no reactiva avisos; dos semestres son referencia, no límite.

`PUT /api/students/{id}/social-service-follow-up/review`: `{ periodId, assessment, notes? }`, admin/encargado SS; exige periodo activo y evidencia aprobada. PUT idempotente, único por expediente/periodo. Observación hasta 1000 caracteres. Conflicto concurrente 409. El coordinador PP no recibe seguimiento SS. Encargado SS consulta candidatos sin trámite mediante resumen: identidad, carrera y seguimiento; otros campos nulos, sin habilitar perfil completo. `canReadProfile` indica acceso a las acciones existentes.

UI: columna Seguimiento SS, valoración, periodo inicial, motivo, filtros y diálogo Valorar seguimiento. Mostrar periodo activo. Conservar pendientes de cohortes anteriores. Textos y colores accesibles. Sin cortes históricos inmutables.
