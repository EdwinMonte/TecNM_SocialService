# Prueba documental local

Acceso: http://localhost:3000. Frontend, API y PostgreSQL se ejecutan en Docker.

Cuentas demo verificadas: `usuario` (alumno) y `admin` (administración). Contraseña demo: `TecNM-Demo-2026!`.

## Entorno preparado

- PostgreSQL Docker, base `tecnm_sspp`, puerto local **15432**; volumen existente conservado.
- Migración incremental documental v2 aplicada. Expedientes existentes permanecen en flujo 1; nuevos usan flujo 2.
- Respaldo previo: `tmp/pdf-qa/backups/tecnm-before-documents-v2-20261104.dump`.
- Cinco archivos conservados en `tecnm_storage_data`, incluidos los cargados durante las pruebas nativas. Copia y hashes verificados; `local-data/documents` conserva la copia previa al cambio.
- API Docker expuesta en http://127.0.0.1:5055; frontend Nginx en http://localhost:3000. La conexión interna de API usa `db:5432`. Configuración en `.env` ignorado por Git.
- Fuentes locales de Windows montadas en solo lectura para pruebas; no se copian al repositorio ni a imágenes. Runtime Linux incluye ICU completo y zona horaria de México.
- Descargas desde mcr.microsoft.com y reconstrucción verificadas. El fallo DNS anterior ya no se reprodujo; no se modificó la configuración global de red. API nativa y Vite detenidos.
- Respaldo adicional antes del cambio: `tmp/pdf-qa/backups/tecnm-before-docker-switch.dump`. No se recrearon tablas ni eliminaron volúmenes.

## Recorrido

Verificación real de RBAC, empresas y métricas: `node frontend/scripts/check-live-process-access.mjs`. Inicia sesión con los cinco roles demo y comprueba estadísticas de ambas áreas, valores iniciales por rol, catálogo tipificado y bloqueo de expedientes cruzados. No carga archivos ni modifica empresas o dictámenes. `PLAYWRIGHT_MODULE` puede apuntar al runtime local; `UI_TEST_URL` y `UI_TEST_PASSWORD` permiten ajustar el entorno. `LINKAGE_SQL_SNAPSHOT` permite indicar un snapshot SQL reciente para comparar las métricas HTTP con los agregados de la base; no usa automáticamente capturas anteriores.

1. Entrar como alumno y abrir Mi perfil. Cargar constancia IMSS PDF real con fecha de expedición.
2. Entrar como admin y revisar IMSS: consultar PDF, confirmar fecha del año actual y aprobar, o rechazar con motivo. Los datos demo anteriores no acreditan vigencia para nuevas solicitudes.
3. Alumno: crear SS o PP, guardar datos y descargar prellenados; firmar físicamente y subir el PDF.
4. SS: cargar Solicitud firmada; staff aprueba su PDF y después la solicitud. Esta aprobación no genera Carta de Presentación ni folio.
5. SS: alumno carga Carta de Presentación ya firmada en el expediente. Staff revisa y aprueba/rechaza. Corregir rechazo mediante nueva carga. No se altera ni estampa el archivo recibido.
6. Completar anexos del expediente. SS nuevo exige las siete evaluaciones separadas. En PP, aceptación se carga únicamente desde administración; alumno carga evaluación y terminación.
7. Staff: concluir y liberar cuando todos los documentos requeridos estén aprobados.

La plantilla limpia de Carta SS queda pendiente para habilitar emisión institucional futura; no bloquea el flujo temporal de carga firmada. La revisión institucional de los formatos sigue siendo necesaria antes de publicar en producción.

## Reinicio

Desde la raíz: `docker compose up -d --build`. Para iniciar las imágenes ya construidas sin descargar: `docker compose up -d --no-build --pull never`.

Estado: `docker compose ps`. Detener conservando datos: `docker compose stop`.

No ejecutar nuevamente la migración ya aplicada ni el archivo de diseño sobre esta base. Prueba HTTP aislada reproducible: `pwsh -File backend.Tests/run-isolated.ps1`. Comprobación del entorno Docker activo sin modificar evidencias: `node frontend/scripts/check-live-documents.mjs` (requiere Playwright y Edge; `UI_TEST_URL` permite otro origen). Verificados login real de alumno/admin, bandeja, PDFs SS/PP y mes en español del Compromiso.

## RBAC y empresas desplegados — 6 de noviembre de 2026

- Aplicadas `20261106_company_type_prepare.sql` y `20261106_company_type_finalize.sql` en una sola transacción, con API y frontend detenidos. No se ejecutaron schema de diseño ni seeds sobre esta instalación.
- Clasificación individual: TecNM interno `public`, conservando `sector=educational`; Presidencia Municipal `public`; Gunderson `large`; SIMSA `medium`. No quedan empresas pendientes y `company_type` es `NOT NULL`.
- Respaldo verificado: `tmp/pdf-qa/backups/tecnm-before-rbac-20261107T031301Z.dump` (nombre en UTC; operación realizada la noche del 6 de noviembre en México).
- Huellas anteriores y posteriores verificadas para 11 alumnos, 6 expedientes SS, 4 expedientes PP, 47 documentos, campos previos de empresas y archivos del volumen. Evidencias y dictámenes conservados.
- Imágenes backend/frontend reconstruidas y servicios actualizados activos en `http://localhost:3000`, API en puerto 5055 y PostgreSQL en 15432. Volúmenes existentes conservados.
- Pasan ambos recorridos reales: acceso con cinco roles, tipología, área inicial, alternancia estadística, bloqueo de expedientes cruzados, comparación de métricas SQL/HTTP y descargas documentales. Capturas en `tmp/pdf-qa/live-process-access/`.

Estas migraciones ya están aplicadas en esta base local. No volver a ejecutarlas; para otras instalaciones seguir `database/migrations/README.md` y clasificar sus registros individualmente.
