# UI Foundation

KernelOS.Web es una aplicación Blazor WebAssembly alojada por KernelOS.Api bajo `/ui`. El navegador descarga los assets publicados por la API y usa un `HttpClient` relativo al mismo origen; no existe un segundo backend, proxy de desarrollo, CORS ni llamadas externas desde la UI.

```text
Browser -> /ui -> Blazor WebAssembly -> same-origin HttpClient
        -> /conversations -> Conversation API -> SQLite / Kai
        -> /health, /health/ollama -> Health API -> API / Ollama
```

`Program.cs` sirve los assets de WebAssembly y limita el fallback SPA a `/ui/{*path:nonfile}`. Por ello los deep links de conversación funcionan sin capturar rutas de API como `/conversations` o `/health`. El publish Release de KernelOS.Api contiene los assets de `ui`.

## Clientes y estado

`ConversationApiClient` usa DTOs propios de Web para listar, crear, borrar y leer conversaciones, leer páginas de mensajes y enviar un turno. `HealthApiClient` es un cliente separado para `/health` y `/health/ollama`. Los componentes no reciben modelos de Infrastructure ni acceden directamente a SQLite, Kai, Tools o policy.

El historial durable siempre se reconcilia desde SQLite tras un turn que puede haber alcanzado el backend. No hay persistencia optimista ni reintento automático de POST: ante un fallo de red el estado es incierto, se recargan mensajes y la persona decide si volver a enviar. La lista de conversaciones se ordena por el `UpdatedAt` que devuelve el backend; tanto conversaciones como mensajes usan paginación y deduplicación por identificador.

El estado transitorio de página incluye borrador, envío, warnings, metadata de la última respuesta, citas, confirmation y respuesta Assistant no persistida. No se guarda en localStorage, sessionStorage, IndexedDB ni otra persistencia del navegador. Las citas, metadata y una respuesta parcial no persistida desaparecen al recargar; el historial SQLite es la fuente de verdad.

## UX y límites

La UI usa el lenguaje HUD/cian de KernelOS: KaiCore deriva su aspecto del estado del turn (`Idle`, `Sending`, `Success`, `Partial`, `Confirmation`, `Cancelled`, `Failed` o `Uncertain`). El composer envía con Enter, permite salto de línea con Shift+Enter y bloquea envíos vacíos o simultáneos. Los mensajes son texto plano mostrado como texto, sin renderer Markdown ni HTML raw.

`PartialSuccess` puede mostrar una respuesta temporal marcada como no persistida. `ConfirmationRequired` se recupera por correlación durable y ofrece Approve/Reject explícitos; tras Approve, Execute es una segunda acción explícita. No hay reintentos automáticos y el resultado seguro de ejecución es transitorio. Si el backend se reinicia, Pending desaparece y la UI muestra `Unavailable`. La health strip informa solo API y Ollama mediante endpoints reales, sin polling agresivo.

La base visual respeta `prefers-reduced-motion`, mantiene navegación y controles semánticos y no carga CDN, fuentes remotas, analytics ni telemetría.

## Seguridad operacional

Los perfiles de lanzamiento usan `localhost`, pero el binding final puede cambiar mediante `ASPNETCORE_URLS` o configuración externa. `AllowedHosts=*` no fuerza un binding local. Los despliegues deben fijar explícitamente la interfaz de escucha y la exposición de red; la UI no convierte por sí misma una instalación en localhost-only.

No hay autenticación multiusuario, idempotencia durable, persistencia de Pending/Approval o de estados terminales, streaming, adjuntos, Markdown, herramientas visuales ni administración de Memory/Knowledge en esta fase. La correlación durable conserva sólo identidad UX entre conversación y pending runtime.
