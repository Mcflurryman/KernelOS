# ADR 0035: UI Foundation

## Estado

Accepted.

## Contexto

KernelOS ya exponía conversaciones durables y health por HTTP, pero no tenía una interfaz visible. La interfaz debía seguir el modelo local-first sin añadir Node, un segundo host, CORS o duplicar la autoridad del backend.

## Decisión

Se adopta Blazor WebAssembly en `KernelOS.Web`, alojado por `KernelOS.Api` bajo `/ui` y con fallback limitado a ese prefijo. La UI usa clientes HTTP manuales y tipados del mismo origen para Conversation y Health.

El estado de composición, envío, confirmation, citas y respuestas parciales vive solo en memoria del componente. SQLite sigue siendo la fuente de verdad del historial. Los POST de turn no se reintentan automáticamente porque aún no existe idempotencia durable. Los mensajes v1 son texto plano; no se implementan streaming, Markdown, acciones de confirmation, autenticación multiusuario ni UI de Tools.

## Consecuencias

La API publica una UI local sin CORS ni infraestructura de frontend adicional, y los deep links sobreviven refresh. La experiencia distingue durable de transitorio y evita duplicar turns tras una incertidumbre de red.

La UI depende de los endpoints existentes y no sustituye sus controles de policy. Pending executions siguen siendo runtime-only y no se pueden continuar desde una conversación recargada. La exposición de red depende de la configuración de hosting externa: los launch profiles locales no garantizan un binding localhost en todos los despliegues.
