# Implementación documental v2: estado

Implementación preparada para prueba local en Docker: http://localhost:3000. Guía: [LOCAL_DOCUMENT_TESTING.md](../LOCAL_DOCUMENT_TESTING.md). Migración aplicada a la base local respaldada; API, frontend y PostgreSQL activos en contenedores. Publicación en producción pendiente de revisión institucional y recursos autorizados.

**Ajuste vigente autorizado:** Carta de Presentación SS mediante carga de PDF firmado por alumno/staff y dictamen. Aprobar Solicitud no emite carta ni folio. Plantilla limpia pendiente únicamente para habilitar emisión automática futura; no bloquea el flujo temporal.

## Implementado

- Contrato canónico v2 enlazado desde contratos y specs locales.
- Metadatos documentales, versiones de flujo, FK de admisión IMSS y fechas del formato PP en entidades/configuraciones.
- Migración incremental preparada, sin ejecutar. Requiere PostgreSQL 15+.
- Catálogo y reglas de carga por rol; cálculos de progreso por versión.
- IMSS del perfil delega al expediente; confirmación anual de fecha al aprobar y requisito documental al crear/aprobar solicitudes.
- Protección optimista con xmin en documentos, trámites y series. Reemplazo documental transaccional, SHA-256 e idempotencia de carga/revisión.
- Guardar Carta Compromiso no declara firma. Aprobación de su evidencia acredita firma.
- Emisión SS bloqueada por ausencia de base limpia.
- Campos administrativos para fecha/motivo IMSS; bandeja deja de usar ejemplos cuando falla la API.
- PDFsharp 6.2.4 con dependencia Pkcs corregida 10.0.13.
- Comprobaciones ejecutables de políticas en backend.Tests.

## Segundo avance

- Motor de superposición PDFsharp con manifest, SHA-256, fuentes explícitas, ajuste de texto y rechazo de desbordamiento.
- Ocho plantillas estáticas preparadas, sin modificar fuentes. Solicitud sin widgets ni datos originales. Preparación reproducible en tools/pdf.
- Endpoints PDF y plantillas; endpoint PP de fechas independiente de calificaciones.
- DTOs SS/PP exponen requisitos y versión. Checklist y visor reutilizables integrados en vistas del alumno.
- Descargas del alumno para formatos de matriz usan backend. PP deja de guardar calificaciones al prellenar.
- Comprobación ejecutable de generación PP, acentos, fechas, dos páginas y error de desbordamiento. Comparación pixel a pixel de segunda página sin cambios.
- Builds backend/frontend pasan. Faltan QA visual de todos los mapas y tareas indicadas abajo.

## Tercer avance

- Dictamen del reporte delega a su PDF. Hitos de revisión actualizados con EF en la misma transacción, incluidos reportes rechazados.
- SS v2 exige el catálogo completo para conclusión/liberación; v1 conserva requisitos históricos.
- Estadísticos y detalle comparten progreso y agregación de evaluaciones por versión.
- Checklist administrativo SS/PP integrado; permite al coordinador cargar CA PP. Formatos de matriz se descargan desde backend.
- Bandeja con filtros/paginación del servidor, general/IMSS, confirmación de fecha IMSS y reintento. Sin documentos ficticios ante fallos ni dictámenes locales.
- Almacenamiento valida estructura PDF y rechaza cifrado/archivos falsos; acceso fuera del directorio raíz bloqueado.
- Pruebas de almacenamiento detectaron y corrigieron excepción no controlada del parser para archivos truncados.
- Migración aplicada con éxito únicamente en PostgreSQL 18 temporal: esquema v1, seeds demo, migración incremental y comprobaciones de IMSS, renovación, rechazo, unicidad con tramiteId nulo y xmin.
- Contenedor temporal eliminado al concluir. Base del proyecto intacta. Backend y frontend compilan; comprobaciones pasan.

## Pendiente para completar el plan aprobado

