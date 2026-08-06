# Proyecto KernelOS

KernelOS es una plataforma personal de IA local; Kai es el asistente que opera sobre ella. Se desarrolla en C# y .NET, priorizando privacidad, modularidad, seguridad y revisión de cambios.

## Estado actual

La solución .NET 8 incluye API mínima, salud, chat mediante `IChatModel` con Ollama como proveedor actual, Tool System, Planner determinista inicial y Filesystem Capability Read Only. Filesystem usa `FilesystemTool` e `IToolRouter` para `search`, `exists`, `metadata`, `resolve` y `list`, con raices configuradas y sin operaciones de escritura.

No están implementados memoria, MCP, herramientas de negocio, escritura de filesystem, proveedores remotos, voz, visión ni interfaz gráfica.

## Principios

- El modelo no ejecuta acciones directamente; solicita herramientas controladas.
- Las acciones sensibles o destructivas requieren autorización explícita.
- No se almacenan secretos en Git.
- Core no depende de Infrastructure, API ni Tools.
