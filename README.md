# KernelOS

KernelOS es una plataforma personal de IA local diseñada para reunir capacidades de asistencia, automatización y conocimiento bajo control del usuario.

Kai es el asistente que operará sobre la plataforma KernelOS. El proyecto se encuentra en una fase inicial: incluye una solución .NET 8 con una API mínima y un endpoint de estado.

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

## Ejecutar localmente

Desde la raíz del repositorio:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KernelOS.Api
```

La API expone `GET /` y `GET /health`.
