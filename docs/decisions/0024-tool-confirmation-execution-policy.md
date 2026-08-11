# ADR 0024: Tool Confirmation & Execution Policy

## Estado

Accepted.

## Decisión

KernelOS separa planificación, autorización y ejecución. `IExecutionPolicy` produce decisiones explícitas `Allow`, `RequireConfirmation` o `Deny`; `IExecutionGate` aplica esa decisión y verifica aprobaciones antes de que `IPlanExecutor` llame al router.

La policy v1 falla cerrada: metadata desconocida requiere confirmación. Las aprobaciones son de un solo uso, expiran, están acotadas a plan y tarea, y se vinculan a una huella SHA-256 determinista de Tool y argumentos canónicos.

## Consecuencias

El executor preserva ejecución secuencial y fail-fast, distinguiendo confirmación pendiente y denegación de un fallo técnico. Kai continúa sin ejecutar Planner. El siguiente milestone es Kai Planner Orchestration; no se incorporan providers concretos, interfaz de confirmación ni autonomía.

Las APIs públicas de ejecución directa se limitan a Tools que la policy permite como read-only mediante `IReadOnlyToolExecutionGateway`. Los efectos laterales se ejecutan exclusivamente desde `IPlanExecutor` después de `IExecutionGate`; no hay una API pública que cree aprobaciones.
