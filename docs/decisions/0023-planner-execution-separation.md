# ADR 0023: Separación entre planificación y ejecución

## Estado

Accepted.

## Decisión

KernelOS separa la construcción de planes (`IPlanBuilder`) de su ejecución (`IPlanExecutor`). `IPlanner` se conserva como fachada compatible de planificación pura. El builder no depende de `IToolRouter`; el executor es la frontera del Planner que lo utiliza.

El executor valida por completo el plan antes de ejecutar, procesa tareas de forma secuencial, aplica fail-fast y propaga cancelación. Un plan válido se entrega en estado `Planned`, nunca `Executing`.

## Consecuencias

La creación de un plan no produce efectos laterales. Kai continúa sin ejecutar Planner ni Tools: confirmación humana y políticas de permisos se reservan para Tool Confirmation & Execution Policy. El endpoint legado de ejecución usa explícitamente builder/planner y executor, sin atribuir la ejecución a `IPlanner.PlanAsync`.
