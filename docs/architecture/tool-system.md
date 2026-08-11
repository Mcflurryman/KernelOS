# Tool System

Kai y el builder del Planner no ejecutan acciones directamente. Una solicitud autorizada de ejecución pasa por `IToolRouter`, que resuelve una `IKernelTool` registrada y devuelve un `ToolExecutionResult` controlado.

```text
Plan ya construido → IPlanExecutor → IExecutionGate → IExecutionPolicy / Approval Store → IToolRouter → IToolRegistry → IKernelTool → ToolExecutionResult
```

Core define los contratos. Cada Tool expone metadata declarativa de ejecución, que permite a la policy decidir sin inferir riesgo desde strings. `KernelToolRegistry` registra herramientas explícitamente y detecta nombres duplicados; `KernelToolRouter` resuelve y ejecuta, pero no planifica ni concede permisos. `IPlanBuilder` no depende de este sistema: puede representar una tarea, pero no ejecutarla.

Las herramientas registradas son `EchoTool`, `TimeTool`, `FilesystemTool` y `DocumentTool`. Las dos primeras son demostrativas. Filesystem y Document delegan respectivamente en sus capacidades de Infrastructure y mantienen sus propias fronteras de autorización y lectura.

Las acciones sensibles se confirman mediante la superficie de approvals; la API directa continúa limitada a la gateway read-only.

No hay tool calling del LLM, selección automática, consola, Git, MCP, servicios externos ni control de sistema. Las futuras herramientas sensibles requerirán políticas y confirmación explícita; este milestone no las implementa.
