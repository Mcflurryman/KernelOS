# Ollama Embeddings Provider

`OllamaEmbeddingGenerator` implementa `IEmbeddingGenerator` en Infrastructure y usa `POST /api/embed` con un `HttpClient` nombrado independiente de Chat. Envía `embeddinggemma` y texto normalizado, o un array para batch real; no usa `/api/embeddings` ni reutiliza DTOs de Chat.

El proveedor solo se registra si `Embeddings:Provider` es `ollama`. Provider vacío o `none` deja embeddings sin registrar y permite arrancar; un provider explícito desconocido falla la validación de configuración. El modelo configurado debe estar instalado por la persona usuaria: KernelOS nunca ejecuta `ollama pull`.

Cada respuesta debe contener exactamente un vector por input, en el mismo orden, con `ExpectedDimensions` valores finitos. Para `embeddinggemma`, la configuración inicial usa 768 como dimensión esperada; cualquier discrepancia falla de forma segura, sin truncar, rellenar ni proyectar. Ollama documenta que `/api/embed` acepta batch y que las dimensiones dependen del modelo.

El proveedor inicial es local (`localhost`); no registra texto ni vectores completos. Errores HTTP, modelo ausente, timeout o JSON inválido devuelven `Failed` seguro. Batch no inventa resultados parciales si una única llamada de Ollama falla globalmente.
