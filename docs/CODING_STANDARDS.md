# Estándares de código

## C#

- Se usa `PascalCase` para tipos, propiedades y métodos; `camelCase` para parámetros y variables locales; las interfaces llevan el prefijo `I`.
- Los nombres serán descriptivos y evitarán abreviaturas ambiguas.
- Nullable e implicit usings permanecen habilitados.
- Cada clase tendrá una responsabilidad clara. Los métodos serán pequeños cuando ayude a entenderlos, sin fragmentarlos artificialmente.
- Las validaciones se harán con guard clauses.
- No se usará el operador null-forgiving (`!`) salvo justificación explícita.
- No se bloqueará código asíncrono con `.Result` ni `.Wait()`; se propagará `CancellationToken` en operaciones que puedan cancelarse.
- Se usarán records para contratos inmutables cuando tenga sentido.

## Configuración, errores y observabilidad

- La configuración se representará mediante Options.
- No se registrará el contenido completo de conversaciones.
- Los errores se tratarán de forma uniforme y controlada en los límites del sistema.
- Se evitarán constantes mágicas: los valores reutilizables tendrán nombre, y los valores configurables vivirán en configuración externa.
- Los comentarios explicarán decisiones o motivos, no lo evidente en el código.

## Pruebas

- Los nombres de prueba describirán escenario y resultado esperado.
- Las pruebas seguirán, cuando aplique, la estructura Arrange, Act, Assert.
- Las pruebas no dependerán de servicios locales o externos salvo que se trate explícitamente de una prueba de integración de ese servicio.
