# ADR 0026: Kai Planner Orchestration

## Estado

Accepted.

## Decisión

Kai usa `IPlanner`, `IPlanExecutor` e `IExecutionConfirmationService` para orquestar acciones explícitas. Puede ejecutar solo acciones autorizadas por policy. `RequiresConfirmation` detiene Kai y se devuelve al caller con datos públicos seguros.

Kai no depende de `IToolRouter`, Tools, ApprovalStore ni PendingStore. Deny es terminal; no hay fallback, agent loop, reintentos autónomos ni confirmación de planes multi-task.
