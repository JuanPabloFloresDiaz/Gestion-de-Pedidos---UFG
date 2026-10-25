using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Comun.Documentos;
using GestionPedidos.Ui.Comun.Notificaciones;
using GestionPedidos.Ui.Modulos.Usuarios;

namespace GestionPedidos.Ui.Tests;

public class UsuariosTests
{
    private static UsuarioDto Usuario(int id, bool activo = true) =>
        new(id, $"u{id}", $"Usuario {id}", $"u{id}@e.com", 2, "Operador", activo, false, null, "AAAA");

    private static (Entorno entorno, UsuariosService servicio) Crear()
    {
        var e = new Entorno();
        e.Manejador.Responder = r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath.EndsWith("/roles")
            ? (HttpStatusCode.OK, new[] { new RolDto(1, "Administrador", null, Array.Empty<string>()), new RolDto(2, "Operador", null, Array.Empty<string>()) })
            : r.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, new PaginaDto<UsuarioDto>(new[] { Usuario(1), Usuario(2) }, 2, 1, 10))
                : (HttpStatusCode.OK, Usuario(3));
        return (e, new UsuariosService(e.Api));
    }

    // ---- Servicio (publicadora) ----
    [Fact]
    public async Task Servicio_PublicaEntidadCambiada_SoloTrasConfirmarLaApi()
    {
        var (e, servicio) = Crear();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (s, a) => { Assert.Same(servicio, s); recibidos.Add(a); };

        await servicio.CrearAsync(new CrearUsuarioSolicitud("nuevo", "Nuevo", "n@e.com", 2, "ClaveTemporal_2026"), default);
        var ev = Assert.Single(recibidos);
        Assert.Equal(TipoEntidad.Usuario, ev.Entidad);
        Assert.Equal(TipoCambio.Registrada, ev.Tipo);

        e.Manejador.Responder = _ => (HttpStatusCode.UnprocessableEntity, new { title = "Debe quedar al menos un administrador activo.", codigo = "REGLA_DE_NEGOCIO" });
        await Assert.ThrowsAsync<GestionPedidos.Ui.Comun.Api.ApiException>(() => servicio.CambiarEstadoAsync(1, false, default));
        Assert.Single(recibidos);   // el fallo NO notificó nada
    }

    [Fact]
    public async Task Servicio_SinSuscriptores_NoFalla()
    {
        var (_, servicio) = Crear();
        await servicio.CambiarEstadoAsync(2, false, default);
    }

    // ---- Notificador (suscriptor) ----
    [Fact]
    public async Task Notificador_MuestraUnAvisoPorCadaCambio_YDejaDeEscucharAlHacerDispose()
    {
        var (_, servicio) = Crear();
        var avisos = new AvisosService();
        var textos = new List<string>();
        avisos.Mostrado += (_, a) => textos.Add(a.Texto);
        var notificador = new Notificador(new IPublicadorDeCambios[] { servicio }, avisos);

        await servicio.CrearAsync(new CrearUsuarioSolicitud("nuevo", "Nuevo", "n@e.com", 2, "ClaveTemporal_2026"), default);
        Assert.Single(textos);
        Assert.Contains("registrado", textos[0]);

        notificador.Dispose();   // par del +=
        await servicio.CambiarEstadoAsync(2, false, default);
        Assert.Single(textos);   // ya no recibe eventos
    }

    // ---- ViewModel ----
    [Fact]
    public async Task ViewModel_Iniciar_CargaRolesYPrimeraPagina()
    {
        var (_, servicio) = Crear();
        using var vm = new UsuariosViewModel(servicio);
        await vm.IniciarAsync();
        Assert.Equal(2, vm.Roles.Count);
        Assert.Equal(2, vm.Pagina!.Total);
    }

    [Fact]
    public async Task ViewModel_Guardar_ConDatosInvalidos_MuestraErroresPorCampo_SinLlamarALaApi()
    {
        var (e, servicio) = Crear();
        using var vm = new UsuariosViewModel(servicio);
        await vm.IniciarAsync();
        var antes = e.Manejador.Peticiones.Count;
        vm.Nuevo();
        vm.NombreUsuario = "ab"; vm.NombreCompleto = ""; vm.Correo = "mal"; vm.ClaveInicial = "corta";
        await vm.GuardarAsync();
        Assert.NotNull(vm.ErrorDe(nameof(vm.NombreUsuario)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.NombreCompleto)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.Correo)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.ClaveInicial)));
        Assert.Equal(antes, e.Manejador.Peticiones.Count);
        Assert.True(vm.Editando);
    }

    [Fact]
    public async Task ViewModel_GuardarValido_CierraElPanel_YRefrescaElListadoPorElEvento()
    {
        var (e, servicio) = Crear();
        using var vm = new UsuariosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        vm.NombreUsuario = "nuevo"; vm.NombreCompleto = "Persona Nueva"; vm.Correo = "nuevo@e.com"; vm.ClaveInicial = "ClaveTemporal_2026";
        var gets = () => e.Manejador.Peticiones.Count(p => p.Method == HttpMethod.Get);
        var antes = gets();
        await vm.GuardarAsync();
        await Task.Delay(100);   // el refresco ocurre en el manejador del evento
        Assert.False(vm.Editando);
        Assert.True(gets() > antes);   // se volvió a consultar el listado
    }

    [Fact]
    public async Task ViewModel_AlHacerDispose_SeDaDeBajaDelEvento()
    {
        var (e, servicio) = Crear();
        var vm = new UsuariosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Dispose();
        var antes = e.Manejador.Peticiones.Count;
        await servicio.CambiarEstadoAsync(2, false, default);   // publica EntidadCambiada
        await Task.Delay(100);
        Assert.Equal(antes + 1, e.Manejador.Peticiones.Count);   // solo el PATCH: el VM ya no recarga
    }

    [Fact]
    public async Task ViewModel_Editar_PrecargaLosDatos_YCancelarCierraSinGuardar()
    {
        var (_, servicio) = Crear();
        using var vm = new UsuariosViewModel(servicio);
        vm.Editar(Usuario(7));
        Assert.True(vm.Editando);
        Assert.Equal("Usuario 7", vm.NombreCompleto);
        vm.Cancelar();
        Assert.False(vm.Editando);
        await Task.CompletedTask;
    }

    // ---- Documentos (pestañas) ----
    [Fact]
    public void Documentos_AbrirDosVecesElMismoModulo_NoDuplicaLaPestana_YPublicaLosEventos()
    {
        var docs = new DocumentosService();
        var log = new List<string>();
        docs.DocumentoAbierto += (_, e) => log.Add("abierto:" + e.Clave);
        docs.DocumentoActivado += (_, e) => log.Add("activado:" + e.Clave);
        docs.DocumentoCerrado += (_, e) => log.Add("cerrado:" + e.Clave);

        var usuarios = new Documento("usuarios", "Usuarios", typeof(object));
        var ayuda = new Documento("integrantes", "Integrantes", typeof(object));
        docs.Abrir(usuarios); docs.Abrir(ayuda); docs.Abrir(usuarios);   // el tercero solo activa

        Assert.Equal(2, docs.Abiertos.Count);
        Assert.Equal(usuarios, docs.Activo);
        docs.Cerrar("usuarios");
        Assert.Equal(ayuda, docs.Activo);   // al cerrar, se activa la vecina
        Assert.Contains("cerrado:usuarios", log);
        Assert.Equal(1, log.Count(l => l == "abierto:usuarios"));
    }
}

public class UsuariosViewModelValidacionTests
{
    [Fact]
    public async Task ViewModel_AlCorregirUnCampo_SuErrorDesaparece_SinEsperarAGuardar()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => (System.Net.HttpStatusCode.OK, new GestionPedidos.Contracts.PaginaDto<GestionPedidos.Contracts.UsuarioDto>(Array.Empty<GestionPedidos.Contracts.UsuarioDto>(), 0, 1, 10));
        using var vm = new GestionPedidos.Ui.Modulos.Usuarios.UsuariosViewModel(new GestionPedidos.Ui.Modulos.Usuarios.UsuariosService(e.Api));
        vm.Nuevo();
        await vm.GuardarAsync();                                  // todo vacío: salen los errores
        Assert.NotNull(vm.ErrorDe(nameof(vm.Correo)));
        vm.Correo = "ana@ejemplo.com";                            // la persona lo corrige
        Assert.Null(vm.ErrorDe(nameof(vm.Correo)));               // el aviso rojo desaparece al instante
        Assert.NotNull(vm.ErrorDe(nameof(vm.NombreCompleto)));    // los demás siguen hasta que los corrija
    }
}
