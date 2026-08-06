# ADR 0007: Filesystem Read Only

## Estado

Accepted.

## Decisión

La primera entrega de Filesystem Capability se limita a `search`, `exists`, `metadata`, `resolve` y `list`. Se expone mediante `POST /filesystem/{operation}`; `operation` solo figura en la URL y el cuerpo contiene `arguments`.

Toda ejecución pasa por `IToolRouter` y `FilesystemTool`. No hay creación, escritura, copia, movimiento, renombrado, eliminación ni Watch.

## Consecuencias

`exists` devuelve HTTP 200 y `exists: false` para rutas autorizadas inexistentes. Errores de argumentos, autorización e inexistencia se traducen a HTTP 400, 403 y 404 respectivamente. La restricción Read Only permite validar seguridad de raíces y contratos antes de introducir operaciones destructivas.
