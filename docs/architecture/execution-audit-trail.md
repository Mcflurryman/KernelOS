# Execution Audit Trail

KernelOS mantiene un Audit Trail interno para reconstruir de forma segura el ciclo de una ejecución. Su objetivo es observabilidad de dominio: **audit no es autoridad**. No decide policy, no crea approvals, no cambia routing ni modifica resultados funcionales.

## Correlación y eventos

`AuditFlowId` es opaco, inmutable y generado internamente por flow. `ExecutionAuditContext` transporta flow y origen. Kai crea un flow por request; `PlanBuilder` lo conserva en el `Plan` y crea uno de origen `Planner` para un plan directo sin contexto. El snapshot pending conserva el contexto, que vuelve al executor tras la aprobación.

`AuditEvent` solo contiene metadata segura y tipada: IDs de correlación, tipo, origen, timestamp, estados o decisiones seguras, riesgo, códigos y duración. No almacena prompts, mensajes, historial, documentos, contexto RAG, argumentos de Tools, resultados, snapshots, rutas sensibles, secretos, mensajes de excepción ni stack traces.

La propiedad de cada transición es única:

- `KaiAgent`: `KaiRequestStarted`, `KaiRouteSelected`.
- `PlanBuilder`: `PlanCreated`.
- `ExecutionPreflight`: `PreflightStarted`, `TaskAuthorizationEvaluated`, `PreflightCompleted`.
- `ExecutionConfirmationService`: `PendingExecutionCreated`, `ExecutionApproved`, `ExecutionRejected`.
- `PlanExecutor`: eventos de inicio, tarea y terminales de ejecución del plan.
- `ReadOnlyToolExecutionGateway`: `DirectReadOnlyExecutionStarted`, `DirectReadOnlyExecutionCompleted`, `DirectReadOnlyExecutionFailed`.

El flujo planificado es Kai → Plan → Preflight → Confirmation → ejecución. La vía directa read-only crea su flow `DirectReadOnly` solo después de encontrar una Tool y de que la policy devuelva `Allow`; los bloqueos de policy no se representan como ejecución.

## Store y fiabilidad

V1 usa `InMemoryExecutionAuditTrail`: store interno, thread-safe, bounded y configurable mediante `ExecutionAudit:MaxEvents`. Retiene eventos FIFO, expulsa primero el más antiguo y entrega snapshots independientes. Reiniciar el proceso elimina el trail; no es durable ni existe endpoint público de audit.

`IExecutionAuditWriter` permite escribir sin acoplar Core o Tools a Infrastructure. `SafeExecutionAuditWriter` encapsula fallos de sink y V1 es fail-open: un fallo de audit no cambia routing, policy, approvals, ejecución de Tools ni el resultado funcional. Un sink durable o de seguridad requeriría una policy futura distinta.

Timestamps y duraciones usan `TimeProvider`, `GetTimestamp()` y `GetElapsedTime()`.

## Concurrencia y límites v1

Los flows concurrentes no comparten `AuditFlowId`; el store sincroniza escrituras y snapshots. La ejecución de Tools no se realiza bajo el lock del store.

No existe todavía un evento terminal directo de cancelación. Si la ejecución read-only devuelve `Cancelled` o lanza `OperationCanceledException`, el gateway no inventa `Failed`; puede quedar Started sin terminal específico. V1 tampoco aporta persistencia, API pública, UI, métricas ni garantías de entrega u orden para sinks futuros asíncronos.
