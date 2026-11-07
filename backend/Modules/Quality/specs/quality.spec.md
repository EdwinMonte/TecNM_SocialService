> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

# Quality backend

Contrato compartido: [spec raíz de calidad](../../../../docs/specs/quality.spec.md).

## Responsabilidades
- `NonConformancesController`: Registro, seguimiento y resolución de Producto No Conforme (PNC) bajo estándar ISO 9001.
- `StatusLogsController`: Consulta append-only de trazabilidad de cambios de estado en trámites (`tramite_status_logs`).
