# Multi-task Authorization Preflight

Un plan multi-task no puede ejecutar una Tool inicial y descubrir después que otra tarea requiere confirmación o está denegada. Ese orden produciría efectos laterales antes de conocer la autorización global.

`IPlanExecutor` evalúa todas las tareas mediante `IExecutionPreflight` antes de invocar `IToolRouter`. La decisión agregada conserva la precedencia `Deny > RequireConfirmation > Allow`: una denegación detiene el plan, una confirmación pendiente detiene el plan si no hay denegación y solo un resultado global `Allow` permite comenzar la ejecución.

La autorización es atómica respecto al inicio de ejecución: no hay llamadas a Tools antes de completar el preflight, y las approvals requeridas se consumen únicamente después de que todas las tareas han quedado autorizadas. Esto no hace atómica la ejecución externa. Las Tools se ejecutan secuencialmente y fail-fast; no hay rollback ni transacción distribuida si una tarea posterior falla, se cancela o un efecto externo ya ocurrió.

La Approval Surface crea un único pending opaco para el plan completo. Conserva un snapshot completo de sus tareas, argumentos JSON y valores anidados, de modo que cambios posteriores sobre las colecciones originales no afectan la ejecución. Al aprobar, KernelOS crea approvals internas independientes para cada tarea que lo exige. Cada approval está limitada por `PlanId`, `TaskId` y fingerprint de Tool y argumentos; por tanto no sirve para otra tarea ni para un plan o payload modificado.

Approve nunca ejecuta. Reject es terminal. El pending y sus approvals tienen TTL exclusivo, no se renuevan al consultar y se consumen una sola vez. Las transiciones del pending y la toma del pending aprobado son atómicas: aprobaciones, rechazo y ejecución concurrentes no duplican approvals ni side effects. La cancelación antes o durante preflight evita Tools; durante la ejecución secuencial solo evita las tareas aún no iniciadas.

La superficie soporta snapshots multi-task, aunque el Planner HTTP actual sigue construyendo una única tarea explícita por request. El comportamiento single-task conserva Allow, RequireConfirmation, Deny, expiración y one-shot existentes.
