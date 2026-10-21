# AGENTS.md — Gestión de Pedidos (UFG)

Reglas cortas para cualquier agente de IA que trabaje en este repositorio. La fuente de verdad es `docs/`; este archivo solo la resume.

## Léelo antes de escribir código
1. `docs/Analisis.md` (requerimientos) y la spec del módulo en `docs/Especificaciones/`.
2. `docs/Equipo/PautasCodigoEventos.md` (cómo escribir delegados, eventos, Página + ViewModel, comentarios). **Si algo de ahí contradice tu costumbre, gana el documento.**
3. `docs/Arquitectura.md` y `docs/Estructura.md` (capas y dónde va cada archivo).

## Reglas
- Stack: **.NET 8** (la migración a .NET 10 está prevista en `docs/Tecnologia.md §5`; no la hagas sin que se pida), **una única interfaz Blazor** (`GestionPedidos.Ui`, Razor Class Library) hospedada en **.NET MAUI Blazor Hybrid** (escritorio) y **Blazor Web App** (web) + API ASP.NET Core + EF Core + SQL Server. API, web y base de datos corren en **Docker** (usa `scripts/*-docker.sh`); el escritorio se compila y ejecuta en Windows. Editor: VS Code.
- **Nada exclusivo de un hospedaje dentro de `Ui`** (ni `HttpContext` ni APIs de MAUI); estilo solo con el tema y el kit de `Comun/Componentes`, sin colores, fuentes ni CSS sueltos.
- Páginas `.razor` **sin lógica** (Página + ViewModel con evento `Cambiado`). Eventos con delegado `XEventHandler` + `XEventArgs` + `OnX` con `?.Invoke`, disparados **solo después** de confirmar la operación; todo `+=` con su `-=` en `Dispose`; repintar con `InvokeAsync(StateHasChanged)`.
- **Comentarios XML en español** en todo tipo o miembro público; comentarios de "cómo/por qué" en lo no obvio (requisito de la materia).
- La interfaz **solo habla con la API**: nunca cadena de conexión, SQL ni secretos fuera de la API.
- Consultas **siempre parametrizadas** (EF Core o el procedimiento con parámetros). Sin SQL concatenado.
- Baja **lógica** (`Activo`), nunca `DELETE` de clientes, productos, pedidos o usuarios.
- Pruebas primero; cada módulo con pruebas de dominio, caso de uso, API, ViewModel y eventos (`docs/Especificaciones/Testing.md`).
- Idioma: dominio, interfaz y comentarios en español; términos técnicos estándar en inglés.
- No inventes requerimientos: las interpretaciones del enunciado están en `docs/Analisis.md §3`; si falta información, **pregunta**.
- Una sola migración de EF Core a la vez (avisa al equipo antes de generarla).

## Flujo
- **Commits**: solo cuando la persona lo pida o lo haya autorizado. Un commit por apartado revisable, con título y descripción cortos en español. **Nunca añadas una co-autoría ni ninguna marca de IA** (`Co-Authored-By`, "Generated with…") en commits ni en Pull Requests; los commits salen solo a nombre de la persona. No fuerces cambios (`push --force`) ni reescribas el historial.
- Si cambias el comportamiento, actualiza la spec correspondiente en el mismo cambio.
- Antes de dar algo por terminado: compila sin advertencias, pruebas en verde, y revisa la lista de `docs/Equipo/PautasCodigoEventos.md §10`.
