# Plan de Construcción Frontend — Flujo Real por Rol, Formatos Automatizados y Estadísticos en Vivo

> **Objetivo:** Transformar las vistas genéricas/mock actuales del frontend en una SPA institucional 100% conectada al backend .NET 10, diferenciada por rol de usuario y alineada al flujo real de **Servicio Social**, **Prácticas Profesionales** y **Estadísticos (`SEGUIMIENTO VINCULACIÓN1.xlsx`)**.
> **Reglas y arquitectura:** Ver [`frontend/AGENTS.md`](../../frontend/AGENTS.md) y [`docs/architecture/FRONTEND_ARCHITECTURE.md`](../architecture/FRONTEND_ARCHITECTURE.md).
> **Contratos canónicos:** Ver [`docs/specs/`](../specs/).

---

## 🔍 1. Diagnóstico del Estado Actual del Frontend

1. **Vistas con datos falsos (Mock):**
   - `SessionView.vue` (ruta `/`) muestra contadores (`142`, `58`, `19`) y alumnos inventados quemados en código en lugar de consultar `/api/reports/dashboard` o redirigir al portal del estudiante.
   - `ExpedientsReviewView.vue` tenía un desajuste en el tipo de respuesta (`PagedResponse` vs arreglo plano) y usaba `PUT` en lugar de `POST /api/documents/{id}/review`, cayendo siempre en datos falsos (`fallbackQueue`).
   - `mainLayout.config.ts` muestra todos los menús a todos los usuarios por igual y tiene badges fijos (`142`, `58`, `19`).
2. **Desajustes en clientes Axios (`src/modules/*/api/`):**
   - `studentsApi.ts`: usa `POST /student-imports/template` (debe ser `GET /api/student-imports/template`), `POST /student-imports/upload` (debe ser `POST /api/student-imports` con `periodId`), y `PUT /students/me/imss` enviando JSON (debe enviar `multipart/form-data` con el PDF de Constancia de Vigencia IMSS).
   - `socialServiceApi.ts`: usa `/social-services/my` y `/social-services/request` (deben ser `GET /api/social-services/me` y `POST /api/social-services`).
   - `internshipsApi.ts`: usa `/internships/my` y `/internships/request` (deben ser `GET /api/internships/me` y `POST /api/internships`).
   - `reportsApi.ts`: `getTrackingData` esperaba `{ items }` cuando el backend devuelve `TrackingRowResponse[]` directo; `getSemesterStatistics` y `get911Report` no enviaban `tramiteType`.

---

## 👥 2. Matriz de Vistas y Funcionalidades por Rol (y Cuentas Semilla Demo)

Todas las cuentas semilla están en [`database/seed/01_dev_users.sql`](../../database/seed/01_dev_users.sql), [`04_dev_students.sql`](../../database/seed/04_dev_students.sql) y [`05_dev_tramites.sql`](../../database/seed/05_dev_tramites.sql).
**Contraseña universal:** `TecNM-Demo-2026!`

### A. Roles de Personal / Vinculación

