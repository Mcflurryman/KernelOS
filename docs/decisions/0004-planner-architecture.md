# ADR 0004: Planner como componente independiente

## Estado

Accepted.

## Decisión

KernelOS tendrá un Planner independiente que transforma Goals en Planes y coordina Tasks y Actions. Kai no planificará directamente: conserva la conversación y el razonamiento, mientras el Planner organiza la ejecución bajo límites y estados explícitos.

## Motivo

Separar la planificación de Kai evita acoplar el ciclo de ejecución a un modelo LLM concreto, hace visibles los permisos, confirmaciones, reintentos y puntos de reanudación, y permite probar el comportamiento de forma determinista.

## Consecuencias

El Planner dependerá de contratos, no de herramientas o proveedores concretos. El Tool Router seguirá resolviendo y ejecutando solicitudes sin decidir qué herramienta usar. Un modelo LLM futuro podrá participar en una `PlanningStrategy`, pero podrá sustituirse sin cambiar el Plan, los estados ni las fronteras de seguridad.
