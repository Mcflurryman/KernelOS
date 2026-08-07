# Conversation Context Core

Conversation Context prepara historial de trabajo por request para un futuro Kai Agent. Recibe turnos `User` y `Assistant`, no persiste ni consulta Memory documental, y no llama a Chat, RAG, Planner o Tools.

El mensaje actual se valida pero queda fuera del pack: el pack contiene solo historial previo. La selección recorre la entrada desde el final, conserva los turnos recientes que caben en MaxTurns y presupuesto de tokens, y los devuelve en el orden original. No resume ni corta turnos; el contenido y los roles se preservan como datos no confiables.
