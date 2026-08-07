# Filesystem Capability

> Estado: Filesystem Read Only completada y validada manualmente. Operaciones: `search`, `exists`, `metadata`, `resolve` y `list`.

## Flujo y responsabilidades

```text
HTTP API -> IToolRouter -> FilesystemTool -> IFilesystemCapability
                                              |
                                              +-> LocalFilesystemCapability
                                              +-> FilesystemRootResolver
```

`KernelOS.Api` recibe `POST /filesystem/{operation}` y no accede directamente a `System.IO`. `IToolRouter` ejecuta la herramienta registrada. `FilesystemTool` traduce el resultado al contrato de herramientas. `LocalFilesystemCapability` implementa las cinco operaciones locales de solo lectura. `FilesystemRootResolver` resuelve y autoriza rutas antes de cualquier acceso.

La operación pertenece exclusivamente a la URL. El cuerpo contiene `arguments`, con `path` obligatorio; `pattern`, `recursive` y `maxResults` son opcionales para `search` y `list`.

## Raíces y seguridad

Las raíces se configuran mediante `Filesystem:AllowedRoots`. `Workspace` se resuelve ascendiendo desde el content root hasta `KernelOS.sln`, por lo que apunta a la raíz del repositorio. Los aliases `Desktop` y `Documents` son opcionales: solo se habilitan si la carpeta especial del sistema devuelve una ruta absoluta válida y no vacía; en entornos headless no disponibles se rechazan. También se aceptan rutas absolutas dentro de una raíz permitida.

La resolución normaliza rutas y exige coincidencia en un límite de segmento. Así se rechazan aliases desconocidos, rutas relativas sin alias, escapes con `..` y prefijos parecidos. La respuesta no expone detalles de excepciones internas.

## Semántica y códigos HTTP

| Resultado | HTTP |
| --- | --- |
| Operación correcta | 200 |
| Operación o argumentos inválidos | 400 |
| Ruta fuera de `AllowedRoots` | 403 |
| Entrada o directorio requerido inexistente | 404 |

`exists` es una consulta: una ruta autorizada inexistente devuelve 200 con `exists: false`. `metadata` devuelve nombre, ruta absoluta, tamaño, fechas, extensión y tipo; `list` y `search` devuelven entradas ordenadas por ruta y respetan patrón, recursividad y límite.

## Límites actuales

No hay lectura de contenido, interpretación de documentos, creación, escritura, copia, movimiento, renombrado, eliminación, Watch, memoria, permisos de usuario ni proveedores remotos. El Planner no accede al disco: cuando integre esta capacidad, seguirá pasando por el Tool Router.