1. Revisión institucional del ejemplo convertido y de las posiciones de estampado al incorporar la base limpia. Ejemplo PDF y estampas configurables implementados. Mapas SS de Solicitud, Compromiso, Reporte y Actividades calibrados con muestras: casillas de carrera/categoría, fecha fragmentada y marca final. Requieren aprobación visual institucional antes de publicar.
2. Incorporar y calibrar la base limpia CP SS cuando se entregue, para emisión institucional futura. Actualmente funciona la carga firmada autorizada por el usuario.
3. Completar revisión institucional de todos los formatos y configurar fuentes autorizadas. Recorridos de interfaz SS/PP, bandeja, cargas por rol, estados terminales y perfil IMSS ya verificados en navegador.
4. Migración local aplicada con respaldo previo. Para producción: repetir respaldo, revisión y migración incremental en el entorno destino. Ciclos de corrección/cierre, concurrencia y errores de guardado pasan en suite aislada.

Las comprobaciones incluyen servicios/persistencia y HTTP mediante TestServer. Faltan la base limpia de CP SS y fuentes autorizadas para publicación. No desplegar hasta completar pendientes.

## Verificación de mapas SS

Se agregaron muestras ejecutables de cuatro formatos, rechazo de carreras sin mapeo, casillas por valor y fechas en español. Preparar plantillas conserva los mapas calibrados. Verificación de píxeles fuera de áreas autorizadas en tools/pdf/verify_templates.py. Campos administrativos y firmas físicas permanecen en blanco.

## Ejemplo y estampas

- Ejemplo de aceptación SS convertido con Word en modo oculto/solo lectura, registrado por hash y revisado visualmente. Original intacto.
- PNG genéricos PRUEBA y script reproducible, sin reutilizar firmas reales.
- Estampado configurable con proporción, página, coordenadas y opacidad. Requiere plantilla institucional y autoridad activa; rechaza rutas, hashes y parámetros inválidos.
- Pruebas de rechazo en anexos físicos, autoridad ausente y opacidad inválida; muestra de estampado generada. Compilación backend correcta.
- El manifiesto no habilita estampas en anexos ni emisión CP SS. Base limpia pendiente.

## Protección de dictámenes

Captura calificada SS/PP exclusiva para staff, con siete criterios explícitos. Compromisos y reportes aprobados bloquean edición. Trámites terminales bloquean carga de nuevas evidencias; renovación IMSS general permanece independiente. Compilación y comprobaciones de permisos pasan. Pendiente verificar estas transiciones por HTTP/PostgreSQL y retirar markup residual de formularios antiguos.

## Limpieza de interfaz

Retirados formulario SS de calificaciones, criterios/observaciones de prellenado PP, checklists duplicados y manejadores de cargas sustituidos. Helper HTML limitado a CP PP y liberaciones, con comprobación ejecutable de compatibilidad y escape de datos. Checklist oculta edición de documentos aprobados/trámites terminales; API expone cargas deshabilitadas al cerrar. Fechas del formato PP se recuperan al reabrir el detalle. Builds y comprobación de impresión pasan.

## Pruebas HTTP

Agregada suite con ASP.NET Core TestServer y login real por cookies. Pasan 401 anónimo, 403 de roles, 404 de recurso ajeno, restricciones de calificación, descarga de ejemplo PDF, revisión IMSS con fecha/motivo obligatorios, proyección del perfil y filtros/paginación. Ejecutada contra PostgreSQL temporal migrado, sin tocar la base del proyecto. Script reproducible en backend.Tests/run-isolated.ps1 fija el esquema v1 y elimina su contenedor. Persisten pendientes de concurrencia HTTP y recorrido visual.

## Concurrencia HTTP y almacenamiento

Dos reemplazos HTTP simultáneos dejan una sola versión vigente; cada petición devuelve éxito o conflicto controlado. Fallo de escritura inyectado desde la suite conserva bytes del documento previo y referencia IMSS del perfil. La suite completa aislada pasa y elimina su contenedor. Faltan concurrencia de folios/apertura, fallos de BD posteriores al archivo y recorrido visual.

