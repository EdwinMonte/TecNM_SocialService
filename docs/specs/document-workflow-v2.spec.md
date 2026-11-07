> Contrato de autorización vigente: [Acceso por proceso](./process-access.spec.md). Prevalece sobre permisos anteriores incompatibles.

# Expediente documental v2

Contrato vigente para documentos, alumnos, SS, PP y estadísticos. Las reglas v1 de los contratos anteriores solo aplican donde aquí se indica compatibilidad histórica.

## Ajuste temporal autorizado: Carta de Presentación SS

Mientras se entrega la plantilla limpia, CP SS se incorpora como PDF firmado cargado por el alumno (o staff SS/admin), pendiente de dictamen. Se permiten rechazo, corrección y aprobación con el historial documental habitual. No se estampa ni altera el archivo recibido. Su aprobación cuenta para progreso y liberación.

Aprobar la solicitud exige SOL aprobado e IMSS vigente, pero no emite CP ni asigna folio institucional. La apertura aprobada no afirma que la carta ya fue emitida. La descarga CP devuelve únicamente evidencia aprobada existente. El endpoint institucional de emisión permanece protegido por plantilla limpia y no se usa en el flujo temporal. CP PP conserva sus reglas actuales. Este ajuste prevalece sobre las referencias a emisión automática y prohibición de carga CP SS de las secciones anteriores.

## Flujo y permisos

- Solicitudes nuevas: `documentWorkflowVersion=2`; expedientes existentes: `1`. No convertir paquetes históricos ni inferir dictámenes individuales.
- SS v2: `SOL`, `CP`, `CA`, `CC`, `R1..R3`, `EV_SUP1..EV_SUP3`, `EV_SELF1..EV_SELF3`, `EV_ACT`, `CT`. Bimestre 3 es final.
- SS v1 conserva `EV1..EV3`. Evaluaciones XXIII/XXIV solo descargan plantilla vacía. XXV solo prellena nombre, programa, periodo y marca final.
- `CA` SS nunca se autogenera, tampoco para modalidad interna. Descargar ejemplo; cargar carta de la dependencia.
- PP: `CA` solo lo carga/revisa `admin` o `internship_coordinator`; alumno solo consulta. UI informa envío empresarial a `arturo.martinez@monclova.tecnm.mx`.
- Alumno no carga documentos institucionales (`CP`, `COT`, liberación PP `CC`). Staff solo modifica su dominio; `department_head` solo consulta.
- Eliminar evidencia pendiente exige los mismos permisos del catálogo que cargarla. Alumno no elimina aceptación PP incorporada por staff; trámites terminales no admiten eliminación. Documentos aprobados/rechazados conservan su protección e historial.
- PDF válido, no cifrado, con páginas, máximo 5 MiB. Rechazo exige motivo de 1–1000 caracteres. Reemplazos conservan archivos históricos; aprobado no se reemplaza excepto IMSS.
- Nueva carga idéntica al documento vigente es idempotente. Una revisión repetida idéntica devuelve el dictamen; un dictamen distinto devuelve 409.
- Guardar datos no acredita firma, evaluación ni entrega. Revisión del reporte delega al documento PDF. `CC` aprobado acredita firma; `signed_at` indica validación documental.
- SS v2 requiere `SOL` aprobado para aprobar apertura. Conclusión/liberación requiere todos los documentos exigibles aprobados, incluida CP emitida. SS v1 conserva sus requisitos históricos.
- Conclusión SS v2 sigue bloqueada con seis evaluaciones aprobadas si falta EV_ACT, o si este último PDF está pendiente. El bloque estadístico solo se completa al aprobar los siete. PP exige CA, EV y CT aprobados. Liberación concurrente debe consumir un solo folio; tras liberar se conservan descargas y se bloquean nuevas evidencias.

## IMSS único

