# ADR 0029: Execution Audit Trail

## Status

Accepted.

## Context

KernelOS ya puede planificar, autorizar, confirmar y ejecutar acciones, pero faltaba una forma segura de reconstruir las decisiones y ejecuciones realizadas. `ILogger` no sustituye un audit trail de dominio estructurado y correlacionado.

## Decision

Se introducen `AuditFlowId`, `ExecutionAuditContext` y `AuditEvent` estructurado. `IExecutionAuditTrail` conserva los eventos y `IExecutionAuditWriter` permite escribir sin depender de Infrastructure. V1 implementa un store in-memory bounded y `SafeExecutionAuditWriter` fail-open.

Los eventos no contienen payload arbitrario: no incluyen argumentos, resultados, prompts ni excepciones. Cada transición tiene propietario único, las duraciones y timestamps usan `TimeProvider`, y V1 no expone API pública de audit.

## Consequences

KernelOS obtiene trazabilidad interna correlacionada para Kai, Planner, approvals, executor y la ejecución read-only directa. Se acepta más instrumentación y el coste de mantener ownership explícito.

No hay persistencia, garantía durable, UI ni autoridad de seguridad. Futuros sinks pueden implementarse detrás de los contratos sin cambiar Kai o Planner, pero una política durable o security-grade requerirá una decisión posterior.
