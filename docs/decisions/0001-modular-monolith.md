# ADR 0001: Monolito modular

## Estado

Accepted.

## Decisión

KernelOS comenzará como un monolito modular en una sola solución y un solo repositorio.

## Motivo

Esta decisión simplifica el desarrollo y el despliegue durante las primeras fases, al tiempo que mantiene límites claros entre módulos.

## Consecuencias

Los componentes podrán separarse en el futuro si las necesidades de escala, despliegue o mantenimiento lo requieren.
