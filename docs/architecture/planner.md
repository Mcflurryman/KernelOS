# Planner

> Estado: `IPlanner` y `KernelPlanner` están implementados como núcleo determinista. Reciben un Goal explícito, construyen una única Task y la ejecutan únicamente a través de `IToolRouter`.

El Planner organiza ejecución; no conversa, no razona como Kai, no accede a recursos externos y no inventa Tools, permisos o capacidades. La API expone `POST /planner/execute` para este flujo acotado.

El núcleo actual preserva separación entre Goal, Plan, Task, Action y resultado controlado. No implementa planificación automática, múltiples Tasks, estrategias basadas en LLM, replanificación, persistencia/reanudación, memoria conversacional, Scheduler ni políticas completas de confirmación. Estas capas futuras deberán mantener la misma frontera: toda acción externa pasa por Tools autorizadas.
