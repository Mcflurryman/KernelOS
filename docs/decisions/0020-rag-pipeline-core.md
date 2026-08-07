# ADR 0020: RAG Pipeline Core

## Estado

Accepted.

## Decisión

RAG es una capa interna separada de Kai Agent. Orquesta únicamente contratos de retrieval, contexto, prompt y chat; no depende de implementaciones concretas, Tools ni Planner. No hace fallback a respuesta general cuando falta contexto.

El prompt builder es independiente y trata el contenido documental como no confiable. Las citas proceden del ContextPack y se exponen como fuentes disponibles. No se incluye historial conversacional ni se validan citas con un LLM.

## Consecuencias

La frontera de privacidad depende del proveedor de `IChatModel`. RAG instruye grounding, pero no elimina matemáticamente las alucinaciones. Kai Agent, conversación persistente y una API pública siguen siendo decisiones posteriores.
