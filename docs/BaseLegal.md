# ⚖️ Glosario y Base Legal - Gestión de Pedidos (UFG)

## 1. Marco Legal y Licencias

* **Protección de datos personales (El Salvador)**: el sistema trata datos de clientes (nombre, contacto, dirección). Se aplican los principios de consentimiento, finalidad, minimización y seguridad de la **Ley de Protección de Datos Personales de El Salvador (2024)**. Al ser un proyecto académico con datos ficticios, no hay tratamiento real; el diseño ([Seguridad.md §5](Seguridad.md)) se mantiene para que sea trasladable a un caso real.
* **Licencias de software**:
  - **.NET, ASP.NET Core, EF Core, Blazor, .NET MAUI**: MIT (uso libre).
  - **SQL Server Developer**: gratuita **solo para desarrollo y pruebas**; producción exige licencia o la edición Express (con límites de tamaño/recursos). Verificar los términos vigentes de Microsoft antes de desplegar.
  - Librerías de terceros (Testcontainers, xUnit, Playwright…): revisar la licencia de cada paquete antes de añadirlo (algunas populares han cambiado a licencias comerciales; por eso se evita MediatR/AutoMapper, [Tecnologia.md §2.2](Tecnologia.md)).
* **Propiedad intelectual**: el repositorio ya incluye `LICENSE`; confirmar con el ingeniero si la materia impone condiciones sobre la entrega.

> Este documento fija criterios técnicos; no reemplaza asesoría legal.

## 2. Glosario Técnico

| Término | Significado |
|---|---|
| **SDD** | *Spec Driven Documentation/Development*: especificar y definir pruebas antes de implementar |
| **POE** | Programación Orientada a Eventos (la materia) |
| **Evento** | Hecho ocurrido que otras partes pueden observar y reaccionar |
| **Evento (C#)** | Mecanismo con el que una clase publicadora avisa a suscriptores; se declara con un delegado y la palabra `event` |
| **Agregado** | Grupo de objetos tratados como una unidad de consistencia, con una raíz |
| **Objeto de valor** | Objeto sin identidad definido por sus atributos (`Dinero`) |
| **Caso de uso** | Operación de aplicación que orquesta dominio y puertos |
| **Puerto / Adaptador** | Interfaz que define la aplicación / su implementación en infraestructura |
| **Clean Architecture** | Capas con dependencias que apuntan hacia el dominio |
| **ORM** | Mapeador objeto-relacional (Entity Framework Core) |
| **Migración** | Cambio de esquema versionado generado por EF Core |
| **Blazor** | Marco de .NET para construir interfaces con componentes Razor |
| **Razor Class Library (RCL)** | Biblioteca de componentes reutilizable por varios proyectos |
| **Blazor Hybrid / .NET MAUI** | Componentes Blazor dentro de una aplicación nativa (`BlazorWebView`); aquí, el escritorio de Windows |
| **ViewModel** | Clase con el estado y la lógica de una pantalla, sin referencias a la interfaz |
| **Interfaz de múltiples documentos** | Varios documentos o módulos abiertos a la vez; aquí, mediante pestañas |
| **EventCallback** | Delegado de Blazor para que un componente hijo avise a su padre |
| **Delegado** | Tipo que referencia métodos con una firma concreta |
| **JWT** | Token de sesión firmado y de corta duración (60 minutos en este proyecto) |
| **Blazor Web App** | Plantilla de .NET 8 para aplicaciones web con componentes Razor y modos de render de servidor, WebAssembly o automático |
| **RBAC** | Control de acceso basado en roles (aquí con permisos por rol) |
| **Baja lógica** | Marcar un registro como inactivo en lugar de borrarlo |
| **Procedimiento almacenado** | Rutina SQL guardada en la base de datos que recibe parámetros |
| **Concurrencia optimista** | Detectar conflictos al guardar (`rowversion`) en vez de bloquear |
| **ACID** | Atomicidad, Consistencia, Aislamiento, Durabilidad (garantías transaccionales) |
| **SOLID / DRY / KISS / YAGNI** | Ver [Principios.md](Principios.md) |
| **Problem Details** | Formato estándar de errores HTTP (RFC 9457) |
| **Testcontainers** | Librería que levanta contenedores efímeros para pruebas de integración |
| **Dokploy** | Plataforma de despliegue sobre Docker (Swarm + Traefik) usada en el ecosistema |
