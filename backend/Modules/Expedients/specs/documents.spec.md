> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../docs/specs/document-workflow-v2.spec.md). Este archivo conserva contexto previo compatible.

# Expedients backend

Contrato compartido: [spec raíz de documentos](../../../../docs/specs/documents.spec.md).

## Responsabilidades
- `IFileStorage` / `LocalFileStorage` (`Core/Storage/FileStorage.cs`): Abstracción de almacenamiento físico local bajo `Storage:RootPath`, validación de encabezado mágico `%PDF` y tamaño máximo configurable.
- `DocumentsService` (`Modules/Expedients/DocumentsService.cs`):
  - Subida polimórfica de documentos (`social_service`, `internship`, `general`) con ruta relativa `{studentId}/{tramiteType}/{guid}.pdf`.
  - Versionado automático: al subir un nuevo archivo para el mismo `(studentId, tramiteType, tramiteId, documentCode)`, las versiones previas en estado `pending` o `rejected` pasan automáticamente a `superseded`. Si ya existe una versión `approved`, responde `409 documents.already_approved`.
  - Revisión documental (`POST /api/documents/{id}/review`): transición `pending -> approved | rejected`, validación de permisos por tipo de trámite (`SocialServiceStaff` vs `InternshipStaff`) y sincronización automática de fechas de hitos (`acceptance_letter_date`, `termination_letter_date`) y vigencia IMSS.
  - Cola de revisión (`GET /api/documents/review-queue`) con datos del estudiante y carrera para validación rápida de firma y sello.
- `DocumentsController` (`Modules/Expedients/DocumentsController.cs`):
  - Endpoints `/api/students/{studentId}/documents`, `/api/students/me/documents`, `/api/documents/{id}/file`, `/api/documents/{id}/review`, `/api/documents/{id}` y `/api/documents/review-queue`.

## Prellenado SS

Aplicar la matriz de prellenado del contrato documental v2: reportes con evaluador propio o supervisor general; XXIII con alumno, programa y supervisor del trámite; XXIV con alumno y programa; XXV conserva su prellenado. Firmas y sellos manuales vacíos. Evaluaciones se descargan mediante pdfKey, conservando códigos históricos y sin registrar calificaciones.

## Carta de Terminación SS

CT descarga el Anexo XIX prellenado mediante pdfKey termination_letter. Reutiliza alumno, programa, actividades, fechas, horas registradas, ciudad y supervisor según el contrato documental v2. Conserva destinatarios impresos, firmas/sello manuales vacíos y carga/revisión del PDF firmado; descargar no altera el trámite.

Carta Compromiso: descarga directa con domicilio de solicitud, teléfono del perfil y domicilio de la dependencia, usando respaldos registrados cuando falten. La fecha impresa se calcula al descargar en America/Mexico_City, sin reutilizar la fecha guardada. No exige captura previa de CC; firma del alumno vacía y ninguna escritura al descargar.
