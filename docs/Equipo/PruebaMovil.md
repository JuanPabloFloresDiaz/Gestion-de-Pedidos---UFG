# 📱 Prueba de la aplicación móvil (Android con .NET MAUI)

> Guía para **probar en Windows** la rama `feat/maui-movil`. Sirve igual para una persona que para un agente de IA (Claude Code, Antigravity): las instrucciones son pasos concretos con su resultado esperado. Lo que se sabe que **no está verificado** está marcado.

## 1. Qué se hizo y qué no se ha probado

El proyecto `src/GestionPedidos.Desktop` (MAUI Blazor Hybrid) ahora tiene **dos destinos**: Windows (el de siempre) y **Android** (nuevo). Los dos cargan la **misma interfaz** (`GestionPedidos.Ui`), así que no hay pantallas nuevas.

| Archivo | Qué cambió |
|---|---|
| `GestionPedidos.Desktop.csproj` | `TargetFrameworks` = `net8.0-android` (+ `net8.0-windows10.0.19041.0` solo en Windows). Lo propio de Windows y de Android va en grupos con condición. |
| `Platforms/Android/` | `AndroidManifest.xml` (permiso de Internet y tráfico `http` para desarrollo), `MainActivity.cs`, `MainApplication.cs` y `Resources/values/colors.xml`. |
| `appsettings.android.json` | Dirección de la API **dentro del APK**. Por defecto `http://10.0.2.2:4390` (el equipo visto desde el **emulador**). |
| `MauiProgram.cs` | En Android la app se comporta como la **web** (tienda para el cliente y administración para el personal), no como el escritorio (solo personal). Lee la API de `appsettings.android.json`. |
| `GestionPedidos.Ui.csproj` | **Cambio en código compartido:** en lugar de `FrameworkReference Microsoft.AspNetCore.App` (que Android no tiene) se referencia el paquete `Microsoft.AspNetCore.Components.Web`. Sin esto Android no compila (`NETSDK1082`). La interfaz no usa nada más de ASP.NET Core. |
| `Desktop/wwwroot/index.html` | Enlaza también `tienda.css` (antes solo `tema.css`: en Android la tienda se veía sin estilos). |
| `UiOptions.ImagenesComoDatos`, `Tienda/ImagenesService.cs`, `Tienda/ImagenRemota.razor` | Las fotos de productos y categorías se descargan de la API desde C# y se muestran como `data:` (la app nativa no tiene el proxy `/imagenes/...` de la web y el WebView bloquea el contenido mixto `https`/`http`). La web no cambia. |
| `TiendaLayout.razor`, `tienda.css` | En pantallas de hasta 640 px las categorías van en un menú desplegable; el panel de Apariencia ya no se sale de la pantalla; la foto del carrito no se encima con el texto. |
| `Directory.Packages.props` | `Microsoft.AspNetCore.Components.Web` sube de 8.0.11 a 8.0.29 (lo exige `Blazicons`). |

**Verificado (en Linux/WSL, 2026-11-18):** el destino Android **compila** (`dotnet build -f net8.0-android`) y genera un APK firmado de depuración (150 MB, 4 arquitecturas). Se comprobó dentro del APK: paquete `sv.edu.ufg.gestionpedidos`, nombre «NoviXtore», Android 5.0+, permisos de Internet, la interfaz (`_content/GestionPedidos.Ui/...`) y `appsettings.json` con `10.0.2.2:4390`. Tras el cambio de `Ui`, las pruebas de Domain, Application, Architecture y Ui y la compilación de la web (Docker) siguen en verde.

**NO verificado:** que el destino **Windows** siga compilando (no hay Windows aquí), y **nunca se ha ejecutado** la app en un emulador ni en un celular. Es posible que aparezcan detalles en el primer arranque; por eso esta guía incluye qué hacer si falla.

### Atajo: instalar el APK ya compilado
Si solo se quiere ver la app en un emulador o celular **sin compilar nada**, el APK de depuración (con la API en `10.0.2.2:4390`, es decir, **para emulador**) se instala con:
```powershell
adb install -r NoviXtore-debug-emulador.apk
```
(Para un celular real hay que cambiar la dirección de la API y volver a compilar: §3.) El APK no se sube a git (pesa 150 MB); se pasa por fuera del repositorio.

## 2. Requisitos (Windows)

1. **SDK de .NET 8** (`dotnet --version` ≥ 8.0.4xx; el repo fija la versión en `global.json`).
2. **Carga de trabajo de MAUI para Android:** `dotnet workload install maui-android` (o Visual Studio 2022 17.8+ con la carga «Desarrollo de interfaz de usuario multiplataforma de .NET (MAUI)»).
3. **JDK 17** y el **SDK de Android** (plataforma API 34 + herramientas). Lo más simple es dejar que .NET los descargue:
   ```powershell
   dotnet build src\GestionPedidos.Desktop -f net8.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory="C:\Android\sdk" -p:AcceptAndroidSDKLicenses=True
   ```
