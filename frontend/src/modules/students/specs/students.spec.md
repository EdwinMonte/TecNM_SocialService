> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../../docs/specs/document-workflow-v2.spec.md). Sin datos ficticios ante fallos; estados y acciones los decide la API.

# Especificación Frontend: Módulo Estudiantes & Padrón

> **Contrato Backend:** [`docs/specs/students.spec.md`](../../../../../docs/specs/students.spec.md)  
> **Roles:** `student` (perfil propio `/me`), `admin`, `social_service_officer`, `internship_coordinator` (gestión completa), `department_head` (lectura).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/students` | `students-list` | `StudentsListView.vue` | Staff, Dept Head | Listado general, filtros avanzados y acciones de padrón. |
| `/students/import` | `students-import` | `StudentImportView.vue` | Staff | Asistente de importación masiva por Excel (`.xlsx`). |
| `/profile` (o `/students/me`) | `student-profile` | `StudentProfileView.vue` | `student` | Consulta de estatus escolar, edición de contacto y subida de Vigencia IMSS. |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Vista `/students` (Directorio y Padrón Administrativo)
- **Contenedor:** `UiBasePage` (`fluid=true`, título: "Padrón de Estudiantes").
- **Header Actions:**
  - Botón secundario con icono: "Importar Excel" -> navega a `/students/import`.
  - Botón principal: "Nuevo Estudiante" -> abre `UiDialog` de alta individual.
- **Barra de Filtros:**
  - `UiField` con buscador por matrícula, nombre o apellidos (con debounce 300 ms).
  - Select de Carrera (`/api/careers/options`).
  - Select de Semestre (1 a 15).
  - Select de Turno (`morning`, `afternoon`, `night`).
  - Toggle o Select de Elegibilidad: Todos / Elegibles SS / Elegibles PP.
  - Toggle o Select de Seguro IMSS: Todos / Vigente / Pendiente.
- **Tabla de Estudiantes (`UiTable`):**
  - Columnas:
    - `controlNumber`: Matrícula en mono/negrita (ej. `I23050363`).
    - `fullName`: Nombre completo del alumno.
    - `career`: Clave de carrera (badge informativo).
    - `semester`: Semestre y turno.
    - `progress`: Porcentaje de créditos reticulares (`curriculumProgressPercentage%`).
    - `imss`: `UiBadge` (`success` "Vigente", `warning` "Subido", `danger` "Sin vigencia").
    - `status`: `UiBadge` (`success` Inscrito / `danger` Baja o Suspendido).
    - `actions`: Botón "Ver Expediente", "Editar Datos" y "Generar/Resetear Cuenta".
- **Modales:**
  - `StudentFormDialog.vue`: Modal con pestañas o secciones claras para Datos Personales (CURP, Nombre, Apellidos, Sexo), Datos Escolares (Carrera, Semestre, Turno, Créditos) y Datos de Contacto (Emails, Teléfono).
  - `ProvisionAccountDialog.vue`: Muestra usuario generado (`username`), estado real y matrícula como contraseña habitual inicial.

---

### 2.2 Vista `/students/import` (Asistente Excel Masivo)
- **Contenedor:** `UiBasePage` con breadcrumb hacia `/students`.
- **Paso 1: Descarga de Plantilla Oficial:**
  - Tarjeta `UiCard` explicativa que incluye botón para descargar plantilla oficial `.xlsx` vía `GET /api/student-imports/template`.
  - Instrucciones claras sobre nombres de columnas exactos (`control_number, curp, first_surname, first_name, career_code, current_semester, completed_credits, shift, institutional_email, personal_email, phone_number`).
- **Paso 2: Carga de Archivo:**
  - Zona de soltar archivo (Drag & Drop) que valida extensión `.xlsx` y tamaño `<= 10 MB`.
  - Selector de Periodo Académico de destino (`/api/academic-periods/options`).
  - Checkbox opcional: "Proveer cuentas de acceso automáticamente a nuevos alumnos".
- **Paso 3: Resultados y Resumen:**
  - Al completar la subida (`POST /api/student-imports`), renderiza métricas (`UiMetric`): Total procesados, Nuevos insertados, Actualizados y Errores.
  - Tabla de incidencias si existieron filas con datos no válidos.

---

### 2.3 Vista `/profile` (Perfil del Estudiante)
- **Contenedor:** `UiBasePage` centrado o fluido con dos columnas:
  - **Columna 1 (Resumen Escolar):** Tarjeta con Matrícula, Carrera, Semestre, Turno, Avance Reticular y estatus de elegibilidad para Servicio Social y Prácticas Profesionales.
  - **Columna 2 (Datos de Contacto):** Formulario editable para Teléfono y Correo personal (`PUT /api/students/me/contact`).
- **Sección de Vigencia IMSS:**
  - Estado actual con `UiAlert` o `UiBadge`.
  - Si no tiene documento o fue rechazado: Zona de carga de PDF (`PUT /api/students/me/imss`, máx. 5 MB).
  - Si ya tiene archivo subido: Botón "Ver Constancia Actual" (abre visor PDF) y botón "Reemplazar Constancia".


## Seguimiento semestral SS

El backend calcula `socialServiceFollowUp` independientemente del estado del trámite. Sin PDF aportado por el alumno y aprobado en el expediente SS vigente: periodo inicial = `first_notice`, uno posterior = `second_notice`, dos = `third_notice`, tres o más = `non_compliant`. Contar periodos por fecha; no usar semestre escolar. IMSS, origen system, borrados y sustituidos no acreditan avance. Con evidencia aprobada: `in_progress`, incluso antes de activar. Liberado prevalece; PNC formal se conserva si no existe nuevo trámite vigente. Cancelados no aportan evidencia. PNC del padrón no crea casos de Calidad ni cierra trámites.

Guardar `socialServiceEligiblePeriodId` al crear: periodo del Excel o selección manual (default activo). Reimportar nunca lo modifica. Admin puede corregirlo; registrar autor y fecha. Registros anteriores sin origen comprobado quedan `pending_setup`; no inferir origen del último lote. Sin periodo activo tampoco calcular avisos.

`GET /api/students` agrega `socialServiceFollowUp` (status, eligiblePeriodId/Code, trackingPeriodId/Code, elapsedSemesters, socialServiceId, approvedDocumentsCount, lastApprovedAt, progressAssessment, notes, reviewedAt). Filtros `followUpStatus` y `followUpProgress` antes de paginar. Valores de valoración: `consistent`, `low_progress`; ausencia = pendiente. Valoración por expediente/periodo con autor y fecha. No heredar entre semestres. Poco avance no reactiva avisos; dos semestres son referencia, no límite.

`PUT /api/students/{id}/social-service-follow-up/review`: `{ periodId, assessment, notes? }`, admin/encargado SS; exige periodo activo y evidencia aprobada. PUT idempotente, único por expediente/periodo. Observación hasta 1000 caracteres. Conflicto concurrente 409. El coordinador PP no recibe seguimiento SS. Encargado SS consulta candidatos sin trámite mediante resumen: identidad, carrera y seguimiento; otros campos nulos, sin habilitar perfil completo. `canReadProfile` indica acceso a las acciones existentes.

UI: columna Seguimiento SS, valoración, periodo inicial, motivo, filtros y diálogo Valorar seguimiento. Mostrar periodo activo. Conservar pendientes de cohortes anteriores. Textos y colores accesibles. Sin cortes históricos inmutables.

## Padrón compacto y detalle (vigente)

Una sola tabla en `/students`: nombre, matrícula, carrera/semestre, seguimiento SS (según rol) y Ver detalle. La cola IMSS de preingreso permanece en `/expedients/review`; no se monta ni consulta automáticamente en el padrón. El detalle reutiliza UiDialog, carga datos completos solo para perfiles autorizados y concentra datos personales, académicos, contacto, cuenta, IMSS y seguimiento. Candidatos restringidos muestran únicamente el resumen permitido, sin peticiones al perfil. Acciones de editar, cuentas, dictamen y valoración quedan dentro del detalle. Filtros principales: búsqueda, carrera y seguimiento; turno/IMSS/constancia dentro de Más filtros. Tabla sin cortes en escritorio, presentación por filas compactas en móvil. Mostrar carga, error recuperable, vacío, paginación y cierre por Escape con retorno de foco. Mantener datos y permisos existentes.
