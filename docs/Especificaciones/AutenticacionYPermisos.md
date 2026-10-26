# 🔑 Autenticación, Roles y Permisos

> RF-08 y parte de RF-07 y RF-11. Se implementa en la **Fase 1**. Tablas en [BaseDatos.md §4.1](../BaseDatos.md); amenazas en [Seguridad.md](../Seguridad.md).

## 1. Roles

| Rol | Cliente que lo usa | Propósito |
|---|---|---|
| **Administrador** | Escritorio | Gestión completa, incluida la administración de usuarios y la consulta del historial |
| **Operador** | Escritorio | Trabajo diario con clientes, productos (consulta) y pedidos |
| **Cliente** | Web pública | Ver el catálogo, administrar su perfil y hacer y consultar **sus propios** pedidos |

* Cada usuario tiene **un** rol.
* El rol `Cliente` **no puede entrar a la administración**: la página de login del hospedaje de escritorio rechaza a quien no tenga el permiso `admin.acceso` (§3), y en la web el área de administración exige ese permiso.
* Cada usuario con rol `Cliente` está enlazado a un registro de `Clientes` mediante `Clientes.UsuarioId`. Los clientes que registra un operador no tienen cuenta (`UsuarioId` nulo) hasta que se registren ellos mismos en la web con su correo y **confirmen su teléfono** ([PortalWeb.md §3](PortalWeb.md)).

## 2. Autenticación (un solo JWT de 60 minutos)

```mermaid
sequenceDiagram
    participant C as Cliente (Ui en Desktop o Web)
    participant A as API /api/v1/auth
    participant DB as SQL Server
    C->>A: POST /login {identificador, clave}
    A->>DB: buscar usuario (activo, no bloqueado)
    A->>A: verificar hash (siempre se hace el trabajo de hash)
    alt credenciales válidas
        A->>DB: reiniciar intentos, registrar último acceso
        A-->>C: 200 {accessToken, expiraEn, debeCambiarClave}
    else inválidas
        A->>DB: sumar intento (5 → bloqueo 15 min)
        A-->>C: 401 "Usuario o contraseña incorrectos"
    end
    Note over C: guarda el token SOLO en memoria
    C->>A: peticiones con Authorization: Bearer
    Note over C: al acercarse a los 60 min avisa; al vencer vuelve al login
```

| Aspecto | Decisión |
|---|---|
| Identificador | Nombre de usuario **o** correo |
| Token | JWT firmado (HS256, clave ≥ 256 bits desde variable de entorno), **60 minutos**. Claims: `sub`, `name`, `role`, `permisos` (lista de códigos) |
| *Refresh tokens* | **No hay**: al vencer el token se vuelve a iniciar sesión |
| Logout | El cliente descarta el token (no hay estado en el servidor) |
| Usuario dado de baja o con rol cambiado | La API comprueba en cada petición que el usuario siga **activo** (consulta con caché en memoria de 60 s), así la baja surte efecto en ≤ 1 minuto y no en 60 |
| Bloqueo | 5 fallos seguidos → `BloqueadoHasta = ahora + 15 min`; un login correcto reinicia el contador |
| Contraseña | Mínimo 10 y máximo 128 caracteres, distinta del nombre de usuario y del correo; hash con `PasswordHasher<T>` |
| Cambio obligatorio | Con `DebeCambiarClave = 1` solo se permite `POST /auth/cambiar-clave` hasta cambiarla |
| Límite de peticiones | *Rate limiting* por IP en `login` y `registro` ([Seguridad.md §4](../Seguridad.md)) |

### Endpoints

| Método y ruta | Descripción | Auth |
|---|---|---|
| `POST /api/v1/auth/login` | Inicia sesión | Anónimo |
| `POST /api/v1/auth/registro` | Registro de un cliente desde la web: crea el usuario (rol `Cliente`) y su registro en `Clientes` en **una transacción**. Respuestas: `409 CORREO_EN_USO` (ya hay cuenta) o `409 CLIENTE_EXISTENTE` (hay un cliente con ese correo; se reenvía con el teléfono de confirmación para enlazarlo) | Anónimo |
| `POST /api/v1/auth/cambiar-clave` | Cambia la contraseña propia | Sí |
| `GET /api/v1/auth/yo` | Usuario actual y permisos | Sí |

## 3. Permisos (catálogo y matriz)

> Matriz **confirmada** por el equipo el 03/10/2026; para ajustarla basta cambiar la semilla.

