# 📏 Principios de Diseño y Código Limpio - Gestión de Pedidos (UFG)

Este documento fija las reglas que se revisan en cada Pull Request. Cada principio incluye **qué significa aquí** y **cómo se ve en este proyecto**, no solo la definición.

## 1. Dogmas del Proyecto

1. **Especificación antes que código (SDD)**: cada módulo se documenta en [Especificaciones/](Especificaciones/) y se define cómo se prueba **antes** de implementarlo.
2. **El dominio no depende de nada**: ni de EF Core, ni de ASP.NET, ni de Blazor. Las dependencias apuntan siempre hacia el dominio ([Arquitectura.md §2](Arquitectura.md)).
3. **Solo Docker**: nada se documenta ni se scriptea asumiendo SDKs instalados en el host ([Infraestructura.md](Infraestructura.md)).
4. **Nada de requerimientos inventados**: toda interpretación del enunciado está registrada en [Analisis.md §3](Analisis.md); si falta información, se pregunta antes de improvisar.
5. **Commits granulares**: un commit por apartado revisable, con título y descripción, y el repositorio en verde en cada uno.

## 2. Principios SOLID

| Principio | Qué exige | Cómo se aplica aquí |
|---|---|---|
| **S** – Responsabilidad Única | Una clase, una razón para cambiar | Un caso de uso = una clase (`ConfirmarPedido`, `CancelarPedido`). Las entidades no persisten ni formatean. |
| **O** – Abierto/Cerrado | Extender sin modificar código estable | Reglas de descuento, validación y cálculo se añaden como nuevas implementaciones de una interfaz (estrategia), no con `if/else` crecientes. |
| **L** – Sustitución de Liskov | Un subtipo debe poder reemplazar a su base sin romper al consumidor | Se prefiere composición a herencia; las interfaces son pequeñas y sus implementaciones no lanzan `NotImplementedException`. |
| **I** – Segregación de Interfaces | Interfaces pequeñas y específicas | `IPedidoReader` / `IPedidoWriter` antes que un `IPedidoService` con 20 métodos. |
| **D** – Inversión de Dependencias | Depender de abstracciones | La capa de aplicación define `IPedidoRepository` e `IUnitOfWork`; Infraestructura los implementa. En la interfaz, el ViewModel depende del servicio del módulo y no de los componentes. Todo se inyecta por constructor. |

## 3. ACID (propiedades de la capa de datos)

> ℹ️ ACID **no es un principio de código limpio**, sino las garantías transaccionales de la base de datos. Se documenta aquí porque condiciona cómo se escribe el código que toca datos.

| Propiedad | Garantía | Aplicación |
|---|---|---|
| **A**tomicidad | Todo o nada | Registrar un pedido (cabecera + líneas + descuento de existencias) ocurre en **una sola transacción**; `SaveChangesAsync` ya es transaccional por llamada, y para varias se usa transacción explícita. |
| **C**onsistencia | Los datos siempre cumplen las reglas | Restricciones en BD (`FK`, `CHECK`, `UNIQUE`, `NOT NULL`) **además** de las invariantes del dominio; nunca solo una de las dos. |
| **A**islamiento | Transacciones concurrentes no se corrompen entre sí | Nivel `READ COMMITTED` (con `READ_COMMITTED_SNAPSHOT` activo) y **concurrencia optimista** con `rowversion` para existencias y pedidos. |
| **D**urabilidad | Lo confirmado persiste | Volumen Docker nombrado para SQL Server; respaldo FULL y restauración verificada ([RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md)). |

## 4. DRY, KISS, YAGNI

