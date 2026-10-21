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
| **Aceptación manual** | Lista de verificación | Las pantallas reales por rol en **escritorio y web** | Aplicación en Windows y navegador |

## 2. Reglas

* Una prueba por comportamiento; nombre `Metodo_Escenario_Resultado`; patrón *Arrange–Act–Assert*.
* **Sin SQLite ni `InMemory` de EF** para persistencia: se comportan distinto a SQL Server (restricciones, `RowVersion`, transacciones).
* Dominio y reglas puras: **100 % de cobertura de ramas**; en el resto, la cobertura es un indicador.
* Tiempo y fechas mediante `TimeProvider` inyectado (`FakeTimeProvider` en pruebas); nada de `DateTime.Now` en lógica.
* Cada bug corregido añade una prueba que lo reproduce.
* **Las páginas no llevan lógica**, por eso casi todo se prueba en ViewModels y servicios; los componentes con bUnit y las pantallas completas, manualmente.

## 3. Pruebas de Eventos (obligatorias en cada módulo)

1. La publicadora dispara el evento **una vez** con los `EventArgs` correctos al tener éxito.
2. **No** lo dispara si la operación falla.
3. Con un suscriptor falso (`+=` con lambda) se verifica `sender` y datos.
4. Sin suscriptores no hay excepción.
5. Un ViewModel reacciona al evento de un servicio falso y dispara `Cambiado`.
6. Tras `Dispose`/cierre ya no recibe eventos.

## 4. Harness de Integración

* `AbstractIntegrationTest` levanta **un contenedor SQL Server por ejecución** (fixture compartida), aplica las migraciones reales y limpia datos con **Respawn** entre pruebas.
* Dentro de contenedores: socket de Docker montado y `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` en Docker Desktop.
* Para probar permisos de BD (por ejemplo, que `pedidos_app` no pueda actualizar `Actividades`) se ejecuta con ese usuario.

## 5. Casos Obligatorios Transversales

* **Concurrencia**: dos pedidos por la última unidad (uno gana, el otro `StockInsuficiente`/`409`); dos cambios de estado simultáneos del mismo pedido.
* **Atomicidad**: un pedido con 2 líneas donde la 2.ª no tiene stock no deja cambios en la 1.ª.
* **Seguridad**: cada endpoint protegido probado sin sesión (401), sin permiso (403) y con permiso; intentos de inyección SQL en filtros de texto.
* **Autenticación**: bloqueo al 5.º fallo, usuario inactivo (la petición siguiente falla), expiración del JWT a los 60 min, registro web transaccional.
* **Datos ajenos**: un cliente de la web no puede ver, cancelar ni crear pedidos de otro cliente (prueba por cada endpoint `mis-pedidos`/`mi-perfil`).
* **Baja lógica**: no hay `DELETE` físico de clientes, productos, pedidos ni usuarios.
* **Respaldo**: la restauración reproduce los conteos de filas ([RespaldoYEntrega.md](RespaldoYEntrega.md)).

## 6. Integración Continua

CI ejecuta, en contenedor: formato (`dotnet format --verify-no-changes`), compilación con advertencias como errores (el host `Desktop` se compila aparte en un *runner* de Windows), todas las pruebas, auditoría de paquetes vulnerables y escaneo de secretos. Un PR no se fusiona con CI en rojo ([GuiaCI.md](../Equipo/GuiaCI.md)).
