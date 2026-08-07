# Kai Agent Core v1

Kai Agent v1 selecciona una única ruta determinista por request: Chat por defecto o RAG ante una intención documental explícita. Para Chat usa Conversation Context presupuestado; para RAG delega en `IRagPipeline`. No accede a filesystem, Memory, índices, embeddings ni proveedores concretos.

La separación Planner/Execution ya establece una frontera segura, pero no habilita acciones de Kai. `PreferredMode=Planner` continúa respondiendo `KAI_PLANNER_UNAVAILABLE`; Kai no depende de `IPlanner`, `IPlanExecutor` ni `IToolRouter`. Antes de integrarlo se requiere el milestone Tool Confirmation & Execution Policy.

No hay agent loop, ReAct, ejecución automática, persistencia conversacional, endpoint ni autonomía.