- Fuente única: `expedient_documents`, tipo `general`, código `IMSS`, `tramiteId=null`.
- Carga multipart agrega `documentDate` (fecha declarada); puede omitirse para cargar un documento histórico pendiente. Aprobar exige confirmar `documentDate` del año actual y no futura.
- El año se calcula con `TimeProvider` en `America/Mexico_City`. No usar año de carga ni booleano del perfil como autoridad.
- Crear y aprobar solicitudes SS/PP exige documento IMSS vigente aprobado, incluso para staff. Nuevas solicitudes guardan FK `admission_imss_document_id`.
- Trámites ya admitidos continúan al cambiar el año. Una carga nueva reinicia revisión; archivo y dictamen previo se conservan como versión sustituida.
- Endpoints IMSS actuales delegan al servicio documental. Validación recibe `{ isValid, rejectionReason?, documentDate? }`; rechazar sin motivo o aprobar sin fecha confirmada devuelve 400.
- Perfil expone estado, fecha documental, observaciones e identificador IMSS. `hasValidImss` es una proyección del documento aprobado vigente del año, no autorización persistente independiente.

## PDF oficial

- `GET /api/social-services/{id}/pdf/{document}`: `application`, `commitment`, `report_1..3`, `activity_assessment`, `presentation_letter`.
- `GET /api/internships/{id}/pdf/evaluation`: solo datos del estudiante y fechas; ambas páginas; criterios, resultados, observaciones y firmas vacíos.
- `PUT /api/internships/{id}/evaluation-format`: `{ evaluationStartDate, evaluationEndDate }`; no crea calificaciones. Captura calificada existente queda exclusiva para staff, con siete valores explícitos.
- El alumno propietario puede guardar fechas del formato PP incluso cuando IMSS deje de habilitar nuevas solicitudes; otro alumno recibe 404. La evidencia aprobada y los estados terminales bloquean edición.
- `GET /api/document-templates/{key}/file`: catálogo autenticado de plantillas sin datos personales. Claves permitidas, nunca rutas arbitrarias.
- PDFsharp Core 6.2.4. Superposición en puntos sobre copias oficiales verificadas, sin recrear membretes ni rasterizar páginas vectoriales. Entrega estática.
- Solicitud: retirar AcroForm, anotaciones, acciones, valores y metadatos personales en copia preparada. Área administrativa siempre vacía.
- Texto fuera de caja: 422 `documents.text_overflow` con campo; datos faltantes: 422 `documents.data_missing`. Sin truncamiento silencioso.
- Fuente requerida ausente: 409 `documents.font_unavailable`. Plantilla/recurso no disponible: 409 `documents.template_unavailable`.
- Manifiesto publicado registra dimensiones por página. Antes de descargar/generar, verificar número/dimensiones de páginas y límites de cada campo: página existente, rectángulo dentro de hoja y tamaños de fuente finitos, positivos y ordenados. Metadatos o mapa inválidos devuelven 409, sin generar archivo ni consumir folio.
- Carta SS original es escaneo ya firmado: emisión bloqueada hasta base limpia configurada. Aprobar/emisión no consume folio ni cambia estado si faltan recursos.
- Carta SS se almacena como `origin=system`, aprobada al emitir, con plantilla y hash. Descarga posterior reutiliza bytes y folio.
- Estampas PNG transparentes solo en documentos institucionales, con coordenadas, dimensiones y opacidad configurables. Dummies identificables; nunca extraer firmas reales del ejemplo.
- Impresión HTML de CP PP y liberaciones SS/PP conserva comportamiento actual. No ofrecer impresión HTML alternativa para documentos de esta matriz.

## Interfaz y estadísticas

- Detalles exponen `documentWorkflowVersion` y `documentRequirements`: código, título, modo de descarga, clave PDF/plantilla, permiso de carga y bloqueo.
- Visor autenticado por blob, descarga y revocación al cerrar/desmontar. Rechazo muestra observaciones y permite corregir.
- Cola paginada con filtros de tipo (incluido `general`), código, periodo, estado y búsqueda. Error de API muestra error/reintento, nunca datos ficticios ni dictámenes locales.
- SS mantiene diez bloques de 10%. En v2, evaluaciones exige los siete documentos separados. EV1/EV2/EV3 estadísticos agregan supervisor+autoevaluación por bimestre; EV3 incluye actividades.
- Comparten cálculo detalle, listado y reportes. V1 conserva cálculo histórico.
- Las marcas CP, CA, SOL y CT de seguimiento usan la misma regla por versión que el porcentaje: en v2, fecha/folio sin PDF aprobado no acredita documento; v1 conserva sus hitos históricos.

