# Principios de ingeniería

## Arquitectura inicial

KernelOS comienza como un monolito modular en una única solución y repositorio. Core contiene contratos y modelos independientes; Infrastructure implementa proveedores e integraciones; Api presenta endpoints; Tools aloja herramientas. No se crean proyectos nuevos sin una justificación demostrable.

## Diseño y dependencias

- Se aplican separación de responsabilidades e inversión de dependencias en fronteras reales.
- Se prefiere composición a herencia y la solución más simple a abstracciones prematuras.
- La inyección de dependencias compone implementaciones en los bordes.
- No se adoptan patrones por moda sin necesidad demostrada.

## Ejecución y configuración

- Las operaciones de entrada y salida usan `async`/`await` y `CancellationToken` cuando aplica.
- La configuración es externa y se consume mediante Options.
- Los logs no incluyen secretos ni conversaciones completas.
- Los errores se traducen a resultados controlados sin detalles internos.

## Calidad y evolución

- Se mantienen pruebas proporcionales al cambio y se documentan rupturas de compatibilidad.
- La validación local y CI ejecutan los mismos restore, build y tests de forma reproducible.
- Los cambios se desarrollan en ramas cortas, de alcance coherente y revisables mediante Pull Request.
- Se prefieren cambios pequeños y diffs fáciles de revisar antes que acumulaciones no relacionadas.
- El rendimiento se mide antes de optimizar.
