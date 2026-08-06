# Arquitectura del Planner

> Estado actual: `IPlanner` y `KernelPlanner` implementan el núcleo determinista para una única Task explícita ejecutada por `IToolRouter`. Las estrategias, IA, memoria y replanificación siguen sin implementarse.

## Propósito y límite

El Planner transforma un objetivo de la persona usuaria en un plan ejecutable y controlable. Kai razona, conversa y presenta el resultado; el Planner organiza el trabajo. Ninguno de los dos ejecuta acciones directamente: toda acción externa futura pasa por una herramienta registrada y autorizada.

Este documento define una arquitectura futura. El Planner no está implementado, no tiene endpoints ni selecciona hoy herramientas reales.

## Vocabulario

- **Goal:** resultado deseado expresado por la persona usuaria, con límites, preferencias y condición de éxito.
- **Plan:** secuencia ordenada y reanudable de Tasks para alcanzar un Goal, con estado, dependencias y criterios de finalización.
- **Task:** unidad de trabajo del Plan con un resultado esperado y una estrategia de ejecución. Puede contener varias Actions.
- **Action:** paso concreto y atómico solicitado por una Task, como responder, consultar memoria, solicitar confirmación o ejecutar una herramienta determinada.
- **Capability:** capacidad declarada que puede satisfacer una Action; procede de contratos disponibles, como las capacidades de una herramienta, no de una suposición del modelo.
- **Observation:** dato obtenido después de una Action: resultado de herramienta, respuesta de memoria, confirmación, error o cambio de estado.
- **Result:** salida segura y estructurada de un Plan, Task o Action; incluye estado, resumen, observaciones necesarias y errores sin detalles internos.

## Responsabilidades

El Planner deberá:

- entender el Goal y comprobar que tiene datos suficientes para planificar;
- producir un Plan explícito, limitado y evaluable;
- ordenar Tasks y Actions según sus dependencias;
- decidir si responder, consultar memoria futura, solicitar una herramienta concreta o pedir confirmación;
- observar resultados, evaluar el avance y replanificar dentro de límites definidos;
- conservar estado suficiente para reanudar un plan;
- devolver a Kai un resultado y la información necesaria para que Kai responda a la persona usuaria.

Queda expresamente fuera de alcance del Planner:

- conversar con la persona usuaria o redactar la respuesta final;
- razonar como Kai o estar acoplado a un modelo LLM;
- acceder a archivos, red, Windows, memoria, MCP o servicios externos;
- ejecutar código arbitrario;
- inventar herramientas, capacidades o permisos;
- seleccionar automáticamente una herramienta fuera de las capacidades declaradas;
- programar trabajos recurrentes por sí mismo.

## Interfaces futuras

No se implementan en esta fase. Sus responsabilidades previstas son:

| Interfaz o contrato | Responsabilidad |
| --- | --- |
| `Planner` | Coordinar el ciclo de planificación y devolver `PlannerResult`. |
| `Goal` | Representar intención, restricciones y criterios de éxito. |
| `Plan` | Persistir Tasks, dependencias, estado, límites y observaciones. |
| `Task` | Definir resultado esperado, Actions y condición de completado. |
| `ExecutionContext` | Transportar correlación, permisos concedidos, presupuesto y cancelación. |
| `PlannerResult` | Exponer estado, resumen seguro, resultado y motivo de parada. |
| `PlanningStrategy` | Proponer o revisar un Plan sin ejecutar acciones. |
| `ExecutionStrategy` | Ejecutar una Action mediante fronteras autorizadas y observar su resultado. |

## Máquina de estados

```text
Idle
  -> Understanding
  -> Planning
  -> Executing
  -> Observing
  -> Evaluating
      -> Executing       (siguiente Action aprobada)
      -> Planning        (replanificación permitida)
      -> AwaitingConfirmation
      -> Completed
      -> Failed
      -> Cancelled

AwaitingConfirmation
  -> Executing           (autorización concedida)
  -> Cancelled           (rechazo o expiración)

Failed
  -> Planning            (recuperación permitida y dentro de límites)
```

