# 🧪 Estrategia de Pruebas - Gestión de Pedidos (UFG)

> Se define **antes** de implementar (SDD). Todo se ejecuta en Docker con `scripts/test-docker.sh` ([Infraestructura.md §3](../Infraestructura.md)). Las pruebas respaldan el criterio de la rúbrica "ejecución correcta sin errores".

## 1. Pirámide de Pruebas

| Nivel | Proyecto | Qué prueba | Dependencias reales |
|---|---|---|---|
| **Unitarias de dominio** | `Domain.Tests` | Entidades, reglas, transiciones, stock | Ninguna |
| **Aplicación** | `Application.Tests` | Casos de uso con puertos simulados; eventos publicados | Ninguna |
| **Integración de datos** | `Infrastructure.Tests` | Configuración EF, migraciones, `CHECK`/`UNIQUE`/FK, concurrencia, `usp_Pedidos_Buscar` | **SQL Server real** (Testcontainers) |
| **Integración HTTP** | `Api.Tests` | Endpoints, validación, 401/403, Problem Details | `WebApplicationFactory` + SQL Server real |
| **Interfaz** | `Ui.Tests` | **ViewModels** y servicios de módulo con un `HttpMessageHandler` falso (publicación y baja de eventos); **componentes** con **bUnit** (kit, validación, eventos) | Ninguna (sin navegador ni Windows) |
| **Arquitectura** | `Architecture.Tests` | Reglas de dependencias de [Arquitectura.md §2](../Arquitectura.md) | Ninguna |
| **Extremo a extremo** | `e2e/` (Playwright) | Recorridos reales en navegador contra el entorno de Docker: login, cambio de clave, pestañas, alta de usuario, permisos del operador | Entorno completo (`./scripts/up.sh`) |
| **Aceptación manual** | Lista de verificación | Las pantallas reales por rol en **escritorio y web** | Aplicación en Windows y navegador |

## 2. Reglas

* Una prueba por comportamiento; nombre `Metodo_Escenario_Resultado`; patrón *Arrange–Act–Assert*.
* **Sin SQLite ni `InMemory` de EF** para persistencia: se comportan distinto a SQL Server (restricciones, `RowVersion`, transacciones).
* Dominio y reglas puras: **100 % de cobertura de ramas**; en el resto, la cobertura es un indicador.
* Tiempo y fechas mediante `TimeProvider` inyectado (`FakeTimeProvider` en pruebas); nada de `DateTime.Now` en lógica.
* Cada bug corregido añade una prueba que lo reproduce.
* **Las páginas no llevan lógica**, por eso casi todo se prueba en ViewModels y servicios; las pantallas completas, con Playwright (web) y a mano (escritorio).

## 3. Pruebas de Eventos (obligatorias en cada módulo)

1. La publicadora dispara el evento **una vez** con los `EventArgs` correctos al tener éxito.
2. **No** lo dispara si la operación falla.
3. Con un suscriptor falso (`+=` con lambda) se verifica `sender` y datos.
4. Sin suscriptores no hay excepción.
5. Un ViewModel reacciona al evento de un servicio falso y dispara `Cambiado`.
6. Tras `Dispose`/cierre ya no recibe eventos.

## 4. Harness de Integración

* Cada proyecto de integración levanta **un contenedor SQL Server por ejecución** (fixture compartida: `BaseDeDatosFixture` y `ApiFixture`) y aplica las migraciones reales; las pruebas usan datos con nombre único para no pisarse.
* Dentro de contenedores: socket de Docker montado y `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` en Docker Desktop.
* Para probar permisos de BD (por ejemplo, que `pedidos_app` no pueda actualizar `Actividades`) se ejecuta con ese usuario.

## 5. Casos Obligatorios Transversales

* **Concurrencia**: dos pedidos por la última unidad (uno gana, el otro `StockInsuficiente`/`409`); dos cambios de estado simultáneos del mismo pedido.
* **Atomicidad**: un pedido con 2 líneas donde la 2.ª no tiene stock no deja cambios en la 1.ª.
* **Seguridad**: cada endpoint protegido probado sin sesión (401), sin permiso (403) y con permiso; intentos de inyección SQL en filtros de texto.
* **Autenticación**: bloqueo al 5.º fallo, usuario inactivo (la petición siguiente falla), expiración del JWT a los 60 min, registro web transaccional.
* **Datos ajenos**: un cliente de la web no puede ver, cancelar ni crear pedidos de otro cliente (prueba por cada endpoint `mis-pedidos`/`mi-perfil`).
* **Baja lógica y eliminación definitiva**: dar de baja solo marca `Activo = 0` y `EliminadoEn` (reversible); el `DELETE` físico existe solo para usuarios ya dados de baja, sin pedidos, y con permiso `usuarios.eliminar`. Igual para el catálogo de clasificación (solo inactivo y sin uso, `categorias.eliminar`). Clientes, productos y pedidos no se borran.
* **Respaldo**: la restauración reproduce los conteos de filas ([RespaldoYEntrega.md](RespaldoYEntrega.md)).

## 6. Cómo se Ejecutan (sin CI/CD)

El equipo decidió **no usar CI/CD** para avanzar más rápido: la verificación es local y manual, pero obligatoria antes de subir cambios.

```bash
./scripts/test-docker.sh          # unitarias + integración (SQL Server real) + arquitectura, todo en Docker
./scripts/up.sh && ./scripts/e2e-docker.sh   # extremo a extremo con Playwright (si se tocó la interfaz)
```

* Si no se tiene Docker a mano, con el SDK instalado: `dotnet test tests/GestionPedidos.<Proyecto>.Tests` (las de integración necesitan Docker igualmente).
* El proyecto `Desktop` (MAUI) no se compila en Linux; se verifica a mano en Windows ([PrimerosPasos.md §4](../Equipo/PrimerosPasos.md)).
* Una prueba en rojo bloquea el PR por acuerdo del equipo (la revisión lo comprueba).

## 7. Pruebas de Extremo a Extremo (Playwright)

Viven en `e2e/` (TypeScript) y corren en una imagen oficial de Playwright con `./scripts/e2e-docker.sh`; no hay que instalar Node ni navegadores. Cubren los recorridos que más importan en la defensa y **guardan capturas** en `e2e/capturas/`. Un módulo nuevo añade su `*.spec.ts` con su recorrido principal.
