using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Documentos;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Modulos.Roles;
using GestionPedidos.Ui.Modulos.Usuarios;

namespace GestionPedidos.Ui.Tests;

public class FaseTresTests
{
    private static UsuarioDto Usuario(int id, bool activo = true) =>
        new(id, $"u{id}", $"Usuario {id}", $"u{id}@e.com", 2, "Operador", activo, false, null, "AAAA");

    // ---- Usuarios: confirmación + eliminar ----
    [Fact]
    public void Usuarios_ElChipPideConfirmar_YSoloConfirmarLlamaALaApi()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => (HttpStatusCode.OK, Usuario(2, false));
        using var vm = new UsuariosViewModel(new UsuariosService(e.Api));

        vm.SolicitarCambioEstado(Usuario(2));
        Assert.True(vm.HayConfirmacion);
        Assert.Equal(AccionUsuario.DarDeBaja, vm.Accion);
        Assert.Empty(e.Manejador.Peticiones);          // aún no se llamó a la API

        vm.CancelarSolicitud();
        Assert.False(vm.HayConfirmacion);
        Assert.Empty(e.Manejador.Peticiones);

        vm.SolicitarCambioEstado(Usuario(2, activo: false));
        Assert.Equal(AccionUsuario.Reactivar, vm.Accion);
    }

    [Fact]
    public async Task Usuarios_EliminarDefinitivo_ExigeEscribirElNombre_YUsaDelete()
    {
        var e = new Entorno();
        e.Manejador.Responder = r => r.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, null) : (HttpStatusCode.OK, new PaginaDto<UsuarioDto>(Array.Empty<UsuarioDto>(), 0, 1, 10));
        var servicio = new UsuariosService(e.Api);
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);
        using var vm = new UsuariosViewModel(servicio);

        vm.SolicitarEliminacion(Usuario(7, activo: false));
        Assert.Equal("u7", vm.TextoAConfirmar);
        Assert.True(vm.EsPeligro);

        await vm.ConfirmarAsync();
        Assert.Contains(e.Manejador.Peticiones, p => p.Method == HttpMethod.Delete && p.RequestUri!.AbsolutePath.EndsWith("/usuarios/7"));
        Assert.Equal(TipoCambio.Eliminada, Assert.Single(cambios).Tipo);
        Assert.False(vm.HayConfirmacion);
    }

    [Fact]
    public async Task Usuarios_SiLaApiRechazaLaEliminacion_NoNotificaYMuestraElError()
    {
        var e = new Entorno();
        e.Manejador.Responder = r => r.Method == HttpMethod.Delete
            ? (HttpStatusCode.UnprocessableEntity, new { title = "Tiene pedidos asociados.", codigo = "REGLA_DE_NEGOCIO" })
            : (HttpStatusCode.OK, new PaginaDto<UsuarioDto>(Array.Empty<UsuarioDto>(), 0, 1, 10));
        var servicio = new UsuariosService(e.Api);
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);
        using var vm = new UsuariosViewModel(servicio);

        vm.SolicitarEliminacion(Usuario(7, activo: false));
        await vm.ConfirmarAsync();
        Assert.Empty(cambios);
        Assert.Contains("pedidos", vm.Error);
        Assert.False(vm.HayConfirmacion);
    }

    // ---- Roles ----
    private static (Entorno, RolesService, RolesViewModel) CrearRoles()
    {
        var e = new Entorno();
        var permisos = new[] { new PermisoDto(1, "clientes.ver", "Ver"), new PermisoDto(2, "clientes.crear", "Crear"), new PermisoDto(3, "roles.ver", "Ver roles") };
        var operador = new RolDto(2, "Operador", "d", new[] { "clientes.ver", "roles.ver" }, new[] { "roles.ver" }, Array.Empty<string>());
        e.Manejador.Responder = r => r.Method == HttpMethod.Put
            ? (HttpStatusCode.OK, new RolDto(2, "Operador", "d", new[] { "clientes.ver", "clientes.crear", "roles.ver" }))
            : r.RequestUri!.AbsolutePath.EndsWith("/permisos") ? (HttpStatusCode.OK, permisos) : (HttpStatusCode.OK, new[] { operador });
        var servicio = new RolesService(e.Api);
        return (e, servicio, new RolesViewModel(servicio));
    }

    [Fact]
    public async Task Roles_NoPermiteQuitarUnObligatorio_YDetectaCambios()
    {
        var (_, _, vm) = CrearRoles();
        await vm.IniciarAsync();
        Assert.False(vm.HayCambios);

        vm.Alternar("roles.ver", false);                 // obligatorio: se ignora
        Assert.Contains("roles.ver", vm.Marcados);
        Assert.False(vm.HayCambios);

        vm.Alternar("clientes.crear", true);
        Assert.True(vm.HayCambios);
        Assert.Equal(1, vm.Anadidos);

        vm.Descartar();
        Assert.False(vm.HayCambios);
    }

    [Fact]
    public async Task Roles_Guardar_EnviaLaListaCompleta_YPublicaElCambio()
    {
        var (e, servicio, vm) = CrearRoles();
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);

        await vm.IniciarAsync();
        vm.Alternar("clientes.crear", true);
        await vm.GuardarAsync();

        var put = e.Manejador.Peticiones.Select((p, i) => (p, i)).Single(x => x.p.Method == HttpMethod.Put);
        Assert.Contains("clientes.crear", e.Manejador.Cuerpos[put.i]);
        Assert.Equal(TipoEntidad.Rol, Assert.Single(cambios).Entidad);
        Assert.False(vm.HayCambios);
    }

    // ---- Pestañas ----
    [Fact]
    public void Documentos_NuevaPestana_ReemplazarPorInstancia_YRecargar()
    {
        var docs = new DocumentosService();
        var modulo = new Documento("usuarios", "Usuarios", typeof(object));
        docs.Abrir(modulo);

        var nueva = docs.NuevaPestana(typeof(string));
        Assert.Equal(nueva, docs.Activo);

        var segunda = docs.ReemplazarPorInstancia(nueva.Clave, modulo)!;
        Assert.Equal("Usuarios (2)", segunda.Titulo);          // el módulo ya estaba abierto: instancia numerada
        Assert.Equal(2, docs.Abiertos.Count);

        var recargados = 0;
        docs.DocumentoRecargado += (_, _) => recargados++;
        docs.Recargar(segunda.Clave);
        Assert.Equal(1, recargados);
        Assert.Equal(1, docs.Activo!.Version);
    }
}
