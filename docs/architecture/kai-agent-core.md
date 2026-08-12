# Kai Agent Core v1

Cada request de Kai crea un `AuditFlowId` interno y emite únicamente inicio y ruta seleccionada. El audit no registra mensaje, historial, respuesta ni contexto RAG.

Kai Planner Orchestration permite Planner explícito sin acceso de Kai a router, Tools, approvals o pending stores. `RequiresConfirmation` se devuelve al caller y Deny es terminal.

Kai Agent v1 selecciona una única ruta determinista por request: Chat por defecto o RAG ante una intención documental explícita. Para Chat usa Conversation Context presupuestado; para RAG delega en `IRagPipeline`. No accede a filesystem, Memory, índices, embeddings ni proveedores concretos.

Un `RagStatus.PartialSuccess` es una respuesta RAG utilizable: Kai devuelve `KaiStatus.PartialSuccess`, `ModeUsed=Rag` y conserva respuesta, citas, warnings y modelo. En Auto no hace fallback a Chat para ese estado; el fallback se reserva para `NoContext`. `RagStatus.Cancelled` se traduce a `KaiStatus.Cancelled`, que el endpoint existente devuelve como HTTP 499.

La separación Planner/Authorization/Execution establece una frontera segura, pero no habilita acciones de Kai. `PreferredMode=Planner` continúa respondiendo `KAI_PLANNER_UNAVAILABLE`; Kai no depende de `IPlanner`, `IPlanExecutor`, `IExecutionGate` ni `IToolRouter`. La policy ya existe; antes de integrarlo se requiere Kai Planner Orchestration.

La superficie de confirmación no cambia este límite: Kai no crea, aprueba, rechaza ni ejecuta pending executions.

No hay agent loop, ReAct, ejecución automática, persistencia conversacional, endpoint ni autonomía.