| Permiso | Administrador | Operador | Cliente |
|---|:-:|:-:|:-:|
| `admin.acceso` (entrar a la Administración) | ✅ | ✅ | ❌ |
| `clientes.ver` / `.crear` / `.editar` | ✅ | ✅ | ❌ |
| `clientes.desactivar` (baja y reactivación) | ✅ | ❌ | ❌ |
| `productos.ver` | ✅ | ✅ | ❌ |
| `productos.crear` / `.editar` / `.desactivar` | ✅ | ❌ | ❌ |
| `pedidos.ver` / `.crear` / `.editar` / `.cambiar_estado` | ✅ | ✅ | ❌ |
| `pedidos.desactivar` | ✅ | ❌ | ❌ |
| `usuarios.ver` / `.crear` / `.editar` / `.desactivar` | ✅ | ❌ | ❌ |
| `usuarios.eliminar` (eliminación definitiva) | ✅ | ❌ | ❌ |
| `roles.ver` / `roles.editar` (gestionar los permisos de cada rol) | ✅ | ❌ | ❌ |
| `catalogos.ver` (ciudades y departamentos) | ✅ | ✅ | ❌ |
| `categorias.ver` (categorías, subcategorías, filtros y opciones) | ✅ | ✅ | ❌ |
| `categorias.crear` / `.editar` / `.desactivar` / `.eliminar` | ✅ | ❌ | ❌ |
| `historial.ver` | ✅ | ❌ | ❌ |
| `catalogo.ver` (además es **público**, sin sesión) | ✅ | ✅ | ✅ |
| `mispedidos.ver` / `.crear` / `.cancelar` (solo los propios) | ❌ | ❌ | ✅ |
| `perfil.editar` (solo el propio) | ❌ | ❌ | ✅ |

* Historial y gestión de usuarios: solo el administrador.
* **Los permisos de cada rol se pueden cambiar desde la pantalla "Roles y permisos"** (`PUT /api/v1/roles/{id}/permisos`, lista completa). Hay reglas que protegen al sistema y que la API aplica siempre (`PermisosProtegidos.ParaRol`): el Administrador no puede perder `admin.acceso`, `usuarios.*`, `roles.ver/editar` (se quedaría sin poder administrar); el Cliente no puede recibir permisos de administración; Operador y Cliente no reciben permisos reservados a otro rol. Los permisos nuevos se añaden **al final** del catálogo (el id de la semilla es la posición). Los cambios rigen cuando cada persona vuelve a iniciar sesión (los permisos viajan en el token).
* En la administración, lo que cambia entre administrador y operador son las **acciones** disponibles (botones y menús) según el permiso: "vistas diferenciadas por rol" (RF-07).
* Un cliente **nunca** puede ver ni tocar datos de otro cliente: los endpoints `mispedidos` y `perfil` filtran por el `Cliente` enlazado al usuario del token, no por un identificador enviado por quien llama.

## 4. Autorización en la API

* Cada endpoint declara el permiso que exige (`RequirePermission("clientes.crear")`) mediante una política que lee la claim `permisos`; los endpoints públicos se marcan explícitamente como anónimos.
* Sin token → `401`; con token sin el permiso → `403`.
* Prueba obligatoria por endpoint: sin sesión (401), con un rol sin permiso (403), con permiso (éxito) y, en los de cliente, **acceso a datos ajenos denegado**.

## 5. Reglas de Usuarios

* Alta de usuarios del personal por un administrador (clave inicial temporal con `DebeCambiarClave = 1`).
* **Dar de baja es un eliminado suave y siempre reversible**: `Activo = 0` y `EliminadoEn` con la fecha de baja; reactivar limpia `EliminadoEn`, el contador de intentos fallidos y el bloqueo. La misma regla (`EliminadoEn`) existe en `Clientes`, `Productos` y `Pedidos`.
* **Eliminación definitiva** (`DELETE /api/v1/usuarios/{id}`, permiso `usuarios.eliminar`): solo de usuarios **ya dados de baja**, nunca de uno mismo ni del último administrador, y se rechaza si el usuario tiene pedidos (`409`). En la interfaz exige escribir el nombre de usuario. Al borrar, el historial y el cliente enlazado conservan sus datos (`UsuarioId` pasa a nulo).
* **Siempre debe existir al menos un administrador activo**: se impide desactivar o degradar al último.
* Un usuario no puede cambiar su propio rol ni desactivarse a sí mismo.
* Nombre de usuario y correo únicos; el registro web usa el correo como nombre de usuario.
* Un usuario con rol `Cliente` solo puede enlazarse a un cliente (`Clientes.UsuarioId` único).

## 6. Interfaz

* Página de login (`/ingresar`); tras el éxito se abre el shell con el menú armado con los **permisos** del token ([InterfazUsuario.md](InterfazUsuario.md)).
* **5 minutos antes** de que venza el token, `SesionService` publica `SesionPorExpirar` y la interfaz avisa para que se guarde el trabajo; al vencer (`SesionExpirada`) se cierran las pestañas y se vuelve al login.

## 7. Plan de Pruebas

* Unitarias: política de contraseña, política de bloqueo, matriz de permisos.
* Integración: login correcto e incorrecto, bloqueo al 5.º fallo, usuario inactivo (la petición siguiente falla), cambio de clave obligatorio, último administrador, registro web transaccional (si falla `Clientes`, no queda el usuario).
* API: cada endpoint protegido (401/403/éxito) y acceso a datos ajenos denegado.
* ViewModels: `LoginViewModel` (muestra errores, deshabilita el botón mientras carga, en Desktop rechaza roles sin `admin.acceso`).