| Rol | Usuario Demo | Vista Inicial (`/`) | Menús y Funcionalidades Exclusivas |
| :--- | :--- | :--- | :--- |
| **Encargado de Servicio Social** (`social_service_officer`) | `encargado.ss` | Dashboard operativo de Servicio Social | • **Padrón de Alumnos:** Carga masiva por Excel (`.xlsx`) y botón **"+ Nuevo Alumno"** para casos específicos.<br>• **Gestión de Servicio Social:** Dictaminar solicitudes Anexo XVIII, generar automáticamente **Carta de Presentación (`SS_CP...`)**, activar trámites, validar terminación y emitir **Constancia de Liberación (`SSC...`)** y **Acta a Escolares (`DGTV...`)**.<br>• **Revisión Documental:** Bandeja de validación de PDFs (`SOL, CA, CC, R1..R3, EV1..EV3, CT`) con visor embebido (Aprobar / Observar con motivo).<br>• **Seguimiento y Estadísticos SS:** Sábana `SEGUIMIENTO VINCULACIÓN1.xlsx` en vivo y PNC. |
| **Coordinador de Prácticas** (`internship_coordinator`) | `coord.practicas` | Dashboard operativo de Prácticas | • **Padrón de Alumnos y Empresas/Convenios:** Consulta de elegibilidad, directorio de empresas receptoras y semáforo de vigencia de convenios.<br>• **Gestión de Prácticas:** Aprobar solicitudes del formulario inicial y emitir **Carta de Presentación (`EJ26/PR...`)**, registrar/validar **Oficio de Aceptación membretado (`CA`)** recibido en `arturo.martinez@monclova.tecnm.mx`, validar `EV` y `CT`, y emitir **Constancia de Acreditación (`PPC...`)**.<br>• **Revisión Documental y Estadísticos PP:** Bandeja de revisión y matriz de seguimiento de Prácticas. |
| **Jefa de Vinculación** (`department_head`) | `jefa.vinculacion` | Tablero Ejecutivo de Vinculación | • **Dashboard General en Vivo:** KPIs reales de ambos trámites, revisiones pendientes, convenios por vencer e incidencias ISO 9001.<br>• **Matriz de Seguimiento (`SEGUIMIENTO VINCULACIÓN1.xlsx`):** Sábana completa con checklist `SI/NO` (`CP, CA, SOL, CC, R1..R3, EV, CT, COT`) y barra de progreso 0–100% exportable a Excel.<br>• **Estadístico Semestral y Formato 911.10:** Conteos automáticos por carrera, género y sector (*En condiciones*, *Solicitantes*, *Liberados*, *PNC*).<br>• **Calidad (PNC):** Supervisión de Producto No Conforme y bitácora de auditoría. |
| **Administrador** (`admin`) | `admin` | Tablero General + Administración | • Acceso completo a todos los módulos operativos anteriores.<br>• **Catálogos Oficiales:** Carreras, Periodos Académicos, Programas de Servicio Social (internos/externos), Autoridades Firmantes (para plantillas dinámicas) y Series de Folios. |

### B. Cuentas de Estudiantes (`student`) por Etapa para Demostración

El rol `student` **solo ve su Portal de Estudiante** (`Mi Perfil & Vigencia IMSS`, `Mi Servicio Social`, `Mis Prácticas Profesionales`):

| Usuario Demo | No. Control / Carrera | Escenario Pre-cargado para Demostración |
| :--- | :--- | :--- |
| `usuario` | `I23050363` (Ing. Informática) | **1. Alumno Elegible Nuevo:** Sin trámite iniciado. Permite demostrar en vivo el asistente de apertura de Servicio Social (o solicitud de Carta de Presentación de Prácticas) subiendo su Vigencia IMSS. |
| `alumno.ss.local` | `I22050114` (Ing. Industrial) | **2. Servicio Social LOCAL en el TecNM (`approved`):** Ya tiene Carta de Presentación (`SS_CP0001/15/02/2026`). Permite demostrar la **generación automática de la Carta de Aceptación Local**, llenado e impresión de **Carta Compromiso (Anexo XX)** y subida de documentos firmados/sellados. |
| `alumno.ss.activo` | `I23050412` (Ing. Gestión Emp.) | **3. Servicio Social EXTERNO Activo (`active`, 60% avance):** Con `SOL, CP, CA, CC, R1, EV1` aprobados y `R2/EV2` en revisión. Permite demostrar el prellenado e impresión de **Reportes Bimestrales (Anexo XXII)**, **Evaluaciones Cualitativas (Anexos XXIII, XXIV y XXV)** y subida de **Carta de Terminación (`CT`)**. |
| `alumno.pp.solicitud` | `I22050289` (Ing. Electromecánica) | **4. Prácticas con Carta de Presentación Emitida (`approved`):** Con folio `EJ26/PR001`. Permite demostrar la descarga de su `CP` y el registro del **Oficio de Aceptación en hoja membretada (`CA`)** tras respuesta de la empresa a `arturo.martinez@monclova.tecnm.mx`. |
| `alumno.pp.activo` | `I22050501` (Ing. Electrónica) | **5. Prácticas Activas en Empresa (`active`, 40% avance):** En *Gunderson Gimsa*, con `EV` en revisión y `CT` observada por falta de sello. Permite demostrar el formulario de **Evaluación Anexo XVII (Productividad Laboral)** y la resubida de documentos corregidos. |
| `alumno.liberado` | `I23050618` (Ing. Energías Ren.) | **6. Trámite 100% Liberado (`released`):** Con todos sus documentos aprobados, Constancia de Terminación `SSC0001/10/08/2026` descargable y Acta `DGTV001/12-08-2026`. |

