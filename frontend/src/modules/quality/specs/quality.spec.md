> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

# Especificación Frontend: Módulo de Calidad ISO 9001 & PNC

> **Contrato Backend:** [`docs/specs/quality.spec.md`](../../../../../docs/specs/quality.spec.md)  
> **Roles:** `admin`, `social_service_officer`, `internship_coordinator` (gestión completa), `department_head` (lectura).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/quality/pnc` | `quality-pnc` | `NonConformancesView.vue` | Staff, Dept Head | Bitácora, alta y seguimiento formal de Producto No Conforme (PNC). |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Vista `/quality/pnc`
- **Contenedor:** `UiBasePage` (`fluid=true`, layout `fullscreen-page` `100dvh` sin scroll exterior, encabezado ejecutivo "Producto No Conforme (PNC - ISO 9001)" y notificaciones `UiToast`).
- **Barra de KPIs Superiores (4 tarjetas interactivas con borde dorado activo):**
  - Total PNC (`all`).
  - Abiertos (`open`).
  - En Revisión (`under_review`).
  - Resueltos (`resolved`).
- **Barra de Filtros:**
  - Buscador con autocompletado interactivo y debounce de `280 ms` por código de incidencia (`PNC-SS-...`), matrícula o nombre del alumno, con botón único azul (`✕`) para limpiar.
  - Selector de Periodo Académico.
  - Selector de Trámite: Todos / Servicio Social / Prácticas Profesionales.
  - Selector de Estado del PNC.
  - Selector de Categoría (`desertion`, `delayed_reports`, `company_dismissal`, `insufficient_grade`, `invalid_documents`, `other`).
- **Tabla de Incidencias (scroll interno y encabezados fijos):**
  - Paginación estandarizada de `10` registros por página (`pageSize = 10`) con controles `Anterior`, `Página X de Y` y `Siguiente`.
  - Columnas: Código de Incidencia, Periodo, Trámite, Alumno (nombre y matrícula), Categoría (etiqueta legible), Estado (`UiBadge`), Fecha de Registro, Acciones (centrada).
  - Botón "Registrar PNC" en encabezado (para `admin`, `social_service_officer` e `internship_coordinator`) que abre modal de alta.
  - Acción de fila: Botón "Gestionar" / "Ver detalle" -> abre modal de seguimiento con plan de acción y transiciones según rol y proceso.

---

### 2.2 Modal / Vista de Seguimiento de Incidencia
- **Información del Trámite Asociado:**
  - Enlace directo al expediente de Servicio Social o Prácticas Profesionales.
  - Nombre del alumno, carrera, empresa y supervisor.
- **Formulario de Plan de Acción Correctiva:**
  - Si el estado es `open` o `under_review`, permite capturar o editar el `actionPlan` (análisis de causa raíz y compromisos acordados con el alumno o empresa).
- **Botones de Transición de Estado (`UiButton`):**
  - "Pasar a Revisión" (`under_review`).
  - "Resolver Incidencia" (`resolved`): Exige que el `actionPlan` esté documentado. Muestra badge de éxito y registra el usuario auditor que resolvió la incidencia.
  - "Cancelar Incidencia" (`cancelled`).

---

### 2.3 Componente Reutilizable `TramiteTimelineModal.vue`
- Modal que consume `GET /api/tramite-status-logs?tramiteType=...&tramiteId=...`.
- Renderiza una línea de tiempo vertical visual:
  - Fecha y hora legible (zona horaria `America/Monterrey`).
  - Estado anterior -> Nuevo estado (`UiBadge`).
  - Motivo u observaciones de la transición.
  - Nombre del usuario que autorizó el cambio de estado.