4. Un **emulador** (Android Studio → Device Manager, o `avdmanager`) con Android 10 o superior, **o** un celular con la depuración USB activada.
5. **La API y la base en marcha** (Docker): `./scripts/up.sh` (en WSL o Git Bash). Compruebe `http://localhost:4390/health` desde Windows. La API publica en todas las interfaces (`4390`), así que el emulador la alcanza.

## 3. Compilar y ejecutar

Desde la raíz del repositorio:

```powershell
# 1) Que lo de Windows no se haya roto (debe compilar sin errores, como antes)
dotnet build src\GestionPedidos.Desktop -f net8.0-windows10.0.19041.0

# 2) Android: compilar
dotnet build src\GestionPedidos.Desktop -f net8.0-android

# 3) Android: instalar y abrir en el emulador o celular conectado
dotnet build src\GestionPedidos.Desktop -f net8.0-android -t:Run
```

### Dirección de la API según dónde se ejecute
| Dónde | `Api:BaseUrl` en `appsettings.android.json` |
|---|---|
| Emulador de Android | `http://10.0.2.2:4390` (valor por defecto: `10.0.2.2` es el equipo anfitrión) |
| Celular real en la misma red Wi‑Fi | `http://<IP-de-su-PC>:4390` (`ipconfig`; permitir el puerto 4390 en el Firewall de Windows) |

Después de cambiar ese archivo hay que **volver a compilar e instalar** (viaja dentro del APK).

## 4. Qué comprobar (lista de verificación)

Marque cada punto y anote lo que falle (con captura y el mensaje de `adb logcat | findstr /i "gestionpedidos mono"` si la app se cierra):

- [ ] La app abre y muestra la pantalla de inicio de NoviXtore (tienda), sin pantalla en blanco ni «Cargando…» eterno.
- [ ] **Tienda:** el catálogo carga con las imágenes de los productos, se puede ver un producto y agregarlo al carrito.
- [ ] **Registro e inicio de sesión de un cliente:** se puede crear una cuenta (clave de **14 caracteres o más**) y entrar.
- [ ] **Hacer un pedido** y verlo en «Mis pedidos». Con el pedido entregado desde la administración, aparece **«Confirmar recibido»**.
- [ ] **Administración (personal):** iniciar sesión como `admin` y abrir Usuarios, Roles, Categorías, Catálogos e Historial. Revisar que las tablas se vean bien o al menos se puedan desplazar.
- [ ] **Teclado y formularios:** escribir en un campo no tapa el botón de guardar.
- [ ] **Girar la pantalla** y volver al estado anterior no pierde la sesión.
- [ ] Cerrar la app y abrirla de nuevo: **pide iniciar sesión otra vez** (la sesión vive solo en memoria; es lo esperado).

### Limitaciones conocidas (no son errores nuevos)
* **Exportar reportes (PDF/Excel/CSV) no descarga en Android:** el botón usa una descarga del navegador (`descarga.js`) que el WebView de Android no maneja. Para este caso hay que usar el escritorio o la web. Es una mejora futura (guardar el archivo con `FileSystem`/`Share` de MAUI).
* **Tráfico `http`:** el manifiesto permite `http` solo para desarrollo. Antes de publicar la app, la API debe ir por `https` y se quita `usesCleartextTraffic`.
* **iOS** no está incluido (requiere una Mac).
* La interfaz de administración se diseñó para escritorio: en un celular puede verse apretada.

## 5. Si algo falla al compilar o ejecutar

1. **Arregle lo mínimo** en la rama `feat/maui-movil` y repita el paso que falló. Cada arreglo, un commit corto en español.
2. **No rompa Windows:** después de cada arreglo, `dotnet build ... -f net8.0-windows10.0.19041.0` debe seguir compilando.
3. Si el cambio toca código compartido (`GestionPedidos.Ui`), corra también las pruebas: `./scripts/test-docker.sh` (en WSL).
4. Reglas del repositorio: `AGENTS.md` (comentarios XML en español, sin lógica en las páginas, nada exclusivo de un hospedaje dentro de `Ui`). **Sin marcas de IA ni `Co-Authored-By` en commits o PRs.**
5. Al terminar, deje en este archivo (§6) el resultado de la lista de verificación.

## 6. Resultado de la prueba (completar)

| Fecha | Equipo / emulador | Resultado | Notas |
|---|---|---|---|
| 2026-11-18 | Windows 11 · emulador Android 14 (API 34, x86_64) | Parcial | Windows compila (0 advertencias). Verificado: abre, catálogo con fotos, filtro por categoría, carrito, menú de categorías, panel de Apariencia y acceso a la administración (visible pero no adaptada a celular; sirve en tableta). Sin recorrer aún: registro, pedido, giro de pantalla y reinicio. Detalle: en Debug solo `-t:Run` despliega el código nuevo (`adb install` deja el viejo). |
