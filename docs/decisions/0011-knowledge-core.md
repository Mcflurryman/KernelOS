# ADR 0011: Knowledge Core

## Estado

Accepted.

## Decisión

KernelOS incorpora Knowledge Core como una transformación interna y determinista de `RawDocument` a `KnowledgeDocument` e `KnowledgeItem`, mediante `IKnowledgeBuilder`. No se expone una herramienta ni endpoint en esta fase.

## Motivo

La separación mantiene a Filesystem como frontera de rutas, a Document Readers como frontera de formatos y a Knowledge como normalización de unidades consultables. Evita que el modelo, Planner o una API pública reciban rutas internas o transporten innecesariamente un documento crudo serializado.

## Consecuencias

Los items conservan procedencia segura, localizadores, tablas y metadatos permitidos. El constructor no usa filesystem, LLM, embeddings ni almacenamiento. Memory y RAG podrán consumir los modelos posteriormente; requerirán sus propias decisiones sobre persistencia, versionado, embeddings y acceso.
