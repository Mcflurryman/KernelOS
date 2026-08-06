# Constitución de KernelOS

Este documento contiene las reglas obligatorias e invariables para KernelOS, Kai y cualquier agente o persona que contribuya al repositorio.

## Identidad y prioridades

- KernelOS es la plataforma personal de IA local; Kai es el asistente que opera sobre ella.
- La seguridad, la mantenibilidad y el control de la persona usuaria tienen prioridad sobre la velocidad de entrega.
- La documentación debe describir el estado real del sistema; las capacidades futuras no se presentarán como implementadas.

## Seguridad y control

- Un modelo nunca ejecuta acciones directamente: las solicita mediante contratos y herramientas controladas.
- Los componentes de orquestación y los modelos no acceden directamente a recursos externos. Toda interacción de ese tipo pasa por una herramienta autorizada o por una abstracción de infraestructura expresamente documentada; `IChatModel` es una de esas fronteras de infraestructura.
- Toda acción destructiva o sensible requiere autorización explícita de la persona usuaria.
- Nunca se almacenarán en Git secretos, tokens, claves, contraseñas ni datos personales.
- No se ejecutarán comandos destructivos sin confirmación previa.
- Git debe permitir revisar y revertir los cambios de forma fiable.

## Arquitectura y calidad

- `KernelOS.Core` no puede depender de `KernelOS.Infrastructure`, `KernelOS.Api` ni `KernelOS.Tools`.
- Se evitarán las dependencias circulares.
- Todo cambio debe compilar.
- Toda funcionalidad incluirá pruebas cuando proceda.
- Toda modificación relevante actualizará simultáneamente la documentación.
- Toda decisión arquitectónica relevante se documentará mediante un ADR.
- No se introducirán funcionalidades fuera del alcance solicitado.
- No se ocultarán errores, limitaciones ni incertidumbres.

## Flujo de cambios

- No se modificará `main` directamente cuando el trabajo con ramas esté establecido.
- No se hará commit ni push salvo solicitud expresa.
