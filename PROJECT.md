# Proyecto KernelOS

## Visión

KernelOS será una plataforma personal de IA local: la base técnica que coordinará modelos, memoria, interfaces y herramientas. Kai será el asistente que interactúe con la persona usuaria y use las capacidades de KernelOS de forma segura. KernelOS es la plataforma; Kai es el asistente construido sobre ella.

El proyecto se desarrollará principalmente con C# y .NET, priorizando el control local, la privacidad, la modularidad y la revisión de cambios.

## Objetivos

- IA local con Ollama.
- Chat y voz.
- Memoria persistente.
- RAG y embeddings.
- Herramientas y servidores MCP.
- Google Drive.
- Telegram.
- GitHub.
- Calendario y correo.
- Screenpipe y memoria visual.
- Control seguro de Windows.
- Generación y edición de Excel, Word y PDF.
- Capacidad futura para modificar y ampliar su propio código.

## Hardware actual

- Intel Core i5-13400F.
- 32 GB de RAM.
- NVIDIA RTX 5070 de 12 GB.

## Modelos inicialmente previstos

- Un modelo principal Qwen de aproximadamente 8B o 14B.
- Un modelo visual Qwen VL de aproximadamente 8B.
- Un modelo de embeddings pequeño.
- Whisper para voz a texto.
- Kokoro o Piper para texto a voz.

## Principios de seguridad

- El modelo nunca controla directamente Windows.
- Las acciones pasan por herramientas controladas.
- Las acciones destructivas requieren confirmación.
- No se almacenan secretos en Git.
- Todo cambio de código debe poder revisarse con Git.

## Estado actual

La solución .NET 8 incluye una API mínima, endpoints de salud, conversación local mediante la abstracción `IChatModel` y Ollama como proveedor actual. También incluye una base de Tool System con registro, router y las herramientas demostrativas EchoTool y TimeTool. No existen todavía memoria, MCP, herramientas de negocio, voz, visión ni interfaz gráfica.
