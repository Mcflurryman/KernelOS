# ADR 0036: Conversation Pending Correlation

## Estado

Accepted.

## Decisión

KernelOS persiste sólo la identidad durable entre conversación y pending execution en la SQLite existente. El orchestrator registra la relación después de crear la confirmation y persistir los mensajes disponibles. No se persisten PendingExecution, Approval, snapshots, argumentos, fingerprints ni resultados.

ConversationId es correlación UX, no autoridad. Discovery usa un endpoint seguro que combina la identidad durable con el confirmation service runtime. Los contratos HTTP de confirmation y execute proyectan DTOs públicos; Approve y Execute siguen siendo acciones explícitas separadas y no se reintentan automáticamente. Pending ausente tras restart se representa como `Unavailable`; el resultado de ejecución es transitorio.

## Consecuencias

Un refresh dentro del mismo proceso puede recuperar confirmations activas. Tras restart queda una correlación histórica sin capacidad de acción y no existe estado durable de executed. Esta frontera permite un futuro milestone de pending durable sin otorgar autoridad a la conversación.