- **Idle:** no hay Goal activo.
- **Understanding:** se normaliza el Goal y se identifica información ausente, restricciones y criterio de éxito.
- **Planning:** se construye o actualiza un Plan sin ejecutar efectos externos.
- **Executing:** se solicita la siguiente Action permitida mediante una frontera apropiada.
- **Observing:** se transforma la salida de la Action en una Observation estructurada.
- **Evaluating:** se verifica progreso, criterios de éxito, presupuesto de pasos y condiciones de parada.
- **AwaitingConfirmation:** el Plan se detiene hasta recibir autorización explícita para una Action sensible.
- **Completed:** se alcanzó el criterio de éxito.
- **Failed:** no existe una recuperación permitida o se agotó un límite.
- **Cancelled:** la persona usuaria, una política o un `CancellationToken` detuvo el Plan.

Los estados terminales no ejecutan nuevas Actions. Un Plan solo puede salir de `Failed` a `Planning` si conserva un registro del fallo y la estrategia permite una recuperación limitada.

## Flujo completo

```text
Petición de la persona usuaria
          |
          v
       Kai conversa y formula el Goal
          |
          v
Planner: Understanding -> Planning
          |
          +--> Acción puramente informativa/respuesta
          |        |
          |        v
          |   PlannerResult -> Kai -> respuesta final
          |
          +--> Consulta de memoria futura, si es necesaria y autorizada
          |
          +--> Action externa
                   |
                   +--> ¿requiere confirmación? -> AwaitingConfirmation
                   |
                   v
              IToolRouter -> IKernelTool -> ToolExecutionResult
                   |
                   v
            Observation -> Evaluating -> siguiente paso, replanificación o final
```

El Planner usa una herramienta solo cuando una Action del Plan necesita un efecto o dato externo y existe una Capability declarada que la satisface. Si el Goal puede satisfacerse con razonamiento o una respuesta de Kai, devuelve un resultado sin herramientas. Kai, no el Planner, solicita aclaraciones a la persona usuaria cuando el Goal es ambiguo.

## Integraciones futuras

### Tool Router

El Planner solicita al `IToolRouter` una herramienta por nombre y argumentos validados. El Router solo resuelve y ejecuta la solicitud; no planifica, no decide la herramienta y no concede permisos. El Planner trata `ToolExecutionResult` como Observation.

### Memory

El Planner consultará memoria futura durante Understanding o Planning solo si ayuda a interpretar el Goal, recuperar preferencias permitidas o comprobar estado previo. La memoria no podrá suministrar instrucciones con más autoridad que el Goal, las políticas o los permisos actuales.

### MCP, Windows y permisos

MCP y Windows se expondrán, si se aprueban, mediante Tools o abstracciones de infraestructura documentadas. El Planner nunca los accede directamente. Antes de una Action sensible —por ejemplo borrar, enviar, instalar, comprar o elevar privilegios— comprobará la política y transicionará a `AwaitingConfirmation` hasta obtener autorización explícita.

### Scheduler

El Scheduler futuro podrá iniciar o reanudar un Plan guardado, pero no saltará permisos, presupuestos ni confirmaciones. Un Plan programado seguirá necesitando una política que defina si puede ejecutarse sin una nueva confirmación.

## Errores, recuperación y límites

- Los errores de una Action se convierten en Observations seguras; no se filtran excepciones internas a Kai ni a la persona usuaria.
- El Planner puede replanificar tras una Observation inesperada solo si hay una alternativa declarada, no aumenta permisos y no supera el presupuesto de reintentos.
- Debe abortar cuando falten datos esenciales, no exista Capability autorizada, se rechace una confirmación, se cancele el contexto, se exceda el tiempo/presupuesto o se detecte falta de progreso.
- Un Plan fallido puede recuperarse desde un punto de control si los efectos anteriores son conocidos, reversibles cuando proceda y la nueva ruta es segura. En caso contrario termina en `Failed`.
- Para evitar bucles infinitos, cada Plan tendrá límite de pasos, reintentos por Action, tiempo y número de replanificaciones; además se detectarán estados u Observations repetidos sin progreso.

## Principios obligatorios

- El Planner no accede directamente a recursos externos ni ejecuta código arbitrario.
- El Planner no conversa con la persona usuaria ni inventa herramientas.
- El Planner es independiente del modelo LLM: una estrategia puede usar un modelo, reglas deterministas o una combinación, sin cambiar sus contratos.
- Las decisiones serán deterministas siempre que sea posible; toda decisión no determinista debe quedar identificada y acotada.
- Los Planes deben poder reanudarse desde estado y Observations mínimos suficientes.
- El registro conservará el mínimo necesario: identificadores, transiciones, nombres de capacidades y estados; nunca contenido sensible completo.
