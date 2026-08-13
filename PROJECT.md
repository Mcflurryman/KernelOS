# Proyecto KernelOS

KernelOS es una plataforma local-first para construir un asistente personal llamado Kai. Prioriza control de datos, modelos, acciones y permisos sin acoplar decisiones de producto a un proveedor.

## Estado actual

Conversation Memory durable persiste turnos locales en SQLite y UI Foundation ofrece una interfaz Blazor WebAssembly same-origin bajo `/ui` para crear, borrar, leer y enviar conversaciones. La interfaz muestra health real de API y Ollama, pero no amplía la autoridad del backend.

La plataforma también incluye Memory durable local, mantenimiento incremental eventual del índice semántico, retrieval híbrido con degradación controlada, RAG Pipeline, Conversation Context y Kai Agent Core v1. Kai puede orquestar Planner mediante contratos de alto nivel; las Tools y autorizaciones continúan separadas.

## Límites actuales

No hay idempotencia HTTP durable, correlación persistente pending/conversation, indexación semántica de conversaciones, streaming, Markdown, acciones de confirmation desde UI, autenticación multiusuario, scheduler, proveedores cloud ni UI administrativa de Tools, Memory o Knowledge.

El historial durable vive en SQLite. Borradores, citas, metadata de respuesta, confirmations y respuestas parciales no persistidas viven solo en memoria del navegador y pueden desaparecer al recargar.

## Principios

- Local-first y minimización de datos.
- Los modelos no ejecutan acciones directamente: pasan por contratos, Tools y autorizaciones controladas.
- Core no depende de Infrastructure, Api ni Tools; las integraciones quedan en los bordes.
- Seguridad, mantenibilidad y revisión prevalecen sobre velocidad o automatización prematura.

La evolución futura se mantiene en el [Roadmap](docs/roadmap/roadmap.md).
