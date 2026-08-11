# ADR 0028: Multi-task Authorization Preflight

## Status

Accepted.

## Context

`PlanExecutor` evaluaba y ejecutaba tareas en el mismo bucle. Una tarea posterior podía requerir confirmación o ser denegada después de que una tarea anterior ya hubiese producido un efecto. La Execution Approval Surface solo soportaba un pending de una tarea.

## Decision

Antes de cualquier llamada a Tool, el executor realiza preflight completo y agrega las decisiones con precedencia `Deny > RequireConfirmation > Allow`. Un resultado distinto de Allow produce cero side effects.

La Approval Surface conserva un pending del plan completo con snapshot inmutable. Approve crea approvals internas por tarea, scoped por `PlanId`, `TaskId` y fingerprint; confirmar no ejecuta. Pending y approvals son one-shot y expiran. La ejecución toma el pending una sola vez y permanece secuencial y fail-fast.

No se implementa rollback de efectos externos ni transacción distribuida.

## Consequences

Los planes multi-task quedan autorizados globalmente antes de ejecutar. El flujo de approvals es más complejo, pero evita ejecución parcial por una autorización posterior pendiente. La ejecución sigue pudiendo completar tareas previas antes de un fallo, cancelación o error posterior.
