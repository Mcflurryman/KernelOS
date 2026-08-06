# Document Readers

> Estado: Document Readers Core implementado para TXT, Markdown, JSON y CSV. PDF, DOCX, XLSX, OCR, Knowledge y Memory permanecen pendientes.

La implementación usa contratos en Core, Readers registrados explícitamente, Registry para localización, Router para selección segura, ReadService para delegar autorización al filesystem, `DocumentTool` y `POST /documents/read`. Los límites configurables cubren tamaño, caracteres, filas y columnas; `DOCUMENT_TRUNCATED` e `IRREGULAR_TABLE` son warnings estables. Las respuestas públicas omiten referencias internas.

`TooLarge` se traduce mediante `ToolExecutionStatus.TooLarge`, no mediante texto de mensajes, y la API devuelve HTTP 413. Un fallo interno conserva `Failed` y devuelve HTTP 500. CSV con un campo entre comillas sin cerrar devuelve `InvalidDocument` si no se permiten parciales; si se permiten, devuelve `PartialSuccess` con `CSV_UNCLOSED_QUOTED_FIELD` y solo conserva registros verificables.

## Propósito y frontera

Document Readers será la única frontera autorizada para interpretar formatos de archivo. Filesystem encuentra, autoriza y entrega una `FileReference`; un Reader extrae contenido estructurado; Knowledge transforma ese contenido en información consultable. Ninguna capa sustituye a otra.

```text
Filesystem -> FileReference autorizada -> Document Reader -> RawDocument
                                                        -> Knowledge -> Memory / Planner / Kai
```

Un Reader identifica formatos, abre contenido mediante una referencia autorizada, extrae texto, tablas, metadatos y estructura, conserva procedencia, devuelve advertencias y controla corrupción, límites y cancelación. No busca, mueve ni autoriza archivos; no razona, resume, genera embeddings, almacena memoria, elige herramientas, modifica documentos ni ejecuta macros, scripts, enlaces o instrucciones del documento.

El contenido documental es dato no confiable. Las instrucciones incluidas en él nunca son instrucciones del sistema ni autorizan acciones.

## Modelo común futuro

Los siguientes contratos vivirán conceptualmente en `KernelOS.Core` y serán serializables e independientes de parsers concretos. No se implementan en esta fase.

| Contrato | Datos y responsabilidad |
| --- | --- |
| `RawDocument` | Obligatorios: `Id`, `Source`, `Format`, `ReadAt`, `ContentHash`, `Metadata`, `Warnings`. Opcionales: `MimeType`, `Title`, `TextContent`, `Sections`, `Tables`. Representa contenido estructurado, no fidelidad visual. |
| `DocumentSource` | Referencia interna autorizada, referencia segura para logs, referencia mostrable a la persona usuaria y localizadores de procedencia. Nunca obliga a exponer una ruta absoluta al modelo. |
| `DocumentSection` | Texto y estructura de párrafos, títulos, listas, páginas, bloques de código, claves JSON o hojas; incluye su localizador de origen. |
| `DocumentTable` | Columnas, encabezados opcionales, filas, nombre de hoja o sección, posición de origen y tipos básicos detectados. |
| `DocumentTableRow` | Celdas y valores originales, sin inventar valores ni forzar la tabla a texto plano. |
| `DocumentMetadata` | Metadatos documentales declarados o extraídos, formato, codificación cuando se conozca y propiedades seguras. |
| `DocumentWarning` | Código estable, mensaje seguro, severidad y localizador; nunca una excepción interna. |
| `DocumentReadRequest` | `FileReference` autorizada, límites, opciones de lectura y contexto de cancelación. |
| `DocumentReadResult` | `DocumentReadStatus`, `RawDocument` opcional, advertencias y error seguro. |
| `DocumentReadStatus` | `Success`, `PartialSuccess`, `UnsupportedFormat`, `InvalidDocument`, `TooLarge`, `Unauthorized`, `NotFound`, `Cancelled`, `Failed`. |

Cada fragmento conserva un localizador: línea para TXT, clave o ruta para JSON, página para PDF, sección para Word, hoja y rango para Excel y fila/columna para CSV. Knowledge debe poder rastrear cualquier fragmento a ese origen.

## Interfaces futuras

`IDocumentReader` declarará formatos y MIME soportados, determinará si puede leer una referencia y leerá asíncronamente con `CancellationToken`, devolviendo `DocumentReadResult`.

`IDocumentReaderRegistry` registrará lectores explícitamente mediante DI, los localizará por formato o MIME, detectará conflictos y listará los disponibles. No será un Service Locator, ni usará reflexión o escaneo automático pesado.

`IDocumentReaderRouter` seleccionará un lector compatible y le delegará la lectura. No interpreta contenido, no consulta al LLM y devolverá `UnsupportedFormat` si no hay un lector. El Registry conoce lectores registrados; el Router aplica la selección y los límites a una solicitud concreta.

## Estados, errores y resultados parciales

