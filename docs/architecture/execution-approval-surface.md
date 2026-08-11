# Execution Approval Surface

La confirmación humana es una operación independiente de ejecutar una Tool.

```text
POST /planner/execute -> PendingExecutionId + confirmation segura
GET /execution/confirmations/{id} -> consulta
POST /execution/confirmations/{id} -> Approve | Reject
POST /execution/pending/{id}/execute -> IPlanExecutor -> Gate -> ToolRouter
```

`PendingExecutionId` es opaco y el servidor conserva un snapshot inmutable del Plan; el cliente nunca reenvía Tool, argumentos, riesgo, fingerprint, TTL ni approval. La solicitud pública muestra nombre, descripción, riesgo, razón de policy y un resumen conservador que no incluye argumentos.

Approve crea una approval one-shot por medio de `IExecutionApprovalStore`, con fingerprint calculado desde el snapshot. Reject no crea approval. Aprobar no ejecuta. El pending y la approval usan el TTL de `ExecutionPolicy`; el store limpia entradas expiradas perezosamente y solo una operación concurrente puede aprobar o tomar un pending aprobado para ejecución.

V1 conserva el Planner HTTP de una tarea y el servicio rechaza explícitamente planes multi-tarea. No implementa reanudación de planes parcialmente ejecutados ni una UI; las capacidades multi-tarea requerirán preflight de autorización antes de exponerlas mediante esta superficie. Kai no accede a estos contratos.
