# Kai Agent Core v1

Kai Agent v1 selecciona una única ruta determinista por request: Chat por defecto o RAG ante una intención documental explícita. Para Chat usa Conversation Context presupuestado; para RAG delega en `IRagPipeline`. No accede a filesystem, Memory, índices, embeddings ni proveedores concretos.

`Planner` permanece como valor contractual, pero devuelve `KAI_PLANNER_UNAVAILABLE`. El `KernelPlanner` actual combina planificación con `IToolRouter.ExecuteAsync`, por lo que Kai v1 no depende de `IPlanner` ni ejecuta Tools. Se requiere separar planificación de ejecución y diseñar confirmación antes de integrarlo.

No hay agent loop, ReAct, ejecución automática, persistencia conversacional, endpoint ni autonomía.
