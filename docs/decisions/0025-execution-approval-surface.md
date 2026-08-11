# ADR 0025: Execution Approval Surface

## Estado

Accepted.

## Decisión

KernelOS introduce un pending execution en memoria con identificador opaco, snapshot inmutable y TTL. La confirmación API acepta exclusivamente `Approve` o `Reject`; KernelOS genera el approval, fingerprint, timestamps y scope. Confirmar no ejecuta: solo un endpoint posterior puede tomar una approval ya creada y entregar el snapshot a `IPlanExecutor`.

## Consecuencias

No existe creación arbitraria de approvals ni auto-aprobación de Kai. Los endpoints directos continúan restringidos a read-only. V1 no reanuda planes multi-tarea parciales; Kai Planner Orchestration es el siguiente milestone.
