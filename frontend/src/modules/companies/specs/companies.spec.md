# Catálogo compartido de empresas

Contrato: [Empresas](../../../../../docs/specs/companies.spec.md). Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md).

Ruta `/companies`, autenticada para admin, ambos encargados y jefatura. Los primeros gestionan; jefatura consulta. No admite alumnos.

Reutilizar componentes institucionales, `UiBasePage` fluid y tokens de tema. Listar con búsqueda, paginación y filtros de los seis tipos y estado. Formulario con tipo obligatorio y conservación de sector educativo para 911. Supervisores y convenios se gestionan en detalle, con confirmación de eliminación y PDF autenticado.

Cargas y guardados bloquean doble envío. Errores de validación permanecen visibles; cargas de listado cancelan respuestas anteriores. Mobile permite desplazamiento dentro de la tabla sin desbordar la página.
