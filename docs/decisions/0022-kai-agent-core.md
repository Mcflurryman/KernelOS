# ADR 0022: Kai Agent Core v1

## Estado

Accepted.

Kai Agent v1 orquesta una sola ruta Chat o RAG mediante contratos. El routing es determinista, Auto elige Chat salvo intención documental explícita y un `NoContext` de RAG puede volver a Chat solo en Auto si está configurado.

Planner se aplaza: su contrato actual ejecuta Tools, por lo que Kai no llama `IPlanner` ni `IToolRouter`; el modo Planner responde de forma segura como no disponible. Los siguientes milestones son Planner Separation / Plan Execution Safety y Tool Confirmation & Orchestration.
