# Migración de tipología y métricas

Aplicar sobre PostgreSQL con respaldo, después de `20261103_document_workflow_v2.sql`.
No ejecutar `database/design/schema.sql` sobre una instalación existente.

1. Ejecutar `20261106_company_type_prepare.sql`. Agrega el campo y clasifica público, social y privados con tamaño válido.
2. Revisar el listado de pendientes, incluidos educativos. Asignar individualmente `company_type` con valores `public`, `micro`, `small`, `medium`, `large` o `social`. Conservar `sector=educational` para el reporte 911. No ejecutar seeds demo en una instalación existente.
3. Ejecutar `20261106_company_type_finalize.sql`. Aborta si alguna empresa, incluso borrada, sigue sin clasificación. Impone `NOT NULL` y crea índices y vistas agregadas.
4. Desplegar backend y frontend coordinadamente. `companyType` es obligatorio en `POST`/`PUT /api/companies`; los clientes anteriores deben actualizarse. Las consultas conservan los demás campos y agregan filtro `companyType`.

Detener escrituras durante clasificación y finalización. La versión nueva de la API requiere ambas migraciones completas. Una falla de finalización revierte su transacción; corregir pendientes y volver a ejecutarla. No elimina empresas, evidencias ni historiales.

Verificación aislada: `pwsh -File backend.Tests/run-isolated.ps1` con Docker, o `pwsh -File backend.Tests/run-native-isolated.ps1` con PostgreSQL 18 local. Ambas crean una base temporal `document_checks`; nunca usan la conexión del proyecto. Prueban migración incompleta, esquema nuevo, permisos HTTP y agregaciones SQL.