---

## 🛠️ 3. Plan de Ejecución por Fases (Frontend)

### Fase 1: Navegación por Rol, Guardias de Ruta y Alineación de APIs
- **1.1. Estado de Sesión y Menú Dinámico por Rol (`MainLayout.vue` / `mainLayout.config.ts`):**
  - Filtrar los ítems del sidebar según `user.roles`:
    - Si es `student`: mostrar únicamente `Mi Expediente & IMSS`, `Mi Servicio Social` y `Mis Prácticas Profesionales`, y redirigir `/` automáticamente a `/social-service/my` (o resumen de portal estudiantil).
    - Si es `social_service_officer`, `internship_coordinator`, `department_head` o `admin`: mostrar los módulos administrativos correspondientes a su rol y conectar `/` al Dashboard real con datos en vivo de `/api/reports/dashboard`.
- **1.2. Corrección de Clientes Axios (`src/modules/*/api/`):**
  - Alinear métodos HTTP, rutas y tipos TypeScript de `studentsApi.ts`, `socialServiceApi.ts`, `internshipsApi.ts`, `expedientsApi.ts`, `reportsApi.ts` y `catalogsApi.ts` contra los controladores .NET 10.

### Fase 2: Padrón de Alumnos (Carga Excel + Alta Individual) y Perfil IMSS
- **2.1. Vista Administrativa de Padrón (`/students` — `StudentsListView.vue`):**
  - Modal **"+ Agregar Alumno (Caso Específico)"** (`POST /api/students`) para registrar manualmente un alumno elegible (No. Control, Nombre, Carrera, Semestre, Créditos, Turno, Correo).
  - Modal **"Importar Padrón desde Excel (.xlsx)"** (`GET /api/student-imports/template` + `POST /api/student-imports`) con selector de periodo y resumen de filas importadas/actualizadas.
  - Filtros por carrera, turno, vigencia IMSS y buscador por matrícula/nombre.
- **2.2. Vista de Perfil del Estudiante (`/students/me` — `StudentProfileView.vue`):**
  - Actualización de teléfono y correo (`PUT /api/students/me/contact`).
  - Subida y visualización en PDF de la **Constancia de Vigencia de Derechos del IMSS** (`PUT /api/students/me/imss` y `GET /api/students/me/imss`).

### Fase 3: Flujo Completo de Servicio Social (Estudiante + Vinculación + Plantillas Imprimibles)
- **3.1. Portal del Alumno (`/social-service/my` — `StudentSocialServiceView.vue`):**
  - **Paso 1 (Solicitud de Apertura + IMSS):** Si no tiene trámite, mostrar el asistente automatizado donde sube su Constancia de Vigencia IMSS y llena los datos del **Anexo XVIII** (programa, dependencia interna o externa, responsable, domicilio, teléfono y actividades). Al enviar (`POST /api/social-services`), genera el formato Anexo XVIII listo para imprimir y lo envía a Vinculación.
  - **Paso 2 (Carta de Presentación `CP`):** Cuando Vinculación aprueba la solicitud, habilitar botón **"Descargar Carta de Presentación (`SS_CP...`)"** generada automáticamente con plantilla oficial (`/print-data/presentation_letter`).
  - **Paso 3 (Carta de Aceptación `CA` — Automatizada si es Local en el TecNM):**
    - Si `modality === 'internal'` (`isTecnmInternal`), mostrar botón **"Generar Carta de Aceptación Local TecNM"** (`/print-data/acceptance_letter`) para que el alumno descargue el oficio prellenado, recabe firma/sello del departamento interno y suba el PDF escaneado (`CA`).
    - Si es externo, subir directamente la Carta de Aceptación emitida por la dependencia (`CA`).
  - **Paso 4 (Carta Compromiso `CC` — Anexo XX):** Modal para confirmar domicilio/teléfono (`PUT /commitment`), botón **"Imprimir Carta Compromiso Llena"** (`/print-data/commitment`) y botón para subir el PDF firmado (`CC`).
  - **Paso 5 (Reportes Bimestrales `R1, R2, R3` y Evaluaciones Cualitativas `EV1, EV2, EV3`):**
    - Formularios para llenar los datos de **Anexo XXII (Reportes 1, 2 y 3)**, **Anexo XXIII (Evaluación Supervisor)**, **Anexo XXIV (Autoevaluación)** y **Anexo XXV (Evaluación de Actividades)** con un clic en **"Generar Formato Lleno para Firma y Sello"** y subida del PDF firmado y sellado (`R1..R3`, `EV1..EV3`).
  - **Paso 6 (Carta de Terminación `CT` y Constancia de Liberación `COT`):** Subida de `CT` firmada y sellada; el progreso avanza 10% por cada documento validado sin bloquear por horas ni calificaciones.
