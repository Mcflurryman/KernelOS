# Separación de planificación y ejecución

La planificación es una operación pura: `Goal` → `IPlanBuilder` → `Plan(Planned)`. El builder genera IDs, conserva los argumentos y no tiene acceso a `IToolRouter`, filesystem, HTTP ni herramientas concretas.

La ejecución es una frontera posterior y explícita: `Plan(Planned)` → `IPlanExecutor` → `IToolRouter`. El executor valida el plan completo antes de la primera llamada al router, ejecuta tareas secuencialmente y se detiene ante error o cancelación. No interpreta lenguaje natural ni crea o modifica tareas.

La autorización humana no forma parte de esta versión. La separación permite introducirla entre ambos pasos sin hacer que Kai o el builder ejecuten acciones.
