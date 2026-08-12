# Execution Approval Surface

El pending snapshot conserva el `ExecutionAuditContext` del Plan para correlacionar creación, decisión y ejecución sin incluir el snapshot ni argumentos en eventos de audit.

Kai puede propagar `PendingExecutionId` y la confirmation pública, pero no puede aprobar ni ejecutar el pending aprobado.

La confirmación humana es una operación independiente de ejecutar una Tool.

```text
POST /planner/execute -> PendingExecutionId + confirmation segura
GET /execution/confirmations/{id} -> consulta
POST /execution/confirmations/{id} -> Approve | Reject
POST /execution/pending/{id}/execute -> IPlanExecutor -> Gate -> ToolRouter
```

`PendingExecutionId` es opaco y el servidor conserva un snapshot inmutable del Plan completo; el cliente nunca reenvía Tool, argumentos, riesgo, fingerprint, TTL ni approval. La solicitud pública muestra nombre, descripción, riesgo, razón de policy y un resumen conservador que no incluye argumentos.

Approve crea una approval one-shot por cada tarea que la requiere, con fingerprint calculado desde el snapshot y scope de plan y tarea. Reject no crea approvals. Aprobar no ejecuta. El pending y las approvals usan el TTL de `ExecutionPolicy`; el store limpia entradas expiradas perezosamente y solo una operación concurrente puede aprobar o tomar un pending aprobado para ejecución.

El Planner HTTP sigue construyendo una tarea por request; no afirma crear planes multi-task desde lenguaje natural. La superficie conserva y aprueba snapshots multi-task ya construidos. No implementa reanudación de planes parcialmente ejecutados, persistencia ni una UI. Kai no accede a estos contratos.