- **3.2. Portal de Vinculación (`/social-service` — `SocialServiceListView.vue`):**
  - Tabla con filtros por estatus, carrera y modalidad + cajón/modal de **Detalle de Trámite**:
    - Dictaminar solicitud Anexo XVIII (`POST /application/review`) → emite automáticamente folio `SS_CP...`.
    - Revisar documentos del expediente (`SOL, CA, CC, R1..R3, EV1..EV3, CT`) con visor PDF embebido.
    - Emitir **Constancia de Terminación (`POST /release` → folio `SSC...`)** y **Acta a Servicios Escolares (`POST /school-services-act` → folio `DGTV...`)**.

### Fase 4: Flujo Completo de Prácticas Profesionales (Forms Automatizado + Vinculación)
- **4.1. Portal del Alumno (`/internships/my` — `StudentInternshipsView.vue`):**
  - **Paso 1 (Solicitud de Carta de Presentación — Automatización del Forms):** Formulario con los campos exactos del Microsoft Forms oficial (*Matrícula, Nombre Completo, Carrera, Semestre 1–15, Turno Matutino/Nocturno, Teléfono de contacto, Correo INSTITUCIONAL* + adjuntar **Constancia de Vigencia de Derechos del IMSS** en PDF).
  - **Paso 2 (Descarga de Carta de Presentación `CP` y Aviso de Oficio de Aceptación `CA`):**
    - Botón para descargar la **Carta de Presentación (`Oficio No. EJ26/PR...`)** una vez aprobada por Vinculación.
    - Aviso destacado: *"La empresa debe enviar el Oficio de Aceptación en hoja membretada con firma y sello al correo `arturo.martinez@monclova.tecnm.mx`"*, acompañado del formulario para registrar la empresa/supervisor/fechas (`POST /acceptance`) y subir el PDF del Oficio de Aceptación (`CA`).
  - **Paso 3 (Prellenado de Evaluación Anexo XVII y Subida de `EV` y `CT`):**
    - Formulario interactivo del **Anexo XVII (Productividad Laboral — 7 criterios de 0 a 4)** para imprimirlo ya lleno (`/print-data/evaluation`), recabar firma y sello de la empresa, y subir los PDFs escaneados de Evaluación (`EV`) y Oficio de Terminación membretado (`CT`).
- **4.2. Portal de Coordinación de Prácticas (`/internships` — `InternshipsListView.vue`):**
  - Aprobar solicitudes del Forms (`POST /approve` → genera folio `EJ26/PR...`), validar `CA`, `EV` y `CT`, y emitir Constancia de Liberación (`POST /release` → folio `PPC...`).

### Fase 5: Bandeja de Revisión Documental y Estadísticos en Vivo (`SEGUIMIENTO VINCULACIÓN1.xlsx`)
- **5.1. Bandeja de Revisión Documental (`/expedients/review` — `ExpedientsReviewView.vue`):**
  - Conectada 100% a `/api/documents/review-queue` y `POST /api/documents/{id}/review` (sin datos falsos), permitiendo aprobar o rechazar con motivo obligatorio.
- **5.2. Sábana de Seguimiento en Vivo (`/reports/tracking` — `TrackingSheetView.vue`):**
  - Tabla réplica de `SEGUIMIENTO VINCULACIÓN1.xlsx` mostrando en tiempo real las columnas `CP, CA, SOL, CC, R1, R2, R3, EV1, EV2, EV3, CT` (con insignias `SI` / `PEND`), barra de progreso `0%–100%`, folios emitidos y exportación a Excel.
- **5.3. Estadístico Semestral y Formato 911.10 (`/reports/stats` — `StatisticsView.vue`):**
  - Tablas dinámicas por carrera, sexo y sector (*En condiciones con CP*, *Solicitantes con CA*, *Liberados*, *PNC* y desglose 911.10 XII y XIII) que se actualizan automáticamente conforme los alumnos suben documentos y Vinculación los aprueba.
