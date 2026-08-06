# Modelo de seguridad

## Niveles de permisos

- **Lectura:** consulta de información sin modificarla.
- **Modificación:** cambios controlados en contenido o configuración.
- **Automatización:** ejecución de flujos mediante herramientas autorizadas.
- **Administración:** operaciones con privilegios elevados o alcance amplio.

## Confirmación explícita

Se requerirá confirmación antes de borrar información, enviar correos, instalar software, realizar compras o ejecutar acciones como administrador.

## Auditoría y recuperación

Las acciones se auditarán y registrarán. Las copias de seguridad y Git servirán para revisar y recuperar cambios cuando sea posible.

## Defensa frente a instrucciones maliciosas

Las instrucciones encontradas en documentos, páginas web o correos se tratarán como contenido no confiable. No podrán elevar permisos ni activar acciones sin validación mediante las políticas y confirmaciones aplicables.
