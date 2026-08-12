# Knowledge Core

> Estado: implementado como transformación interna determinista; no tiene endpoint ni herramienta pública.

Knowledge Core transforma un `RawDocument` ya leído en un `KnowledgeDocument` con `KnowledgeItem` homogéneos y consultables. El flujo disponible es `Filesystem -> Document Readers -> RawDocument -> Knowledge Core -> Memory Core -> Search Engine Core`; Planner y Kai son consumidores futuros.

## Fronteras y responsabilidades

Knowledge no lee archivos, no usa `System.IO`, no conoce rutas físicas ni ejecuta Readers. Tampoco consulta LLM, genera embeddings, persiste datos, decide herramientas ni convierte contenido en acciones. `IKnowledgeBuilder` recibe solamente `RawDocument` y una instantánea de opciones, y devuelve un resultado seguro sin excepciones expuestas.

Cada item conserva `DocumentId`, referencia segura, referencia mostrable y un localizador de sección, línea, columna, fila, ruta JSON o descripción. No existe `InternalReference` en los modelos de Knowledge y no se publica una ruta absoluta. El contenido sigue siendo no confiable: una inyección de prompt permanece como dato.

## Transformación

Secciones se mapean de forma directa: párrafos y bloques de texto a `Text`, títulos a `Heading`, listas a `List`, código a `Code` y valores JSON a `JsonValue`. Las tablas se representan como JSON serializable con nombre, cabeceras, filas y valores originales, sin aplanarlas a texto. Los metadatos seguros pueden generar un item `Metadata`; propiedades de ruta o referencia interna se omiten.

El chunking solo se aplica a contenido largo, usa caracteres, prefiere saltos de párrafo o línea y admite solapamiento configurable. La deduplicación es conservadora y local: solo omite contenido normalizado idéntico con el mismo tipo y procedencia. `ContentHash` usa SHA-256; el del documento combina el hash de RawDocument con opciones relevantes. Los identificadores no reutilizan el hash como valor público.

## Evolución

Memory durable local almacena snapshots de `KnowledgeDocument` mediante `IMemoryStore` sin modificar el Builder. RAG puede consumir retrieval sobre esos items, pero Knowledge no genera embeddings ni administra Vector Index. Vector Index y embeddings siguen derivados e in-memory; su reindexado desde Memory durable queda fuera de este alcance. Planner solicita datos mediante contratos y herramientas autorizadas; Kai recibe contexto mínimo.

No se crea `KnowledgeTool`: pasar un RawDocument completo mediante el Tool System introduce serialización y acoplamiento sin una referencia segura persistente. Tampoco se crea `POST /knowledge/build`; las pruebas e integraciones futuras usan `IKnowledgeBuilder` directamente.
