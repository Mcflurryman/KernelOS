# RAG Pipeline Core

> Deuda conocida: `ProviderUnavailable` de Hybrid no degrada todavía RAG a lexical-only.

RAG Pipeline orquesta `IHybridSearchEngine`, `IContextBuilder`, `IRagPromptBuilder` e `IChatModel`. No conoce filesystem, Readers, índices vectoriales, embeddings, Tools ni Planner. El flujo es query → retrieval → ContextPack → prompt determinista → modelo → respuesta.

Si retrieval no devuelve resultados o Context Builder no produce items, devuelve `NoContext` y no llama al modelo. Así se diferencia una respuesta basada en conocimiento recuperado de una respuesta general del modelo.

El prompt contiene una instrucción controlada por KernelOS y fragmentos delimitados como datos no confiables. Instruye a ignorar órdenes incluidas en los documentos, no ejecutar acciones, basarse en el contexto y citar IDs disponibles como `[C1]`. Las citas de la respuesta son fuentes disponibles del `ContextPack`; no se afirma que el modelo las haya usado ni se parsea su salida.

Actualmente el chat configurado es local con Ollama, por lo que el contexto se procesa localmente. Un futuro `IChatModel` remoto constituirá una frontera de privacidad y requerirá configuración y política explícitas. RAG Pipeline sigue independiente de Conversation Context: no acepta historial ni se modifica `IRagPipeline`. Un futuro Kai Agent combinará `CurrentUserMessage`, `ConversationContextPack`, RAG y, según su política, Chat/Planner/Tools. No hay endpoint ni Kai Agent.