## Apertura y folios atómicos

Índices únicos parciales impiden dos trámites SS/PP vigentes por alumno. Apertura PP y bitácora comparten transacción; acciones SS/PP con folios confirman serie, trámite y bitácora juntas. Pruebas HTTP aisladas verifican apertura concurrente con un solo trámite y aprobación concurrente con un solo incremento de folio. Fallo de BD inyectado después de escribir archivo conserva versión previa y limpia el archivo nuevo. Suite completa aislada pasa; base del proyecto intacta.

## Emisión institucional SS

Implementado guardar CP como system/approved con SHA-256 y versión de plantilla, dentro de la transacción de folio/aprobación. Descargas reutilizan exactamente los mismos bytes; repetir aprobación no reemite. Ruta directa exige trámite aprobado y no elude el control de plantilla. Faltando plantilla institucional limpia devuelve 409 sin folio. Pruebas usan una copia sintética fuera del catálogo publicado; verifican emisión, hash, idempotencia y rollback de estado/folio/archivo ante fallo de BD. No se habilitó emisión real ni se alteró el escaneo oficial.

## Recorrido visual SS

Prueba de navegador con fixtures reales de la API aislada: escritorio 1440px y móvil 390px, sin errores JS ni desbordamiento horizontal. Visor abre blob, Escape cierra y retira iframe; al liberar trámite desaparecen cargas/edición. Se movió descarga PDF al footer para que siempre sea visible, incluso sin soporte de visor nativo. Capturas revisadas; PDF nativo headless no representa la revisión del contenido PDF, que tiene su suite separada. Pendiente extender recorrido a PP y bandeja administrativa.

## Recorrido visual PP y bandeja administrativa

Extendido script SS a PP con fixtures exportados desde API aislada para alumno y admin. Verifica editor de dos fechas sin calificaciones, ausencia de carga CA del alumno, carga multipart CA de staff y layout móvil sin desbordamiento. Capturas escritorio/móvil revisadas.

Bandeja con estados sintéticos verifica fecha obligatoria IMSS, rechazo obligatorio, payloads de ambos dictámenes, búsqueda y filtro General enviados al servidor, fallo API sin datos ficticios y reintento. Corregidas etiquetas General / IMSS, referencia falsa a sello digital y periodo fijo AD26. Visor libera blob al cerrar/desmontar; filtro Tipo tiene etiqueta accesible explícita. Capturas revisadas, sin errores JS.

Suite completa HTTP/PostgreSQL temporal y build frontend pasan. Estas comprobaciones visuales interceptan respuestas API; no sustituyen la integración HTTP probada por separado. La base del proyecto permanece intacta. Continúan pendientes plantilla limpia CP SS, fuentes autorizadas y aplicación de migración en el entorno de destino.

## Fecha declarada IMSS

Corregida pérdida del campo multipart documentDate en cargas documentales y adaptadores de Students. Se guarda con el PDF pendiente; fechas futuras se rechazan antes de escribir archivo. Clientes antiguos sin fecha conservan compatibilidad y revisión obligatoria. Reenvío de mismos bytes conserva fecha confirmada y aprobación.

Perfil, formularios SS/PP y padrón administrativo solicitan fecha al adjuntar PDF. Separadas fecha declarada del nuevo archivo y fecha confirmada del dictamen; se restablecen al cambiar de alumno. Perfil muestra fecha registrada y observaciones de rechazo. Normalizado un fragmento de codificación mixta en DocumentsService a UTF-8.

Pruebas HTTP verifican persistencia declarada, rechazo de fechas futuras en adaptador y endpoint general, confirmación administrativa e idempotencia. Suite aislada completa y build frontend pasan. Navegador verifica fecha requerida, multipart y layout móvil del perfil. Base del proyecto intacta.

## Ciclo documental SS por HTTP

