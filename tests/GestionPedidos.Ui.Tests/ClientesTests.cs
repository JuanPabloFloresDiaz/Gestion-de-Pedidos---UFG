using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Modulos.Clientes;

namespace GestionPedidos.Ui.Tests;

/// <summary>Servicio y ViewModel de Clientes ([Clientes.md §7]): guardar válido e inválido, listas dependientes y eventos.</summary>
public class ClientesTests
{
    private static ClienteDto Cliente(int id, bool activo = true, string version = "AAAA") =>
        new(id, "Ana", "López", "Ana López", "7123-4567", "Calle 1", "ana@e.com", 10, "San Salvador", 1, "San Salvador", false, activo,
            activo ? null : DateTimeOffset.UtcNow, version);

    /// <summary>Entorno con una API simulada. <paramref name="alActualizar"/> permite programar la respuesta del PUT.</summary>
    private static (Entorno entorno, ClientesService servicio) Crear(Func<HttpRequestMessage, (HttpStatusCode, object?)>? alActualizar = null)
    {
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/departamentos")) return (HttpStatusCode.OK, new[] { new DepartamentoDto(1, "SS", "San Salvador", 19), new DepartamentoDto(2, "LL", "La Libertad", 22) });
            if (ruta.EndsWith("/ciudades")) return (HttpStatusCode.OK, new[] { new CiudadDto(10, 1, "San Salvador", "San Salvador", "San Salvador") });
            if (r.Method == HttpMethod.Put && alActualizar is not null) return alActualizar(r);
            if (r.Method == HttpMethod.Get && ruta.Contains("/clientes/")) return (HttpStatusCode.OK, Cliente(1, version: "NUEVA"));
            if (r.Method == HttpMethod.Get) return (HttpStatusCode.OK, new PaginaDto<ClienteDto>(new[] { Cliente(1), Cliente(2, activo: false) }, 2, 1, 10));
            return (HttpStatusCode.OK, Cliente(3));
        };
        return (e, new ClientesService(e.Api));
    }

    private static void LlenarValido(ClientesViewModel vm)
    {
        vm.Nombres = "Ana"; vm.Apellidos = "López"; vm.Telefono = "7123-4567"; vm.Direccion = "Calle 1"; vm.Correo = "ana@e.com";
        vm.DepartamentoId = 1; vm.CiudadId = 10;
    }

    // ---- Servicio: publica EntidadCambiada solo tras confirmar la API ----

    [Fact]
    public async Task Servicio_PublicaElEventoCorrectoEnCadaOperacion()
    {
        var (_, servicio) = Crear();
        var tipos = new List<TipoCambio>();
        servicio.EntidadCambiada += (_, ev) => { tipos.Add(ev.Tipo); Assert.Equal(TipoEntidad.Cliente, ev.Entidad); };

        await servicio.CrearAsync(new CrearClienteSolicitud("A", "B", "71234567", "d", null, 10), default);
        await servicio.ActualizarAsync(1, new ActualizarClienteSolicitud("A", "B", "71234567", "d", null, 10, "AAAA"), default);
        await servicio.CambiarEstadoAsync(1, false, default);

        Assert.Equal(new[] { TipoCambio.Registrada, TipoCambio.Actualizada, TipoCambio.EstadoCambiado }, tipos);
    }

    [Fact]
    public async Task Servicio_NoPublicaSiLaApiRechaza()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => (HttpStatusCode.UnprocessableEntity, new { title = "No se puede", codigo = "REGLA_NEGOCIO", status = 422 });
        var servicio = new ClientesService(e.Api);
        var publicados = 0;
        servicio.EntidadCambiada += (_, _) => publicados++;

        await Assert.ThrowsAnyAsync<Exception>(() => servicio.CambiarEstadoAsync(1, false, default));
        Assert.Equal(0, publicados);
    }

    // ---- ViewModel ----

    [Fact]
    public async Task ViewModel_Iniciar_CargaDepartamentosYLaPrimeraPagina()
    {
        var (_, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);

        await vm.IniciarAsync();

        Assert.Equal(2, vm.Departamentos.Count);
        Assert.Equal(2, vm.Pagina!.Total);
        Assert.True(vm.FiltroActivo);   // por defecto solo activos
    }

    [Fact]
    public async Task ViewModel_Guardar_ConDatosInvalidos_MuestraErroresPorCampo_SinLlamarALaApi()
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);
        await vm.IniciarAsync();
        var antes = e.Manejador.Peticiones.Count;

        vm.Nuevo();
        vm.Nombres = ""; vm.Apellidos = " "; vm.Telefono = "--------"; vm.Direccion = ""; vm.Correo = "mal";
        await vm.GuardarAsync();

        foreach (var campo in new[] { nameof(vm.Nombres), nameof(vm.Apellidos), nameof(vm.Telefono), nameof(vm.Direccion), nameof(vm.Correo), nameof(vm.CiudadId) })
            Assert.NotNull(vm.ErrorDe(campo));
        Assert.Equal(antes, e.Manejador.Peticiones.Count);
        Assert.True(vm.Editando);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("++++++++")]
    [InlineData("7123-ABCD")]
    public async Task ViewModel_TelefonoInvalido_NoSeEnvia(string telefono)
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);
        vm.Nuevo();
        LlenarValido(vm);
        vm.Telefono = telefono;
        var antes = e.Manejador.Peticiones.Count;

        await vm.GuardarAsync();

        Assert.NotNull(vm.ErrorDe(nameof(vm.Telefono)));
        Assert.Equal(antes, e.Manejador.Peticiones.Count);
    }

    [Fact]
    public async Task ViewModel_GuardarValido_CierraElPanel_YPublicaElEvento()
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);
        await vm.IniciarAsync();
        var eventos = new List<TipoCambio>();
        servicio.EntidadCambiada += (_, ev) => eventos.Add(ev.Tipo);

        vm.Nuevo();
        LlenarValido(vm);
        await vm.GuardarAsync();

        Assert.False(vm.Editando);
        Assert.Equal(TipoCambio.Registrada, Assert.Single(eventos));
        Assert.Contains(e.Manejador.Peticiones, p => p.Method == HttpMethod.Post && p.RequestUri!.AbsolutePath.EndsWith("/clientes"));
    }

    [Fact]
    public async Task ViewModel_CambioDeDepartamento_RecargaLasCiudades_YLimpiaLaElegida()
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);
        vm.Nuevo();
        vm.CiudadId = 10;

        await vm.AlCambiarDepartamentoFormularioAsync(2);

        Assert.Null(vm.CiudadId);
        Assert.Single(vm.CiudadesFormulario);
        Assert.Contains(e.Manejador.Peticiones, p => p.RequestUri!.AbsolutePath.EndsWith("/departamentos/2/ciudades"));
    }

    [Fact]
    public async Task ViewModel_Editar_EnviaLaVersionOriginal()
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);

        await vm.EditarAsync(Cliente(1, version: "VERSION-ORIGINAL"));
        vm.Nombres = "Ana María";
        await vm.GuardarAsync();

        var indice = e.Manejador.Peticiones.FindIndex(p => p.Method == HttpMethod.Put);
        Assert.Contains("VERSION-ORIGINAL", e.Manejador.Cuerpos[indice]);
    }

    [Fact]
    public async Task ViewModel_TrasUnConflicto409_MuestraElError_YRecuperaLaVersionActual()
    {
        var (_, servicio) = Crear(_ => (HttpStatusCode.Conflict, new { title = "El cliente fue modificado por otra persona.", codigo = "CONFLICTO", status = 409 }));
        using var vm = new ClientesViewModel(servicio);

        await vm.EditarAsync(Cliente(1, version: "VIEJA"));
        await vm.GuardarAsync();

        Assert.Contains("modificado por otra persona", vm.Error);
        Assert.True(vm.Editando);                       // lo escrito no se pierde
        Assert.Equal("NUEVA", vm.Original!.RowVersion); // el siguiente intento ya usa la versión vigente
    }

    [Fact]
    public async Task ViewModel_BajaConConfirmacion_CambiaElEstado()
    {
        var (e, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);

        vm.SolicitarCambioEstado(Cliente(1));
        Assert.True(vm.HayConfirmacion);
        Assert.Equal(AccionCliente.DarDeBaja, vm.Accion);

        await vm.ConfirmarAsync();

        Assert.False(vm.HayConfirmacion);
        Assert.Contains(e.Manejador.Peticiones, p => p.Method == HttpMethod.Patch);
    }

    [Fact]
    public void ViewModel_ClienteInactivo_PideReactivar_YCancelarCierraElDialogo()
    {
        var (_, servicio) = Crear();
        using var vm = new ClientesViewModel(servicio);

        vm.SolicitarCambioEstado(Cliente(1, activo: false));
        Assert.Equal(AccionCliente.Reactivar, vm.Accion);

        vm.CancelarSolicitud();
        Assert.False(vm.HayConfirmacion);
    }

    [Fact]
    public async Task ViewModel_TrasDispose_YaNoReaccionaAlEvento()
    {
        var (e, servicio) = Crear();
        var vm = new ClientesViewModel(servicio);
        vm.Dispose();
        var antes = e.Manejador.Peticiones.Count;

        await servicio.CambiarEstadoAsync(1, false, default);

        Assert.Equal(antes + 1, e.Manejador.Peticiones.Count);   // solo la petición del cambio: no recargó el listado
    }
}
