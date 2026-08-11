# ADR 0027: Composition Modularization

## Estado

Accepted.

## Contexto

`Program.cs` y `ServiceCollectionExtensions.cs` concentraban mappings HTTP y registros de múltiples dominios. `PlannerTests.cs` reunía pruebas de core, executor, endpoints Planner y Kai, y approval flow.

## Decisión

Los endpoint mappings se organizan por dominio y `Program.cs` queda como composition root. Los contratos HTTP específicos permanecen separados en API Contracts.

`AddInfrastructure` conserva su papel de fachada pública, mientras los registros internos se dividen por dominio. Las pruebas de Planner se separan por responsabilidad, con helpers pequeños para datos y fakes compartidos.

## Consecuencias

La composición es más fácil de revisar, mantiene menor acoplamiento y permite añadir capacidades futuras sin ampliar un único archivo central. No cambian Core, contratos de dominio, rutas, políticas de seguridad, lifetimes ni comportamiento. Se acepta el coste de más archivos y un boilerplate reducido para hacer explícitas las fronteras de composición.
