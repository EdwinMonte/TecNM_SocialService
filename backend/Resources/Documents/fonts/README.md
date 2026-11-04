# Fuentes oficiales

Configurar `Documents__FontsRootPath` con un directorio que contenga `arial.ttf` y `GOTHIC.TTF`, usando archivos cuya licencia permita su uso institucional. No se incluyen fuentes del sistema operativo en el repositorio.

Para comprobaciones locales: `DOCUMENT_TEST_FONTS=C:/Windows/Fonts`. La API no activa esta ruta automáticamente. Fuente ausente devuelve `409 documents.font_unavailable`.
