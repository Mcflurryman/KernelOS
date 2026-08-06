# Seguridad del repositorio público

Se pueden publicar código revisado, documentación, configuraciones de ejemplo y `testdata` sintético. Nunca se publican `.env`, secretos, tokens, credenciales, `appsettings.Local.json`, bases de datos, modelos, memoria, grabaciones, capturas, vídeos, audios, logs, resultados generados, archivos personales ni datos reales de Workspace.

La configuración local debe vivir en archivos ignorados por Git. Antes de cada commit se revisan `git status` y el diff para detectar contenido accidental. Si se sube un secreto, debe revocarse o rotarse inmediatamente, avisar según la política aplicable y eliminarlo del historial mediante el procedimiento de seguridad adecuado: borrarlo en un commit posterior no lo elimina del historial.