`Success` contiene una lectura completa dentro de límites. `PartialSuccess` entrega únicamente contenido verificable junto a advertencias; se permite solo si `AllowPartialResults` lo autoriza. Un archivo corrupto, protegido por contraseña, bloqueado o con formato inválido se traduce a un estado seguro, sin filtrar excepciones. Un PDF escaneado sin texto devuelve una advertencia o resultado parcial sin inventar OCR; JSON inválido y CSV de delimitador ambiguo se rechazan o advierten según se pueda identificar contenido fiable.

Hojas ocultas, fórmulas y macros se representan solo como datos y metadatos futuros; no se ejecutan. Codificaciones desconocidas, límites de tamaño, archivos bloqueados y cancelación se resuelven respectivamente mediante advertencia o error seguro, `TooLarge`, estado controlado y `Cancelled`.

`DocumentReaderOptions` será configuración futura con `MaxFileSizeBytes`, `MaxExtractedCharacters`, `MaxPages`, `MaxRows`, `MaxColumns`, `TimeoutSeconds`, `AllowPartialResults` y `TemporaryDirectory`. Los límites se aplican antes y durante la extracción; la cancelación se propaga a todas las operaciones. Los temporales, cuando sean imprescindibles, se crearán y eliminarán de forma segura.

## Seguridad

1. Los Readers reciben solo referencias ya autorizadas por Filesystem; no resuelven rutas arbitrarias.
2. Funcionan inicialmente en modo solo lectura y no ejecutan contenido activo.
3. El hash detecta duplicados, reprocesado y relación futura de versiones; su algoritmo será configurable, no es autorización ni identificador permanente, y cambios mínimos generan hashes distintos.
4. Los logs contienen identificadores, estados y referencias redactadas, nunca texto completo ni rutas sensibles.
5. Todo contenido, incluidos encabezados, comentarios, macros y enlaces, se trata como entrada no confiable y se aísla de instrucciones de sistema o herramientas.

## Formatos y representación

La primera fase prevista cubre TXT, Markdown, JSON y CSV. La segunda cubrirá PDF, DOCX y XLSX. OCR e imágenes, presentaciones, audio y otros formatos quedan para fases posteriores; ninguno está implementado ahora.

TXT y Markdown preservarán líneas, títulos, listas y bloques de código cuando se detecten. JSON conservará claves y rutas estructurales. CSV, Excel, Word y PDF podrán producir `DocumentTable` sin perder columnas, celdas, encabezados, contexto de hoja o sección y posición de origen. RawDocument no intenta reproducir diseño visual completo.

## Relación con las capas

- **Filesystem:** entrega `FileReference` autorizada; no interpreta contenido.
- **Document Readers:** extraen `RawDocument`; no producen conocimiento semántico.
- **Knowledge:** normaliza y transforma RawDocument en conocimiento consultable.
- **Memory:** almacena e indexa conocimiento, no archivos crudos por defecto.
- **Planner:** solicita una lectura mediante herramientas autorizadas cuando necesita contenido; no conoce parsers ni librerías concretas.
- **Kai:** recibe únicamente contexto necesario y no depende de la librería que procesó el archivo.
- **MCP y Drive:** podrán aportar referencias o streams autorizados; Readers deben evolucionar sin acoplarse a rutas locales.

## Estrategia de implementación futura

Fase 1: contratos, Registry, Router, TxtReader, MarkdownReader, JsonReader y CsvReader. Fase 2: PdfReader, DocxReader y XlsxReader. Fase 3: OCR, imágenes, presentaciones y formatos adicionales.

Core alojará contratos y modelos. Infrastructure alojará implementaciones locales, configuración y adaptadores de bibliotecas. Tools expondrá una herramienta de lectura futura que delegue al Router. API solo expondrá contratos HTTP cuando exista una herramienta y política aprobadas. No se crean proyectos nuevos; una separación futura requerirá evidencia de dependencias, despliegue o aislamiento suficiente.

Las bibliotecas para PDF, DOCX, XLSX y CSV se evaluarán antes de elegirlas según licencia, mantenimiento, seguridad, compatibilidad .NET 8, procesamiento local, streaming, rendimiento, tolerancia a corrupción y ausencia de servicios externos obligatorios. No hay dependencia decidida.

## Pruebas futuras

```text
testdata/documents/
├── text/
├── markdown/
├── json/
├── csv/
├── pdf/
├── docx/
├── xlsx/
├── corrupt/
└── security/
```

Los datos serán pequeños, sintéticos y sin información personal. Las pruebas cubrirán documento válido, vacío, Unicode, corrupto, demasiado grande, cancelación, formato no soportado, resultado parcial, procedencia, tablas, seguridad y prompt injection documental.

## Riesgos y preguntas abiertas

Riesgos: consumo de recursos con archivos adversariales, parsers vulnerables, ambigüedad de codificaciones y CSV, extracción parcial engañosa, documentos protegidos, datos sensibles en contenido y diferencias entre proveedores remotos.

Preguntas abiertas: si RawDocument guarda texto completo o chunks/streams; fórmulas Excel; PDF escaneado y activación de OCR; estilos relevantes; metadatos expuestos al modelo; versionado; documentos protegidos; límites por defecto; documentos extremadamente grandes; y coordinación entre Readers locales y proveedores remotos.
