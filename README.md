# KernelOS

KernelOS es una plataforma personal de IA local diseñada para reunir capacidades de asistencia, automatización y conocimiento bajo control del usuario.

Kai es el asistente que opera sobre KernelOS. El proyecto está en una fase inicial y ofrece una API .NET 8 mínima con estado del sistema y conversación local mediante Ollama.

## Objetivos principales

- Ejecutar IA local y privada.
- Construir una base modular, segura y mantenible para futuras integraciones.
- Incorporar progresivamente conversación, voz, memoria y herramientas controladas.
- Mantener al usuario al mando de las acciones con impacto externo o destructivo.

## Estructura del repositorio

```text
src/       KernelOS.Api, KernelOS.Core, KernelOS.Infrastructure y KernelOS.Tools
tests/     Pruebas de KernelOS.Tests
docs/      Arquitectura, decisiones, guías y hoja de ruta
prompts/   Prompts para sistema, agentes y herramientas
scripts/   Automatización de configuración, desarrollo y mantenimiento
deploy/    Recursos de despliegue
config/    Ejemplos de configuración no sensible
assets/    Recursos de marca e interfaz
.github/   Plantillas de colaboración de GitHub
```

Consulta [PROJECT.md](PROJECT.md) para la visión del proyecto y [docs/roadmap/roadmap.md](docs/roadmap/roadmap.md) para sus fases previstas.

## Gobernanza de ingeniería

- [Constitución](CONSTITUTION.md)
- [Instrucciones para agentes](AGENTS.md)
- [Guía de Kai](KAI.md)
- [Principios de ingeniería](docs/ENGINEERING_PRINCIPLES.md)
- [Estándares de código](docs/CODING_STANDARDS.md)
- [Decisiones arquitectónicas](docs/decisions/)

## Ejecutar localmente

Desde la raíz del repositorio:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KernelOS.Api
```

La API expone `GET /`, `GET /health`, `GET /health/ollama` y `POST /chat`.

## Requisitos para chat local

1. Instala Ollama.
2. Descarga un modelo:

   ```powershell
   ollama pull qwen3:8b
   ```

3. Comprueba que Ollama reconoce el modelo:

   ```powershell
   ollama list
   ```

4. Ejecuta KernelOS:

   ```powershell
   dotnet run --project src/KernelOS.Api
   ```

5. Usa la URL que muestre `dotnet run`, ya que el puerto puede variar, para enviar una petición `POST /chat`:

   ```powershell
   Invoke-RestMethod `
     -Uri "http://localhost:PUERTO/chat" `
     -Method Post `
     -ContentType "application/json" `
     -Body '{"message":"Hola Kai"}'
   ```

La configuración de Ollama se encuentra en `appsettings.json` y también admite las variables de entorno `Ollama__BaseUrl`, `Ollama__Model`, `Ollama__TimeoutSeconds` y `Ollama__SystemPrompt`.
