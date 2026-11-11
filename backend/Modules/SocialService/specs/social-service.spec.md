> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../docs/specs/document-workflow-v2.spec.md). Este archivo conserva contexto previo compatible.

# Social Service backend

Contrato compartido: [spec raíz de servicio social](../../../../docs/specs/social-service.spec.md).

## Responsabilidades
- `SocialServicesController`: Trámite maestro de Servicio Social, apertura automatizada (Anexo XVIII + Vigencia IMSS), emisión de Carta de Presentación con folio (`SS_CP...`), generación automática de Carta de Aceptación cuando es local en el TecNM (`modality = "internal"`), progreso documental (0–100%) y `allowedActions`.
- `CommitmentsController`, `BiMonthlyReportsController` & `EvaluationsController`: Formularios para que el alumno llene todos los datos de sus formatos (Anexo XX, Reportes Bimestrales 1–3 Anexo XXII, Evaluaciones Cualitativas Anexos XXIII/XXIV y Actividades Anexo XXV) y los imprima listos para pedir firma y sello.
- `ReleaseController`: Validación de documentos firmados/sellados subidos (`SOL`, `CA`, `CC`, `R1–R3`, `EV1–EV3`, `CT`), liberación con folio atómico (`SSC...`) y envío de acta a Servicios Escolares (`DGTV...`) sin bloqueos por horas ni promedios numéricos.

## Prellenado SS

Aplicar la matriz de prellenado del contrato documental v2: reportes con evaluador propio o supervisor general; XXIII con alumno, programa y supervisor del trámite; XXIV con alumno y programa; XXV conserva su prellenado. Firmas y sellos manuales vacíos. Evaluaciones se descargan mediante pdfKey, conservando códigos históricos y sin registrar calificaciones.

## Carta de Terminación SS

CT descarga el Anexo XIX prellenado mediante pdfKey termination_letter. Reutiliza alumno, programa, actividades, fechas, horas registradas, ciudad y supervisor según el contrato documental v2. Conserva destinatarios impresos, firmas/sello manuales vacíos y carga/revisión del PDF firmado; descargar no altera el trámite.

Carta Compromiso: descarga directa con domicilio de solicitud, teléfono del perfil y domicilio de la dependencia, usando respaldos registrados cuando falten. La fecha impresa se calcula al descargar en America/Mexico_City, sin reutilizar la fecha guardada. No exige captura previa de CC; firma del alumno vacía y ninguna escritura al descargar.

Apertura SS: enviar Solicitud e IMSS juntos mediante POST /api/social-services/submissions multipart (application JSON, imssFile opcional con evidencia vigente pendiente/aprobada). No exige aprobación IMSS previa al envío. Vista previa antes de confirmar; aprobación administrativa sigue exigiendo IMSS aprobado. No aplica a PP.

## Acta Individual

GET /api/social-services/{id}/pdf/individual_act entrega PDF oficial únicamente a admin, social_service_officer y department_head, para trámites released. Prellena alumno, matrícula, carrera, dependencia, actividades de reportes ordenados sin duplicados (respaldo solicitud), fechas del trámite y 480 horas. Expedición: día de descarga en America/Mexico_City mediante TimeProvider. Autoridades activas outreach_division_head e institute_director, con título opcional, nombre y cargo; solo firmas vacías. Faltantes devuelven 422 documents.data_missing por campo; desbordamiento 422 documents.text_overflow. Plantilla/fuente inválida devuelve 409. No modifica estado, evidencia, folios ni envío a Escolares. Solo PDF individual, sin reverso. Descarga administrativa con carga, prevención de doble petición y errores visibles; alumno sin acceso.
