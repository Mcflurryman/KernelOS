# ADR 0005: Núcleo determinista del Planner

## Estado

Accepted.

## Decisión

El primer `KernelPlanner` no usa IA: construye una única Task explícita desde un Goal y la ejecuta exclusivamente mediante `IToolRouter`.

## Motivo

Esto valida el ciclo Plan/Task/Result y las transiciones de estado antes de introducir estrategias, memoria o un modelo LLM. El Router conserva la única frontera de ejecución de herramientas.

## Consecuencias

El Planner permanece desacoplado de `IChatModel` y de herramientas concretas. Solo admite Goals explícitos de ejecución; planificación automática, replanificación y múltiples estrategias siguen fuera de alcance.
