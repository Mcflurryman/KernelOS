# Kai Planner Orchestration

El flow de audit creado por Kai se conserva en el Plan, pending snapshot y executor. El audit solo registra metadata segura y no modifica esta orquestación.

Kai puede planificar una acción explícita y entregar el Plan a `IPlanExecutor`. La policy sigue decidiendo Allow, RequiresConfirmation o Deny.

Kai no llama Tools ni `IToolRouter`, no manipula approvals ni pending stores y no aprueba acciones. Los side effects devuelven un `PendingExecutionId` y confirmación pública; la aprobación sigue en la Execution Approval Surface. Kai no crea todavía planes multi-task desde lenguaje natural.

Auto conserva Chat por defecto y RAG ante señales documentales. No hay agent loop ni reintentos autónomos.
