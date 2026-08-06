# ADR 0006: Filesystem Capability como frontera única

## Estado

Accepted.

## Decisión

Filesystem Capability gestionará archivos, rutas, metadatos y operaciones de filesystem. Document Readers interpretarán contenido; Knowledge representará información extraída; el Planner organizará objetivos y Kai razonará y conversará.

## Motivo

Separar estas responsabilidades permite aplicar permisos, confirmaciones, validación de rutas y auditoría en un único límite sin acoplar el Planner ni el modelo a `System.IO` o a proveedores concretos.

## Consecuencias

Las futuras herramientas de archivos delegarán en la Capability. Los lectores de documentos recibirán referencias autorizadas, no rutas arbitrarias. Proveedores como NAS, Drive o SFTP podrán añadirse como adaptadores sin modificar Planner.
