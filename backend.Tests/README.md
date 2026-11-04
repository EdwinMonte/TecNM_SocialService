# Comprobaciones documentales

Desde la raíz, ejecutar `pwsh -File backend.Tests/run-isolated.ps1`. Requiere Docker y la imagen `postgres:18-alpine`; prepara esquema v1, seeds demo y migración en una base nueva `document_checks`. El contenedor se elimina en `finally`. Nunca toma la conexión de la base del proyecto.

`BaselineRevision` fija el commit con esquema anterior a v2. Actualizarlo únicamente si cambia deliberadamente la base de comparación de migraciones.

La suite prueba políticas, PDFs, almacenamiento, PostgreSQL y endpoints HTTP reales mediante TestServer: cookies, propiedad de recursos, roles, IMSS y descarga de plantillas. Para el bloque PDF, proporcionar fuentes autorizadas mediante `DOCUMENT_TEST_FONTS`; en Windows busca `C:/Windows/Fonts` solo para estas comprobaciones.

`dotnet run --project backend.Tests/TecNM.DocumentChecks.csproj` sin `DOCUMENT_TEST_CONNECTION` ejecuta los controles locales y omite PostgreSQL/HTTP de forma explícita. No sustituye la suite aislada.

Verificados reemplazos HTTP concurrentes y conservación del archivo/referencia anterior ante un fallo de almacenamiento simulado. También pasan aperturas/aprobaciones concurrentes sin duplicar trámite ni consumir folios extra, y rollback con eliminación del archivo nuevo ante fallo de BD. Carta SS usa temporalmente PDF firmado cargado por el alumno: aprobación de solicitud sin plantilla ni folio, rechazo/corrección/aprobación de CP y conservación de bytes. Emisión institucional directa sigue bloqueada sin base limpia.

## Recorrido de interfaz

Después de la suite aislada, iniciar Vite con `pnpm --prefix frontend run dev`. Ejecutar `node frontend/scripts/check-expedient-ui.mjs` desde la raíz. Requiere Playwright y Edge; `PLAYWRIGHT_MODULE` puede apuntar al módulo del runtime local de Codex. Usa respuestas demo exportadas por la API aislada en tmp/pdf-qa/ui-fixtures, sin consultar ni modificar la base del proyecto. Comprueba SS en escritorio/móvil, visor, descarga visible, Escape, desbordamiento y acciones terminales. El visor PDF nativo puede no renderizar en headless; contenido de PDFs se verifica con la suite de renderizado separada.

El mismo script comprueba PP: editor de dos fechas sin criterios, aceptación sin carga para alumno y carga multipart CA para staff. `node frontend/scripts/check-review-ui.mjs` verifica bandeja administrativa con estados sintéticos: fecha IMSS, rechazo obligatorio, payloads, filtros enviados al servidor, limpieza de blob, error sin ejemplos y reintento. Ambos guardan capturas en tmp/pdf-qa. Estas pruebas de interfaz interceptan API; la integración HTTP/PostgreSQL se ejecuta por separado en la suite aislada.

Perfil IMSS: el recorrido comprueba que falta de fecha deshabilita carga, que multipart incluye documentDate y que se muestra revisión pendiente. La suite HTTP verifica fecha declarada persistida, fechas futuras rechazadas y que reenviar mismos bytes no cambia fecha confirmada ni aprobación.

Ciclo SS por HTTP: Compromiso solo acredita firma tras aprobar evidencia; reporte no admite dictamen sin PDF. Rechazo, reemplazo y aprobación sincronizan datos y PDF, bloquean edición posterior y conservan versiones/observaciones históricas.

Permisos: login real de alumno, admin, encargado SS, coordinador PP y jefatura. Eliminación respeta dominio/catálogo y bloqueo terminal. Reloj controlado prueba cambio de año a medianoche de México: nuevas aperturas SS/PP bloqueadas para alumno/staff, formatos de trámites admitidos disponibles, referencia histórica conservada y prellenado PP sin calificaciones. Guardado/descarga PP verifica propiedad (éxito propio, 404 ajeno).

Geometría de plantillas: comprobar páginas y dimensiones del manifiesto, campos fuera de hoja, página inválida, fuentes con tamaño inválido y JSON malformado. HTTP verifica que el endpoint institucional directo no emite sin base limpia. `tools/pdf/verify_templates.py tmp/pdf-qa/pp-evaluation.pdf` contrasta dimensiones y renderizados; ejecutar tras generar muestras.

ClosingChecks completa SS/PP por HTTP después del cambio de año IMSS. Comprueba siete anexos SS, progreso 80/90/100, CA/EV/CT PP, liberación concurrente con un solo folio y bloqueos terminales. Seguimiento y detalle deben coincidir; fechas históricas/paquetes v1 conservan su validez sin acreditar documentos v2.

Entorno local migrado: `frontend/scripts/check-live-documents.mjs` verifica login real alumno/admin, bandeja y generación SS/PP sin mocks ni modificación de evidencias. Guía de acceso y reinicio en docs/LOCAL_DOCUMENT_TESTING.md. Pruebas de interfaz con fixtures cubren carga CP firmada visible y ausencia de prellenado CP temporal.
