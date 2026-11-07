> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../docs/specs/document-workflow-v2.spec.md). Este archivo conserva contexto previo compatible.

# Students backend

Contrato compartido: [spec raíz de estudiantes](../../../../docs/specs/students.spec.md).

## Responsabilidades
- `StudentsController`: Padrón de alumnos, alta individual con botón para casos específicos, consulta de elegibilidad (`/eligibility`), expediente `/me` y carga de Constancia de Vigencia de Derechos del IMSS.
- `StudentImportsController`: Procesamiento de lotes de importación masiva desde Excel (`student_import_batches`) para cargar los alumnos que pueden hacer el servicio y reporte de filas con error.
