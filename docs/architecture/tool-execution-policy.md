# Tool Confirmation & Execution Policy

Cuando una ejecución directa llega a `Allow`, el gateway emite un flow de audit interno sin argumentos, rutas ni resultados. Los bloqueos de policy no se registran como ejecución. No existe aún evento terminal directo de cancelación, por lo que un resultado cancelado conserva el comportamiento funcional sin inventar un fallo de audit.

Kai respeta la policy: no autoaprueba, no accede al router y detiene los side effects en `RequiresConfirmation`.

La autorización es una frontera independiente entre construir un plan y ejecutar una Tool:

```text
PlanTask → IExecutionGate → IExecutionPolicy → aprobación one-shot → IToolRouter
```

Cada Tool declara `ToolExecutionMetadata`: solo lectura, efectos laterales, denegación explícita y nivel de riesgo (`Low`, `Medium`, `High`, `Critical`). La policy determinista permite Tools conocidas de solo lectura, exige confirmación para efectos laterales o metadata desconocida, y deniega las explícitamente prohibidas. Un Tool desconocido nunca se permite automáticamente.

Las aprobaciones están ligadas a `PlanId`, `TaskId` y a una huella SHA-256 de `ToolName` y argumentos JSON canónicos (propiedades ordenadas, también en objetos anidados). Expiran según `ExecutionPolicy:ApprovalTtlMinutes` y se consumen una sola vez de forma atómica; `ExpiresAt` es exclusivo y el store elimina entradas expiradas de forma perezosa al crear nuevas aprobaciones. Cambiar una tarea invalida la aprobación. Los números conservan su representación JSON: `1` y `1.0` son argumentos distintos en v1.

`PlanExecutor` hace preflight de todas las tareas antes de llamar al router. La agregación es `Denied > RequiresConfirmation > Authorized`, así que una confirmación faltante o una denegación posterior no permite ejecutar tareas anteriores. Solo después consume las approvals scoped y ejecuta secuencialmente. El resultado conserva todas las tareas originales: las no iniciadas permanecen `Planned`. V1 no reanuda planes parcialmente procesados; volver a ejecutar un plan parcial queda rechazado para evitar repetir tareas ya completadas. No hay rollback de efectos externos. Kai no usa todavía esta capacidad.

La cancelación se comprueba antes de policy, antes de buscar o consumir una aprobación y antes de cada Tool. El consumo atómico sucede antes de despachar al router; una vez consumida no se realiza una comprobación adicional entre gate y despacho, para no perder una aprobación por una cancelación observada artificialmente en ese hueco. Una cancelación concurrente posterior puede ser observada por la Tool y producir resultado cancelado; no existe transacción entre cancelación y efectos externos en v1.

Las rutas HTTP directas (`POST /tools/{name}`, `/documents/read` y `/filesystem/{operation}`) no acceden al router directamente. Usan `IReadOnlyToolExecutionGateway`, que consulta la misma policy con metadata procedente exclusivamente del registro de Tools y solo ejecuta decisiones `Allow`. Por ello siguen habilitadas las capacidades actuales inequívocamente read-only, mientras que cualquier Tool con efectos laterales, denegada, desconocida o con metadata incompleta se rechaza; sus acciones deben pasar por un `Plan` y `IExecutionGate`. No existe todavía una API pública para crear aprobaciones: una interacción humana controlada futura deberá crear el scope, fingerprint y timestamps dentro de KernelOS.

La superficie de confirmación HTTP definida en `execution-approval-surface.md` proporciona ahora esa interacción controlada sin permitir que el cliente cree approvals arbitrarias.
