# Instrucciones para agentes

## Secuencia de lectura

Antes de modificar cualquier archivo, todo agente debe leer:

1. `CONSTITUTION.md`
2. `README.md`
3. `PROJECT.md`
4. `AGENTS.md`
5. `KAI.md` cuando actúe como Kai o implemente trabajo de ingeniería
6. `docs/ENGINEERING_PRINCIPLES.md`
7. `docs/CODING_STANDARDS.md`
8. La arquitectura, guías y ADR relevantes.

## Reglas permanentes

- No añadas dependencias sin justificarlas.
- No introduzcas funcionalidades fuera del alcance de la tarea.
- Mantén el código modular y evita dependencias circulares.
- No guardes claves, tokens, contraseñas ni credenciales.
- Evita comandos destructivos y pide confirmación antes de eliminar archivos.
- No modifiques `main` directamente cuando el desarrollo por ramas esté en marcha.
- No hagas commit ni push salvo que se solicite expresamente.
- Muestra siempre un resumen de cambios, así como errores o limitaciones reales.

## Flujo de trabajo

1. Analiza la especificación.
2. Revisa la arquitectura y los ADR relevantes.
3. Identifica los archivos afectados.
4. Implementa el mínimo alcance necesario.
5. Añade o actualiza las pruebas aplicables.
6. Actualiza la documentación.
7. Ejecuta `dotnet restore`, `dotnet build` y `dotnet test` cuando exista código .NET.
8. Revisa los cambios.
9. Informa de las limitaciones.
10. Espera autorización para commit o push.

## Documentación obligatoria

- Código y documentación deben cambiar en la misma tarea cuando el cambio afecte al comportamiento o diseño.
- Actualiza README si cambia el uso.
- Actualiza PROJECT si cambia el alcance o la visión.
- Actualiza arquitectura si cambia el flujo o los módulos.
- Actualiza el roadmap si cambia el estado de una fase.
- Crea un ADR si se toma una decisión arquitectónica relevante.
- Explica expresamente cuando un documento no necesita cambios.

## Definition of Done

Una tarea solo está terminada cuando:

- el alcance está implementado;
- compila;
- las pruebas pasan;
- no hay advertencias nuevas injustificadas;
- la documentación está sincronizada;
- no hay secretos;
- se ha mostrado un resumen de cambios;
- se han declarado limitaciones reales;
- no se ha hecho commit ni push sin permiso.

## Desarrollo mediante ramas

- Comprueba la rama antes de modificar archivos; nunca trabajes directamente sobre `main`.
- Usa una rama `feature/`, `fix/`, `chore/` o `docs/` según el alcance.
- Ejecuta `./scripts/validate.ps1` antes de proponer un Pull Request. La CI no sustituye la validación local.
- No crees Pull Requests, merges, tags ni releases salvo solicitud expresa.
- No declares una tarea terminada si build, tests o CI aplicable fallan.
