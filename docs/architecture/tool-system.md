# Tool System

Kai y el builder del Planner no ejecutan acciones directamente. Una solicitud autorizada de ejecución pasa por `IToolRouter`, que resuelve una `IKernelTool` registrada y devuelve un `ToolExecutionResult` controlado.

```text
Plan ya construido → IPlanExecutor → IToolRouter → IToolRegistry → IKernelTool → ToolExecutionResult
```

Core define los contratos. `KernelToolRegistry` registra herramientas explícitamente y detecta nombres duplicados; `KernelToolRouter` resuelve y ejecuta, pero no planifica, concede permisos ni selecciona herramientas por iniciativa propia. `IPlanBuilder` no depende de este sistema: puede representar una tarea, pero no ejecutarla.

Las herramientas registradas son `EchoTool`, `TimeTool`, `FilesystemTool` y `DocumentTool`. Las dos primeras son demostrativas. Filesystem y Document delegan respectivamente en sus capacidades de Infrastructure y mantienen sus propias fronteras de autorización y lectura.

No hay tool calling del LLM, selección automática, consola, Git, MCP, servicios externos ni control de sistema. Las futuras herramientas sensibles requerirán políticas y confirmación explícita; este milestone no las implementa.
