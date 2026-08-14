# Conversation Pending Correlation

Una confirmation puede sobrevivir un refresh del navegador sin convertir el pending execution en durable. SQLite conserva exclusivamente la identidad `ConversationId`, `PendingExecutionId`, IDs de mensajes y creación en `conversation_pending_executions` (schema v3). No almacena contenido, argumentos, snapshots, fingerprints, approvals, audit ni resultados.

`ConversationTurnOrchestrator` registra la correlación después de persistir el User y, si existe, el Assistant. Si el registro falla o entra en conflicto, mantiene `ConfirmationRequired` y el pending funcional, pero añade una advertencia segura: la recuperación desde la conversación no estará garantizada.

```text
Conversation UI -> turn -> Kai/Planner -> pending in-memory
  -> correlation SQLite -> GET conversation pending-executions
  -> explicit Approve | Reject -> explicit Execute
```

La correlación es UX, nunca autoridad. `IExecutionConfirmationService`, pending/approval stores, gate y executor mantienen policy, TTL, fingerprint, scopes y one-shot. `ConversationId` no aprueba ni ejecuta, y `AuditFlowId` continúa siendo una correlación de audit independiente.

El endpoint de discovery devuelve sólo estado público y confirmation segura. Pending vigente deriva `Pending`, `Approved` o `Rejected`; una correlación cuyo pending no está en memoria es `Unavailable`, sin afirmar falsamente que expiró o se ejecutó. Tras restart Pending y Approval desaparecen; la correlación queda visible como `Unavailable` y no admite acciones.

La UI usa acciones explícitas de dos pasos: Approve no ejecuta; Execute sólo aparece tras Approved. No hay reintentos automáticos. El resultado seguro de Execute es transitorio y no se persiste como mensaje. Borrar una conversación elimina la correlación por cascade, pero no cancela el pending in-memory.