* **DRY** (*Don't Repeat Yourself*): cada regla de negocio vive en **un solo lugar** (el dominio). Se extrae lógica repetida cuando aparece la **tercera** vez (regla de tres), no antes: una abstracción prematura es peor que dos copias. DRY aplica al conocimiento, no al texto: dos bloques parecidos que cambian por razones distintas **no** se unifican.
* **KISS** (*Keep It Simple*): la solución más simple que cumpla el requerimiento. Sin patrones "por si acaso"; un `switch` claro gana a una jerarquía de clases innecesaria.
* **YAGNI** (*You Aren't Gonna Need It*): no se construye nada para un requerimiento que nadie ha pedido. Es la razón por la que no hay `IRepository<T>` genérico, ni MediatR, ni microservicios ([Tecnologia.md §2](Tecnologia.md)).

## 5. Otros Principios y Prácticas Adoptadas

* **Separación de responsabilidades (SoC)**: UI, aplicación, dominio e infraestructura en proyectos distintos.
* **Ley de Deméter**: un objeto habla con sus colaboradores directos (`pedido.Total`, no `pedido.Cliente.Direccion.Pais.Impuesto`).
* **Composición sobre herencia**: se hereda solo para modelar "es-un" estable; el comportamiento variable se inyecta.
* **Tell, Don't Ask**: se le pide al objeto que haga (`pedido.Confirmar()`), no se consulta su estado para decidir afuera.
* **CQS** (*Command–Query Separation*): un método o cambia estado o devuelve datos, no ambas cosas.
* **Fail fast / programación defensiva en el borde**: se valida la entrada al entrar al sistema y las invariantes al construir el objeto; un pedido inválido no puede existir.
* **Inmutabilidad por defecto**: `record`, `init`, colecciones de solo lectura expuestas; el estado se cambia solo mediante métodos del agregado.
* **Principio de mínima sorpresa**: nombres y comportamientos predecibles.
* **Regla del Boy Scout**: dejar el código un poco mejor de lo encontrado, sin mezclar refactors con cambios funcionales en un mismo commit.

## 6. Patrones de Diseño en Uso (con criterio)

| Patrón | Dónde | Por qué |
|---|---|---|
| **Entidad / Agregado / Objeto de Valor** (DDD táctico ligero) | Dominio | Invariantes protegidas (`Pedido` es raíz de `LineaPedido`; `Dinero` como objeto de valor) |
| **Caso de uso (*Application Service*)** | Aplicación | Un flujo de negocio por clase, orquestación sin lógica de dominio |
| **Repositorio específico + Unit of Work** | Infraestructura (EF Core) | Aislar la persistencia del dominio |
| **Estrategia** | Reglas intercambiables (descuentos, impuestos) | Cumple Abierto/Cerrado |
| **Observador con delegados y eventos de C#** | Servicios de la interfaz y casos de uso (publicadoras); `Notificador`, ViewModels e historial (suscriptores) | Núcleo de la materia ([PatronEventos.md](Especificaciones/PatronEventos.md), [PautasCodigoEventos.md](Equipo/PautasCodigoEventos.md)) |
| **Página + ViewModel** (variante de MVVM) | Interfaz Blazor | Páginas sin lógica; el ViewModel guarda el estado, avisa con el evento `Cambiado` y se prueba sin pantalla |
| **Inyección de dependencias** | Toda la solución | Inversión de dependencias y pruebas sin infraestructura real |
| **Opciones (`IOptions<T>`)** | Configuración | Configuración tipada y validada al arrancar |

> No se adopta un patrón que no resuelva un problema presente (YAGNI).

## 7. Convenciones de Código

* **Estilo**: `.editorconfig` en la raíz con las reglas de .NET; `dotnet format` antes de subir cambios (con `./scripts/dotnet-docker.sh format GestionPedidos.Linux.slnf`).
* **Nullable reference types** activos (`<Nullable>enable</Nullable>`) y **advertencias como errores** (`TreatWarningsAsErrors`).
* **Nombres**: tipos y miembros públicos en `PascalCase`, privados `_camelCase`, interfaces con prefijo `I`, métodos asíncronos con sufijo `Async` y siempre con `CancellationToken`.
* **Idioma**: el **dominio y la UI usan español** (`Pedido`, `Cliente`, `Producto`) por ser el lenguaje de la materia (lenguaje ubicuo); el código de infraestructura y los identificadores técnicos usan inglés estándar (`Repository`, `Handler`, `Options`). Se mantiene esa línea sin mezclar dentro de un mismo concepto.
* **Métodos pequeños** (idealmente < 20 líneas) con un nivel de abstracción; **nada de números/cadenas mágicas** (constantes o enumeraciones).
* **Comentarios**: el enunciado exige comentar el código explicando **qué hace o cómo funciona**, así que en este proyecto todo tipo y miembro público lleva comentario XML en español y los pasos no obvios llevan comentario en línea con el *cómo* y el *porqué* ([PautasCodigoEventos.md §8](Equipo/PautasCodigoEventos.md)). Aun así, un comentario no sustituye a un buen nombre: si una línea no se entiende, primero se renombra o extrae.
* **Excepciones** para situaciones excepcionales; los errores esperables de negocio se modelan con resultados explícitos (`Result`) y no con excepciones de control de flujo.
* **Asincronía**: `async/await` de extremo a extremo, sin `.Result` ni `.Wait()`; `async void` solo en manejadores de eventos y con `try/catch`.
* **Registro (*logging*)**: `ILogger<T>` con plantillas estructuradas, **sin datos sensibles**.

## 8. Definición de "Terminado"

Un cambio está terminado cuando: compila sin advertencias, pasa los tests de su nivel ([Especificaciones/Testing.md](Especificaciones/Testing.md)), su spec está actualizada, se ejecutó con `docker compose` y el commit es revisable de forma aislada.
