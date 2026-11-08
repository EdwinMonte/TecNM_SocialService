# Base de Datos (`database/`)

Tipología de empresas y métricas: seguir la [guía de migración incremental](./migrations/README.md). La API nueva requiere preparación, clasificación explícita de pendientes y finalización. Los seeds actualizados son solo para bases de desarrollo nuevas o aisladas.

Este directorio centraliza los recursos y especificaciones de base de datos del proyecto **TecNM SSPP**, organizados según su rol dentro del ciclo de vida del desarrollo.

| Subcarpeta | Estado | Propósito | Reglas de Uso |
| :--- | :--- | :--- | :--- |
| [`design/`](./design/) | **Borrador en curso** | Nuevo diseño relacional para PostgreSQL enfocado en los módulos del TecNM SSPP. | Espacio de trabajo activo para documentar tablas, relaciones y scripts de esquema en evolución. |
| [`seed/`](./seed/) | **Desarrollo local** | Datos iniciales y semillas (`01_dev_users.sql`, `02_dev_catalogs.sql`, `03_dev_companies.sql`, `04_dev_students.sql`) para pruebas de desarrollo. | Solo para entornos locales de prueba. **No ejecutar ni cargar en producción.** |
| [`legacy/`](./legacy/) | **Solo referencia** | Scripts y esquemas históricos del sistema anterior SET_MX (SQL Server). | **NO EJECUTAR.** Únicamente fuente de consulta técnica y análisis de entidades preexistentes. |

---

> [!WARNING]
> La carpeta `database/legacy/` contiene scripts y esquemas del sistema legacy bajo SQL Server. No deben ejecutarse contra el motor PostgreSQL del proyecto ni intentar migrarse 1:1 sin rediseño previo.

Escenarios opcionales de seguimiento SS: ejecutar seed/scenarios/01_social_service_follow_up.sql después de la migración 20261108. Reutiliza los once alumnos demo, activa AD26, añade FJ25 y distribuye avisos, PNC, progreso constante/poco avance/pendiente, liberado y periodo de aptitud pendiente. Repetible; solo para datos de prueba autorizados.

Verificación contra estos escenarios locales: node frontend/scripts/check-live-follow-up.mjs (PLAYWRIGHT_MODULE configurado). Usa API real, verifica los siete estatus, filtros, valoración y permisos SS/PP; capturas en tmp/pdf-qa/live-follow-up.
