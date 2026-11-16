# Especificación Frontend: Catálogo de Carreras

## Propósito

Vista administrativa para la gestión integral del catálogo de carreras académicas del TecNM Campus Monclova, consumiendo los endpoints de `/api/careers`.

## Contrato Backend de Referencia

Basado en `docs/specs/catalogs.spec.md`:
- `GET /api/careers?page=1&pageSize=20&search=&isActive=&sort=code`: Lista paginada `PagedResponse<CareerResponse>`.
- `POST /api/careers`: Alta con `CareerRequest` (`code`, `name`, `totalCredits`).
- `PUT /api/careers/{id}`: Edición con `CareerRequest`.
- `PATCH /api/careers/{id}/status`: Toggle activo/inactivo con `UpdateStatusRequest` (`isActive`).
- `DELETE /api/careers/{id}`: Baja lógica con verificación de integridad.

## Requerimientos de UI y Accesibilidad

1. **Estructura base:**
   - Contenedor raíz `UiBasePage` con `fluid=true` y layout `fullscreen-page` (`100dvh` sin scroll exterior, con scroll interno en tabla y encabezados fijos).
   - Encabezado ejecutivo con botón "Nueva Carrera" que abre `UiDialog` y 4 KPIs compactos neutros donde únicamente el KPI activo resalta con borde dorado (`var(--color-accent)`), cálculo de 70% de créditos para Servicio Social y respaldo local cuando la API no está en ejecución.

2. **Filtros y Búsqueda:**
   - Campo de búsqueda con autocompletado interactivo y debounce de `280 ms` por clave (`code`) o nombre (`name`), con botón único azul (`✕`) para limpiar.
   - Filtro de estado: Todas / Activas / Inactivas.

3. **Tabla de Datos (`UiTable`):**
   - Columnas:
     - `code`: Clave (badge o texto mono).
     - `name`: Nombre oficial de la carrera.
     - `totalCredits`: Créditos totales de la retícula (ej. 260).
     - `isActive`: Estado con `UiBadge` (`success` para Activa, `danger` para Inactiva).
     - `actions`: Botones compactos para Editar, Cambiar Estado y Baja, centrados bajo el encabezado.
   - Paginación estandarizada de `10` registros por página (`pageSize = 10`) con barra fija inferior (`Mostrando X de Y carreras · Página P de T`, `Anterior`, `Siguiente`).

4. **Modal de Creación / Edición (`UiDialog`):**
   - Campos (`UiField`):
     - `code`: Clave (mayúsculas, 1-20 caracteres, regex `^[A-Z0-9_-]+$`). En edición puede ser de solo lectura o editable según regla de negocio.
     - `name`: Nombre oficial (1-150 caracteres).
     - `totalCredits`: Créditos (1-500, predeterminado 260).
   - Acciones de modal: Cancelar (`UiButton` secondary) y Guardar (`UiButton` primary).
   - Manejo de errores RFC 7807 (código duplicado muestra mensaje descriptivo).

5. **Modal de Confirmación de Eliminación (`UiDialog`):**
   - Advertencia clara si la carrera tiene estudiantes vinculados (error 409).
   - Acciones: Cancelar y Confirmar eliminación (`UiButton` danger).

6. **Feedback al usuario:**
   - Avisos mediante `UiToast` en operaciones exitosas (creación, actualización, cambio de estado, eliminación).
   - Estado de carga y mensajes de error con `UiAlert`.
