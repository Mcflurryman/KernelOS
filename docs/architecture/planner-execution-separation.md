# Separación de planificación y ejecución

La planificación es una operación pura: `Goal` → `IPlanBuilder` → `Plan(Planned)`. El builder genera IDs, conserva los argumentos y no tiene acceso a `IToolRouter`, filesystem, HTTP ni herramientas concretas.

La ejecución es una frontera posterior y explícita: `Plan(Planned)` → `IExecutionGate` → `IPlanExecutor` → `IToolRouter`. El executor valida el plan completo antes de la primera llamada al router, consulta el gate para cada tarea, ejecuta secuencialmente y se detiene ante error, cancelación, confirmación pendiente o denegación. No interpreta lenguaje natural ni crea o modifica tareas.

La autorización se aplica mediante policy y aprobaciones acotadas; Kai y el builder siguen sin ejecutar acciones.
