# ADR 0008: Resolución de raíces de filesystem

## Estado

Accepted.

## Decisión

Las rutas se autorizan solo después de resolver aliases configurados (`Workspace`, `Desktop`, `Documents`) o una ruta absoluta perteneciente a `Filesystem:AllowedRoots`.

`Workspace` se busca ascendiendo desde el content root hasta encontrar `KernelOS.sln`; por tanto representa la raíz controlada del repositorio. El resolver normaliza la ruta y requiere coincidencia de un segmento completo con la raíz autorizada.

## Consecuencias

Se rechazan aliases desconocidos, rutas relativas sin alias, escapes con `..` y prefijos parecidos a una raíz autorizada. Las rutas absolutas se normalizan antes de la misma comprobación. Ninguna ruta no autorizada llega a la implementación local de filesystem.