Compromiso: guardar datos y generar PDF no acredita firma; rechazo mantiene firma/fecha nulas, reemplazo aprobado acredita firma y fecha. Edición posterior bloqueada; versión sustituida conserva motivo de rechazo.

Reportes: revisión sin PDF devuelve 422. Rechazar desde ruta del reporte rechaza la misma evidencia; reemplazar y aprobar desde ruta documental aprueba datos del reporte y limpia observaciones. Edición posterior devuelve 409. Consulta con includeSuperseded conserva ambas versiones. Suite completa aislada pasa.

## Permisos de eliminación y cambio de año

Eliminación pendiente comparte validación del catálogo y estado terminal con carga. Alumno no elimina CA PP cargada por staff; encargado SS tampoco puede cargar, revisar ni eliminar CA PP. Jefatura conserva consulta sin eliminación; coordinador PP sí puede eliminar pendiente de su dominio. Pruebas HTTP verifican 403, conservación de evidencia ante rechazo de permisos, 409 en trámite cerrado y 204 para eliminación autorizada.

Reloj controlado en TestServer verifica 05:59:59 y 06:00:00 UTC del primero de enero: expiración exactamente a medianoche de México. Aperturas SS/PP de alumno y admin devuelven 422 por IMSS anual; SS conserva descarga y referencia de admisión, PP conserva edición de fechas y descarga sin crear evaluación calificada.

La prueba detectó falta de Include(Student) en guardado y descarga de formato PP: el propietario recibía 404. Corregidas ambas consultas; propietario funciona y alumno ajeno mantiene 404. Normalizados fragmentos de codificación mixta en muestras PDF, controller de plantillas y almacenamiento; nombres con acentos y Ñ se verifican en PDF generado. Pasan suite completa aislada y verificador de nueve plantillas. Base del proyecto intacta.

## Geometría y empaquetado de plantillas

Manifiesto registra dimensiones de las nueve plantillas, incluidas las dos páginas PP. Motor exige cantidad/dimensiones coincidentes y valida rectángulos, índices de página y tamaños de fuente antes de entregar/generar. JSON nulo/malformado y mapas inválidos devuelven 409 documents.template_unavailable. Pruebas locales cubren estos rechazos; HTTP institucional verifica que no se consume folio con metadatos inválidos.

Preparador conserva mapas calibrados y calcula dimensiones también para entradas externas como el ejemplo SS. Ejecutado con comprobación de hashes de archivos oficiales antes/después: fuentes originales intactas. Verificador exige geometría y conserva comparaciones visuales; nueve plantillas pasan. dotnet publish local incluye los 14 recursos documentales con hashes idénticos. Suite HTTP/PostgreSQL aislada pasa. Sin despliegue ni cambios a base del proyecto; continúan recursos oficiales y revisión institucional pendientes.

## Cierre SS/PP y seguimiento histórico

Pruebas HTTP completan SS v2: seis evaluaciones aprobadas sin EV_ACT mantienen 80%; EV_ACT pendiente bloquea conclusión; aprobación completa bloque y alcanza 90%; liberación alcanza 100%. Paquetes EV1 no se admiten en v2. PP requiere CA, EV y CT aprobados. Dos liberaciones simultáneas por proceso dejan un solo éxito y un incremento de folio. Descargas posteriores funcionan y cargas/edición PP quedan bloqueadas. El ciclo se ejecuta tras vencer IMSS, confirmando continuidad de trámites admitidos.

Detectada divergencia en matriz de seguimiento: acreditaba CP/CA/SOL/CT por fechas en v2 aunque el porcentaje exigía PDF aprobado. DocumentProgressCalculator centraliza ahora esos hitos para porcentaje y matriz. Pruebas HTTP comparan marcas y porcentajes; pruebas de compatibilidad conservan hitos por fecha y paquetes v1, sin trasladar aprobaciones a v2. Suite completa aislada y verificación de nueve plantillas pasan. Base del proyecto intacta.
