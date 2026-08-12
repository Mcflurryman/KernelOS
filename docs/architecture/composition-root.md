# Composition Root

`src/KernelOS.Api/Program.cs` es el composition root de KernelOS. Se limita a configurar logging y serialización, registrar `AddInfrastructure` y `AddKernelTools`, instalar el middleware de errores y mapear los módulos HTTP antes de iniciar la aplicación.

## API modular

Los endpoints viven en `src/KernelOS.Api/EndpointMappings` y se agrupan por responsabilidad: Health, Chat, Kai, Tools, Planner, Execution, Documents y Filesystem. Los contratos específicos de HTTP viven en `src/KernelOS.Api/Contracts`; no duplican ni trasladan los modelos de Core.

La modularización no modifica rutas, verbos, payloads ni mappings de estado. La API pública de Tools, Documents y Filesystem continúa pasando por `IReadOnlyToolExecutionGateway`; no accede directamente a `IToolRouter`.

## Infrastructure modular

`services.AddInfrastructure(configuration)` permanece como la única fachada pública para consumidores. Internamente coordina módulos por dominio para Chat, Execution, Planning, Filesystem, Documents, Knowledge, Persistence, Memory, Retrieval, Context, RAG, Conversation, Kai y Embeddings.

Cada módulo conserva sus Options, validaciones, valores por defecto, lifetimes y registros existentes. Los clientes HTTP de Ollama se configuran mediante `IHttpClientFactory`; la generación de embeddings sigue registrándose únicamente cuando el provider configurado es `ollama`.

## Límites preservados

Core no depende de API ni de Infrastructure. `Program.cs` no contiene lógica HTTP de dominio y `AddInfrastructure` no introduce Service Locator. La ejecución pública read-only continúa atravesando el gateway y los side effects siguen la ruta PlanExecutor, ExecutionGate y policy. Kai no depende directamente de `IToolRouter`, approval stores ni pending stores.
