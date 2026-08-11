# Planner

> Estado: planificación y ejecución están separadas. `IPlanner` es una fachada de planificación pura sobre `IPlanBuilder`; `IPlanExecutor` es la única capa del Planner que invoca `IToolRouter`.

`IPlanBuilder.BuildAsync` valida un `Goal` explícito y construye un `Plan` determinista, con identificadores, argumentos preservados y estado `Planned`. No depende del router, de Tools concretas ni de infraestructura de entrada/salida, por lo que construir un plan no produce efectos laterales.

`IPlanExecutor.ExecuteAsync` recibe un plan ya construido. Antes de ejecutar, valida por completo el plan, sus tareas, identificadores, argumentos y estado `Planned`; un plan inválido no llega al router. Para cada tarea consulta `IExecutionGate`; la ejecución v1 es secuencial y fail-fast. Los estados incluyen `RequiresConfirmation` y `Denied`, además de `Failed` y `Cancelled`.

`POST /planner/execute` conserva la operación explícita existente, pero compone visiblemente `IPlanner` e `IPlanExecutor`; no oculta ejecución dentro de `IPlanner.PlanAsync`. Aún no hay endpoint separado de creación, persistencia, reanudación, replanificación, confirmación humana ni políticas de permisos.
