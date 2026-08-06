# Principios de ingeniería

## Arquitectura inicial

KernelOS comienza como un monolito modular en una única solución y repositorio. Cada módulo tiene responsabilidades delimitadas: Core contiene contratos y modelos independientes; Infrastructure implementa proveedores e integraciones; Api presenta los endpoints; Tools aloja contratos de herramientas. No se crearán proyectos nuevos sin una justificación demostrable.

## Diseño y dependencias

- Se aplicará separación de responsabilidades e inversión de dependencias en los límites reales del sistema.
- Se preferirá composición antes que herencia.
- Las interfaces se crearán solo cuando exista una frontera o una posibilidad real de sustitución, como `IChatModel`.
- Se elegirá la solución más simple antes que abstracciones prematuras.
- La inyección de dependencias compondrá las implementaciones en los bordes de la aplicación.
- No se adoptarán CQRS, MediatR, microservicios u otros patrones por moda; requerirán una necesidad demostrada.

## Ejecución y configuración

- Las operaciones de entrada y salida usarán `async`/`await` y aceptarán `CancellationToken` cuando sea aplicable.
- La configuración será externa al código y se accederá mediante Options en los componentes que la consuman.
- El logging será útil para operar el sistema, pero nunca incluirá secretos ni el contenido completo de conversaciones.
- Los errores se traducirán a resultados controlados en los límites públicos, sin exponer detalles internos.
- El código debe ser observable y comprobable mediante límites claros y dobles de prueba sencillos.

## Calidad y evolución

- Se mantendrán pruebas unitarias e integración proporcionales al cambio.
- Se evitará deuda técnica innecesaria.
- Se conservará la compatibilidad o se documentará explícitamente cualquier ruptura.
- El rendimiento se medirá antes de optimizar.
