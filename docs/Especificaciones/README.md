# 📑 Especificaciones - Gestión de Pedidos (UFG)

Specs por módulo bajo **Spec Driven Documentation**: cada módulo se especifica aquí **antes** de implementarlo y declara cómo se probará.

| Spec | Requerimientos | Fase | Estado |
|---|---|---|---|
| [PatronEventos.md](PatronEventos.md) | RT-06 | Transversal | 🟡 Borrador |
| [AutenticacionYPermisos.md](AutenticacionYPermisos.md) | RF-08 | 1 | 🟡 Borrador |
| [InterfazUsuario.md](InterfazUsuario.md) | RF-01, RF-07, RF-09 | 0–1 | 🟡 Borrador |
| [Clientes.md](Clientes.md) | RF-03 | 2 | 🟡 Borrador |
| [Productos.md](Productos.md) | RF-04 | 2 | 🟡 Borrador |
| [Pedidos.md](Pedidos.md) | RF-05 | 3 | 🟡 Borrador |
| [Busquedas.md](Busquedas.md) | RF-06 | 2–4 | 🟡 Borrador |
| [HistorialActividades.md](HistorialActividades.md) | RF-10 | 4 | 🟡 Borrador |
| [PortalWeb.md](PortalWeb.md) | RF-11 | Web | 🟡 Borrador |
| [Testing.md](Testing.md) | RNF | Transversal | 🟡 Borrador |
| [RespaldoYEntrega.md](RespaldoYEntrega.md) | RE-02 a RE-04 | 5 | 🟡 Borrador |

Documentos para el equipo (no son specs de módulo): [PrimerosPasos.md](../Equipo/PrimerosPasos.md), [PautasCodigoEventos.md](../Equipo/PautasCodigoEventos.md), [Tareas.md](../Equipo/Tareas.md).

## Plantilla de una Spec de Módulo

```markdown
# <Módulo>
## 1. Objetivo y requerimientos que cubre (RF-xx)
## 2. Modelo (campos, reglas, invariantes, estados)
## 3. Casos de uso (entrada, resultado, errores)
## 4. API (rutas, permisos, códigos de estado)
## 5. Eventos que emite/consume
## 6. Interfaz (página, eventos)
## 7. Plan de pruebas (dominio / integración / API / ViewModel)
## 8. Decisiones abiertas
```
