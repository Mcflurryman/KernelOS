# Kai Planner Orchestration

Kai puede planificar una acción explícita y entregar el Plan a `IPlanExecutor`. La policy sigue decidiendo Allow, RequiresConfirmation o Deny.

Kai no llama Tools ni `IToolRouter`, no manipula approvals ni pending stores y no aprueba acciones. Los side effects devuelven un `PendingExecutionId` y confirmación pública; la aprobación sigue en la Execution Approval Surface. Kai no crea todavía planes multi-task desde lenguaje natural.

Auto conserva Chat por defecto y RAG ante señales documentales. No hay agent loop ni reintentos autónomos.
