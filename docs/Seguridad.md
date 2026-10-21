# 🔐 Seguridad - Gestión de Pedidos (UFG)

## 1. Superficie y Amenazas Principales

| Activo | Amenaza | Control |
|---|---|---|
| Base de datos | Inyección SQL | Solo EF Core/LINQ y consultas parametrizadas; el procedimiento almacenado se invoca con parámetros; prohibido concatenar SQL |
| Credenciales de BD | Fuga en el repositorio o en el ejecutable | Secretos por variables de entorno; ni el escritorio ni la web (la `Ui`) tienen cadena de conexión |
| Cuentas de usuario | Fuerza bruta, contraseñas débiles | Bloqueo temporal, política de contraseña, hash PBKDF2, mensaje de error genérico |
| Web pública | Registros masivos, enumeración de cuentas, scraping del catálogo | *Rate limiting* por IP (`AddRateLimiter`); login con mensajes genéricos (el registro sí avisa si el correo existe, mitigado con el límite de peticiones); catálogo sin datos internos |
| Cuenta web sobre un cliente ajeno | Registrarse con el correo de otra persona para ver su historial | El enlace con un cliente existente exige confirmar el **teléfono registrado**, con límite de intentos y registro en el historial |
| Datos de otros clientes | Acceso a pedidos ajenos (IDOR) | Los endpoints de cliente filtran por el cliente del token, nunca por un id recibido; prueba obligatoria |
| API | Acceso sin sesión / sin permiso | JWT obligatorio, autorización por permisos en cada endpoint |
| Datos de clientes | Exposición innecesaria | DTOs mínimos, sin datos personales en logs |
| Pedidos / stock | Carreras y doble envío | Concurrencia optimista (`RowVersion`), transacciones |
| Historial | Alteración | Usuario de BD sin `UPDATE/DELETE` sobre `Actividades` |
| Contenedores | Escalada | Usuario no root, imágenes oficiales con etiqueta fija |
| Dependencias | Paquetes vulnerables | `dotnet list package --vulnerable` en CI, Dependabot |

## 2. Reglas de Código Seguro

* **Nunca confiar en el cliente**: se valida y autoriza en la API aunque la interfaz ya valide.
* **Sin `sa`** en la aplicación ([BaseDatos.md §7](BaseDatos.md)).
* Errores hacia el cliente con **Problem Details** genérico; el detalle técnico solo va a logs.
* HTTPS obligatorio fuera de desarrollo (TLS en Traefik/Dokploy); cabeceras básicas (`X-Content-Type-Options`, `Referrer-Policy`).
* OpenAPI/Swagger solo en `Development`.
* **CORS**: ni el escritorio (MAUI) ni la web (render de servidor) llaman a la API desde un navegador, así que no hace falta. Si se usara WebAssembly, se abriría CORS solo para su origen.
* Registrar en `Actividades` los eventos de seguridad relevantes (login, login fallido, cambio de rol, baja de usuario).

## 3. Conexiones Seguras a la Base de Datos (criterio de la rúbrica)

1. La API se conecta con `pedidos_app` (mínimo privilegio) y las migraciones con `pedidos_migrator` ([BaseDatos.md §7](BaseDatos.md)).
2. Cadena de conexión con **`Encrypt=True`**. En desarrollo, el contenedor usa un certificado autofirmado, por lo que se añade `TrustServerCertificate=True` **solo** en el perfil `Development`. En el servidor se usa un certificado confiable o la red interna de Docker sin puerto expuesto.
3. La cadena de conexión viene de variables de entorno (`ConnectionStrings__Default`), nunca del código ni del repositorio.
4. Todas las consultas son **parametrizadas** (EF Core genera `sp_executesql`; el procedimiento recibe parámetros).
5. El puerto de SQL Server no se publica hacia la red (`127.0.0.1` en local, ninguno en el servidor).

## 4. Autenticación

Detalle en [AutenticacionYPermisos.md](Especificaciones/AutenticacionYPermisos.md). Resumen:

* **Contraseñas**: `PasswordHasher<T>` (PBKDF2 con sal e iteraciones del framework); mínimo 10 caracteres; nunca en claro, ni en logs, ni en el historial.
* **Login**: usuario o correo + contraseña → un **JWT de 60 minutos** (sin *refresh tokens*; al vencer se vuelve a iniciar sesión). Mensaje genérico ("Usuario o contraseña incorrectos") y trabajo de hash equivalente aunque el usuario no exista (evita enumeración por tiempos). La API comprueba en cada petición que el usuario siga **activo** (caché de 60 s), así una baja surte efecto en ≤ 1 minuto.
* **Bloqueo**: 5 intentos fallidos consecutivos → bloqueo de 15 minutos.
* **Sesión del escritorio**: el token vive **solo en memoria**; no se escribe a disco. **Sesión de la web**: el token se guarda en `ProtectedSessionStorage` (cifrado por ASP.NET Core, ligado a la pestaña); no se usa `localStorage`. Cerrar sesión lo descarta.
* **Límite de peticiones**: *rate limiting* por IP en `login` (p. ej. 10/min), `registro` (p. ej. 5/hora) y creación de pedidos; respuesta `429`.
* **Primer ingreso**: el administrador inicial (clave desde `ADMIN_INITIAL_PASSWORD`) debe cambiar su contraseña.
* **Firma JWT**: clave simétrica (`JWT_SIGNING_KEY`, ≥ 256 bits) desde variable de entorno.

## 5. Autorización

* **RBAC con permisos**: el rol agrupa permisos (`clientes.crear`, `pedidos.desactivar`…) y la API exige el permiso del endpoint ([AutenticacionYPermisos.md §3](Especificaciones/AutenticacionYPermisos.md)).
* La interfaz oculta menús y botones según los permisos del token, pero **eso es solo comodidad**: la barrera real es la API.
* Un usuario no puede modificar su propio rol ni desactivarse a sí mismo; siempre debe existir al menos un administrador activo.

## 6. Gestión de Secretos

* `.env.example` sin valores reales; `.env` en `.gitignore`.
* Local: `.env` + Compose. Servidor: variables de Dokploy.
* Secretos de desarrollo distintos de los de producción.
* CI con escaneo de secretos (Gitleaks) en cada push ([GuiaCI.md](Equipo/GuiaCI.md)).

## 7. Datos Personales

Clientes y usuarios contienen datos personales (nombre, teléfono, dirección, correo). Principios: **minimización** (solo lo que pide el enunciado), **finalidad** clara y acceso restringido por permisos. En la defensa se usan **datos ficticios**. Marco en [BaseLegal.md](BaseLegal.md).
