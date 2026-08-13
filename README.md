# KernelOS

KernelOS es una plataforma personal de IA local en .NET 8. Incluye conversaciones persistentes en SQLite y una UI Blazor WebAssembly alojada por la propia API bajo `/ui`.

Kai conversa localmente con Ollama, puede aplicar RAG interno y orquestar Planner bajo policy. Las acciones con efectos laterales requieren confirmación explícita; Kai no autoaprueba ni ejecuta fuera del executor.

## Estado actual

Están implementados chat local mediante Ollama, Tool System, Planner con planificación, autorización y ejecución separadas, Filesystem Read Only, Document Readers, Knowledge Core, Memory durable local, retrieval híbrido, Context Builder, RAG Pipeline, Conversation Context, Kai Agent Core y Conversation Memory durable.

UI Foundation ofrece conversaciones bajo `/ui`: crear, borrar, navegar mediante deep links, leer historial, enviar turnos, ver estados de Kai y health de API/Ollama. La UI no tiene streaming, Markdown, acciones de confirmation, autenticación, adjuntos ni almacenamiento de borradores en el navegador.

## Uso local

Se necesita .NET 8. Para chat o embeddings se necesita Ollama local y los modelos configurados; KernelOS nunca descarga modelos automáticamente.

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KernelOS.Api
```

Con la API en ejecución, abre `http://localhost:5266/ui`. Los perfiles de lanzamiento usan localhost, pero la exposición final depende de la configuración externa de hosting.

La validación reproducible es:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate.ps1
```

## Límites

- Las conversaciones se guardan localmente en SQLite sin cifrado de aplicación; no hay idempotencia durable ni correlación persistente de pending executions.
- La UI conserva solo estado transitorio en memoria. SQLite es la fuente de verdad del historial.
- Vector Index y embeddings son derivados en memoria; tras restart o estado `Dirty` requieren rebuild explícito.
- No existen Scheduler, automatización de Windows, MCP, integraciones cloud, OCR, voz, UI de Tools/Memory/Knowledge ni producto multiusuario.

## Documentación

- [Visión del proyecto](PROJECT.md)
- [Arquitectura actual](docs/architecture/overview.md)
- [UI Foundation](docs/architecture/ui-foundation.md)
- [Persistent Conversation Memory](docs/architecture/persistent-conversation-memory.md)
- [Roadmap](docs/roadmap/roadmap.md)
- [Decisiones arquitectónicas](docs/decisions/)
- [Changelog](CHANGELOG.md)
