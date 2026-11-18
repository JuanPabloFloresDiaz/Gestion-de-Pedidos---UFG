# Correos

## 1. Objetivo
Avisar por correo electrónico de los hechos que la persona afectada debe conocer. En desarrollo **no se envía nada real**: todo va a **MailHog**, un servidor SMTP de pruebas con bandeja web. Para producción basta cambiar variables de entorno por las de un SMTP real, **sin tocar código**.

## 2. Correos que envía el sistema
| Correo | Cuándo | Destinatario | Contenido |
|---|---|---|---|
| **Baja de cuenta** | Se da de baja a un usuario (`PATCH /usuarios/{id}/estado` con `Activo=false`) | El usuario dado de baja | Que su cuenta fue dada de baja y la **justificación** escrita por quien la dio |
| **Comprobante de pedido** | Un pedido pasa a `Entregado` (`PATCH /pedidos/{id}/estado`) | El correo del cliente (si no tiene, no se envía: es opcional en Clientes) | Detalle y total del pedido, y la invitación a entrar a **Mis pedidos** y **confirmar que lo recibió** |

**Confirmar recibido** (`PATCH /api/v1/mis-pedidos/{id}/confirmar`, permiso `mispedidos.ver`): el cliente confirma desde la tienda un pedido **propio, `Entregado` y aún no confirmado** (`422` si no está entregado o ya se confirmó; `404` si es ajeno). Guarda `Pedidos.ConfirmadoPorClienteEn` (migración `AgregarConfirmacionDePedido`), queda en el historial (`ConfirmarRecepcion`) y `PedidoClienteDto` trae `ConfirmadoEn` y `PuedeConfirmar`.

## 3. Diseño (patrón delegado + evento, igual que el historial)
```
CambiarEstadoUsuario ──(tras guardar)──▶ EventosAplicacion.UsuarioDadoDeBaja
                                              │
                                   CorreoSubscriber  (suscrito con +=, se da de baja con -= en Dispose)
                                              │  arma el correo con PlantillasCorreo y lo encola
                                              ▼
                                   IColaDeCorreos ──▶ ColaDeCorreosEnSegundoPlano ──▶ IEnviadorDeCorreo (SMTP)
```
* El caso de uso **no conoce** el correo: solo publica el evento **después** de confirmar la operación.
* El envío es **en segundo plano**: si el servidor de correo está caído, se registra el error y **la operación del usuario no falla ni se demora**.
* Todo dato escrito por una persona (nombre, motivo) se **escapa para HTML** antes de ir en el correo.
* Nunca se registra en el log el destinatario ni el cuerpo; solo el asunto.

## 4. Configuración (sección `Correo`)
| Variable (`.env`) | Por defecto | Para qué |
|---|---|---|
| `CORREO_HABILITADO` | `true` | `false` descarta los correos (queda un aviso en el log) |
| `CORREO_HOST` / `CORREO_PUERTO` | `mailhog` / `1025` | Servidor SMTP |
| `CORREO_USAR_SSL` | `false` | `true` con un proveedor real |
| `CORREO_USUARIO` / `CORREO_CLAVE` | vacío | Credenciales SMTP (MailHog no las pide) |
| `CORREO_REMITENTE` / `CORREO_NOMBRE_REMITENTE` | `no-responder@novixtore.local` / `NoviXtore` | Quién firma el correo |
| `CORREO_URL_SITIO` | `http://localhost:4392` | Dirección pública de la web (sin barra final): el comprobante enlaza a `…/mis-pedidos` |
| `MAILHOG_UI_PORT` | `4394` | Bandeja web de MailHog: <http://localhost:4394> |

## 5. Plan de pruebas
* **Aplicación**: la baja exige justificación (nula, vacía, corta o de más de 500 caracteres → `Validacion`); publica el evento con el motivo recortado y lo guarda en el historial; reactivar no pide motivo ni envía correo.
* **Suscriptor**: la baja encola un correo al destinatario con el motivo; el HTML se escapa; tras `Dispose` deja de escuchar.
* **Comprobante**: se publica solo al entregar y solo si el cliente tiene correo (no al cancelar); el HTML escapa nombre y producto; incluye total, fecha de El Salvador y el enlace.
* **Confirmar recibido**: dominio (solo entregado, una vez), caso de uso (propio / ajeno / ya confirmado / concurrencia), API (pendiente `422`, ajeno `404`, doble `422`, historial) y pantalla (botón y error visibles).
* **Manual**: dar de baja a un usuario en Usuarios y ver el correo en MailHog.
