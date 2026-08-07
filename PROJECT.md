# Proyecto KernelOS

## Propósito

KernelOS es una plataforma local-first para construir un asistente personal llamado Kai. Busca que la persona usuaria mantenga control sobre sus datos, modelos, acciones y permisos, sin acoplar las decisiones de producto a un proveedor concreto.

## Visión

KernelOS podrá ayudar a conversar, recuperar conocimiento personal, planificar trabajo y ejecutar acciones autorizadas. Esa evolución será gradual: una capacidad no se considera disponible hasta que sus fronteras de seguridad, contratos, pruebas y documentación existan.

## Principios

- Local-first y minimización de datos; cualquier proveedor remoto requerirá una decisión explícita.
- Los modelos no ejecutan acciones directamente: pasan por contratos, Tools y autorizaciones controladas.
- Core no depende de Infrastructure, Api ni Tools; las integraciones quedan en los bordes.
- Seguridad, mantenibilidad y revisión prevalecen sobre velocidad o automatización prematura.

## Arquitectura conceptual

La plataforma separa contratos independientes en Core, implementaciones y proveedores locales en Infrastructure, Tools como frontera de acciones y Api como borde HTTP. El flujo de conocimiento previsto es Filesystem autorizado → Document Readers → Knowledge → Memory → Retrieval → Context para Kai. Las capas existentes y sus límites se describen en la documentación de arquitectura.

## Objetivos y no objetivos actuales

El objetivo presente es consolidar los componentes locales ya entregados: chat, acciones controladas, ingestión documental, memoria efímera, búsqueda léxica, generación de embeddings, Vector Index In-Memory, Semantic Search, Hybrid Search y Context Builder internos. RAG, automatización general, proveedores cloud, UI, voz, visión y persistencia de memoria siguen fuera del alcance actual.

La evolución futura y sus dependencias se mantienen en el [Roadmap](docs/roadmap/roadmap.md), no en este documento.
