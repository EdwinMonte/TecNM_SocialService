# Catálogo compartido de empresas

Contrato: [Empresas](../../../../../docs/specs/companies.spec.md). Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md).

Ruta `/companies`, autenticada para admin, ambos encargados y jefatura. Los primeros gestionan; jefatura consulta. No admite alumnos.

Reutilizar componentes institucionales, `UiBasePage` fluid (`fullscreen-page` 100dvh sin scroll exterior y con scroll interno de tabla y encabezados fijos) y tokens de tema mate claro/oscuro. Encabezado ejecutivo con 4 KPIs interactivos (Total, Activas, Inactivas, Internas TecNM) con borde dorado únicamente en el KPI activo. Buscador con autocompletado interactivo, debounce de `280 ms`, botón único azul (`✕`) para limpiar y filtros de los seis tipos y estado. Paginación estandarizada de `10` registros por página (`pageSize = 10`). Formulario con tipo obligatorio y conservación de sector educativo para 911. Supervisores y convenios se gestionan en detalle, con confirmación de eliminación y PDF autenticado. Notificaciones con `UiToast`.

Cargas y guardados bloquean doble envío. Errores de validación permanecen visibles; cargas de listado cancelan respuestas anteriores. Mobile permite desplazamiento dentro de la tabla sin desbordar la página.
