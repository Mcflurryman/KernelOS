# ADR 0003: Sistema de herramientas mediante registro y router

## Estado

Accepted.

## Decisión

KernelOS utilizará `IKernelTool` para describir acciones, `IToolRegistry` para registrar y resolver herramientas por nombre, e `IToolRouter` para ejecutar la herramienta solicitada y devolver un resultado controlado.

## Motivo

Kai y los componentes de orquestación no deben ejecutar acciones directamente. Separar registro y router permite mantener la resolución explícita, validar resultados y añadir autorizaciones futuras sin acoplarlas al modelo ni a herramientas concretas.

## Consecuencias

El router no selecciona herramientas ni implementa lógica de negocio. Las herramientas se registran explícitamente mediante DI, sin reflexión ni Service Locator. La versión inicial solo incluye EchoTool y TimeTool como demostraciones; el tool calling del LLM está fuera de alcance.
