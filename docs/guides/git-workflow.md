# Flujo de trabajo Git y Pull Requests

## Ramas permitidas

- `main`: código estable.
- `feature/<descripcion>`: funcionalidad coherente.
- `fix/<descripcion>`: corrección acotada.
- `chore/<descripcion>`: mantenimiento o ingeniería.
- `docs/<descripcion>`: documentación.

No se trabaja directamente en `main`. Cada rama nace desde `main` actualizado, mantiene un alcance único y no mezcla refactorizaciones no relacionadas. Antes de abrir un PR deben estar correctos build, tests y documentación. No se hace merge si KernelOS CI falla. Se prefiere squash merge, se elimina la rama tras el merge y no se hace force push sobre `main`.

## Flujo

```text
git switch main
git pull
git switch -c feature/nombre
...trabajo...
powershell -ExecutionPolicy Bypass -File .\scripts\validate.ps1
git add .
git commit
git push -u origin feature/nombre
crear Pull Request
esperar CI y revisar
squash merge
eliminar rama
git switch main
git pull
```

Los commits locales deben ser claros y revisables. No se almacenan secretos ni se aprueban automáticamente cambios sensibles.

El bypass de PowerShell se limita a ese proceso y no cambia permanentemente la política del sistema. Cuando PowerShell 7 esté disponible, puede usarse `pwsh -File .\scripts\validate.ps1`.

Un **commit** registra cambios locales; un **push** los publica en el remoto; un **Pull Request** solicita revisión e integración de una rama; un **merge** integra historial; un **squash merge** integra el resultado como un único commit legible.

## Configuración manual recomendada de `main`

Estas son reglas documentadas, no configuración aplicada automáticamente en GitHub. Se recomienda exigir Pull Request, aprobación antes de merge cuando sea posible, éxito de KernelOS CI, resolución de conversaciones, bloqueo de force pushes y borrado de `main`, y permitir squash merge.
