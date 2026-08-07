# ADR 0017: Semantic Search Core

## Estado

Accepted.

Semantic Search se implementa como capa independiente sobre `IVectorIndex`, usando exclusivamente cosine similarity para vectores compatibles. No integra generación, texto ni contexto.
