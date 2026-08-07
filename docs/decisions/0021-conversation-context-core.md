# ADR 0021: Conversation Context Core

## Estado

Accepted.

Conversation Context se separa de Kai Agent y usa historial proporcionado por el caller, sin persistencia. Selecciona los turnos recientes por presupuesto, conserva roles y deja el mensaje actual fuera del pack. No integra RAG, Chat, LLM ni Tools.
