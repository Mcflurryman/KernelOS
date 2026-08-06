# ADR 0002: Abstracción del proveedor de lenguaje

## Estado

Accepted.

## Decisión

KernelOS utilizará `IChatModel` como abstracción para los proveedores de modelos de lenguaje. Ollama será la primera implementación mediante `OllamaChatModel`.

## Motivo

KernelOS no debe depender directamente de un modelo ni de un proveedor concreto. El contrato en Core permite que la API mantenga el mismo comportamiento cuando se añadan proveedores futuros.

## Consecuencias

La integración específica de Ollama queda confinada a Infrastructure. Cualquier proveedor posterior deberá implementar `IChatModel` sin introducir dependencias de proveedor en Core.