## Persistencia y verificación

- Migración PostgreSQL incremental, sin ejecutar `database/design/schema.sql` ni `database/legacy/`. Conserva evidencias, auditoría y versiones.
- Documento agrega fecha, origen, SHA-256 y versión de plantilla. Unicidad vigente incluye `tramiteId=null`. Concurrencia mediante tokens y transacciones; limpiar archivo nuevo si falla BD.
- Probar aprobación IMSS anual y cambio de año, permisos, revisión/reemplazo concurrente, unicidad de folios, firma documental, históricos y errores de almacenamiento.
- Verificar dimensiones, páginas, contenido vacío y ausencia de datos ocultos. Comparar renders fuera de cajas autorizadas; página 2 de PP intacta. Probar acentos, Ñ, texto largo y desbordamiento.
- Publicación incluye plantillas/mapas y fuentes autorizadas configuradas explícitamente. No redistribuir fuentes del sistema operativo automáticamente.

## Mapas SS

Casillas se marcan por valores explícitos de carrera y categoría; valores sin mapeo devuelven 422. Categorías combinadas marcan todas las casillas aplicables. Fechas fragmentadas usan día, mes en español y año. Preparar plantillas conserva mapas calibrados. Campos opcionales no fuerzan datos inexistentes, como segundo apellido.

## Recursos institucionales

Las estampas del manifiesto solo aplican a plantillas marcadas `institutional=true`; requieren autoridad activa en los datos de emisión, PNG y hash, página y rectángulo dentro de la hoja, y opacidad 0–1. Cualquier estampa configurada sobre anexos de firma física se rechaza. Los PNG dummy dicen PRUEBA y son genéricos; no reproducen firmas existentes.

## Inmutabilidad documental

La captura digital de calificaciones SS/PP queda exclusiva para staff. Los siete criterios deben ser explícitos; no sustituir datos ausentes por Excelente. Reporte y Compromiso aprobados no admiten edición de datos. Un trámite terminal no admite nuevas evidencias. IMSS general mantiene su renovación independiente.

## Atomicidad de apertura y folios

Un índice único parcial por alumno impide dos SS o PP activos/liberados (excluye cancelados y no conformes). Apertura y bitácora comparten transacción. Las acciones que consumen folio confirman serie, trámite y bitácora en una transacción; conflictos concurrentes devuelven 409 sin consumir folios extra. La emisión directa SS sigue bloqueada sin base limpia.

## Emisión SS persistida

Carta SS requiere clave `ss-presentation-letter` institucional, sello y firma configurados. Aprobación exige SOL aprobado en v2 e IMSS vigente; emisión guarda PDF, SHA-256, origen system, revisor y versión de plantilla en la misma transacción del folio. Repetir emisión devuelve el archivo existente sin cambiar fecha/folio. Si falla BD, se limpia solo el archivo nuevo. Sin base limpia se mantiene 409.

## Bandeja administrativa

Identificar documentos generales como General / IMSS. Un dictamen aprobado no implica sello digital. Las indicaciones de revisión no deben fijar periodos académicos de ejemplo. Verificar en navegador filtros enviados al servidor, fecha IMSS, rechazo obligatorio y error de API sin datos ficticios.

## Fecha declarada en cargas IMSS

Las cargas documentales y adaptadores IMSS propagan `documentDate` multipart al expediente pendiente. Si se proporciona, no admite fechas futuras. Los clientes actuales solicitan la fecha al adjuntar; los adaptadores conservan compatibilidad con clientes anteriores que no la envían, dejando fecha nula y revisión pendiente. La fecha declarada nunca acredita vigencia ni sustituye la confirmación del encargado. Reenviar los mismos bytes no cambia la fecha ni el dictamen existentes; cambiar solo la fecha exige confirmación administrativa.
