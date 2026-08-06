# ADR 0010: Document Readers Core

## Estado

Accepted.

## Decisión

KernelOS implementa contratos comunes, Readers explícitos para TXT, Markdown, JSON y CSV, Registry y Router por DI, `DocumentTool` y `POST /documents/read`. No se usan paquetes externos. Los Readers preservan procedencia, límites y hash SHA-256, y tratan todo contenido como no confiable.

## Consecuencias

Filesystem sigue autorizando rutas; Readers no resuelven ni escriben archivos. El Router transforma excepciones inesperadas en resultados seguros y el Tool no expone referencias internas. PDF, DOCX, XLSX, OCR, Knowledge y Memory permanecen pendientes.

`TooLarge` usa un estado de herramienta explícito para mapear HTTP 413. CSV incompleto no se repara silenciosamente: usa el warning estable `CSV_UNCLOSED_QUOTED_FIELD` y aplica la política de resultados parciales.
