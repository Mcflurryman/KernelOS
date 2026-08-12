# Planner

El builder conserva el contexto de audit recibido o crea un flow interno para Planner directo; el executor reutiliza ese flow para preflight y ejecución. Los eventos nunca incluyen argumentos ni resultados.

`POST /kai` puede iniciar el flujo Planner con una Tool explícita; sigue pasando por Plan, policy, gate y executor.

> Estado: planificación y ejecución están separadas. `IPlanner` es una fachada de planificación pura sobre `IPlanBuilder`; `IPlanExecutor` es la única capa del Planner que invoca `IToolRouter`.

`IPlanBuilder.BuildAsync` valida un `Goal` explícito y construye un `Plan` determinista, con identificadores, argumentos preservados y estado `Planned`. No depende del router, de Tools concretas ni de infraestructura de entrada/salida, por lo que construir un plan no produce efectos laterales.

`IPlanExecutor.ExecuteAsync` recibe un plan ya construido. Antes de ejecutar, valida por completo el plan, sus tareas, identificadores, argumentos y estado `Planned`; un plan inválido no llega al router. `IExecutionPreflight` consulta el gate para todas las tareas y agrega `Denied > RequiresConfirmation > Authorized`, por lo que no hay Tool call antes de la autorización global. Después, la ejecución v1 es secuencial y fail-fast; no existe rollback de efectos externos. Los estados incluyen `RequiresConfirmation` y `Denied`, además de `Failed` y `Cancelled`.

`POST /planner/execute` conserva la operación explícita existente, pero compone visiblemente `IPlanner` e `IPlanExecutor`; no oculta ejecución dentro de `IPlanner.PlanAsync`. Aún no hay endpoint separado de creación, persistencia, reanudación, replanificación, confirmación humana ni políticas de permisos.

Cuando requiere confirmación, el endpoint devuelve un identificador pending opaco. La confirmación y la ejecución posterior se realizan mediante endpoints separados y el snapshot almacenado. El Planner HTTP actual sigue produciendo un único task por request, aunque la Approval Surface puede conservar planes multi-task creados por otros callers internos.
