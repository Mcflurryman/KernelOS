# ADR 0009: Document Readers

## Estado

Accepted.

## Decisión

Filesystem, Document Readers y Knowledge permanecen separados. Los Readers usarán una representación común `RawDocument`, se registrarán explícitamente mediante DI y tratarán el contenido documental como no confiable. Cada fragmento conservará procedencia segura.

## Motivación

La separación desacopla formatos y librerías, evita parsers dispersos, permite memoria y RAG futuros, mejora la seguridad frente a contenido activo o prompt injection y permite pruebas reproducibles por formato.

## Consecuencias

Se incorpora una frontera adicional con contratos comunes, Registry y Router. Los resultados podrán ser parciales y deberán declarar advertencias, límites y procedencia. La selección de bibliotecas para PDF, DOCX, XLSX y CSV queda pendiente de evaluación; no se implementan Readers en esta decisión.
