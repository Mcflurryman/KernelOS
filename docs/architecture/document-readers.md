# Document Readers

> Estado: implementado para TXT, Markdown, JSON y CSV mediante contratos de Core, Readers registrados, Registry, Router, ReadService, `DocumentTool` y `POST /documents/read`.

Filesystem autoriza la ruta y entrega una referencia; Document Readers interpretan el formato y producen `RawDocument`; Knowledge Core transforma ese resultado. Los Readers no resuelven rutas arbitrarias, no escriben, no ejecutan macros o enlaces, no llaman a LLM y no convierten contenido documental en instrucciones.

Los límites configurables cubren tamaño, caracteres extraídos, filas, columnas y timeout. Los resultados preservan procedencia, hash y warnings seguros; los errores internos no se exponen. El contenido, incluidos prompt injections, es dato no confiable.

PDF, DOCX, XLSX y OCR no están implementados. Añadirlos requerirá mantener la misma frontera de referencias autorizadas, evaluar dependencias y documentar la decisión correspondiente.
