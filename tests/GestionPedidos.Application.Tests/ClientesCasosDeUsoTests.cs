using GestionPedidos.Application.Clientes;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

/// <summary>Repositorio de clientes en memoria (sin base de datos) para probar los casos de uso.</summary>
internal sealed class ClienteRepositoryFake : IClienteRepository
{
    private int _siguienteId = 1;
    public List<Cliente> Clientes { get; } = new();
    public HashSet<int> Ciudades { get; } = new() { 1, 2 };
    public HashSet<int> ConPedidosPendientes { get; } = new();
    public int VersionesFijadas { get; private set; }

    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct) => Task.FromResult(Clientes.FirstOrDefault(c => c.Id == id));

    public Task<(IReadOnlyList<Cliente> Elementos, int Total)> BuscarAsync(string? q, int? departamentoId, int? ciudadId, bool? activo,
        int pagina, int tamano, CancellationToken ct)
    {
        var l = Clientes.Where(c => activo is null || c.Activo == activo).ToList();
        return Task.FromResult(((IReadOnlyList<Cliente>)l.Skip((pagina - 1) * tamano).Take(tamano).ToList(), l.Count));
    }

    public Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Clientes.Any(c => string.Equals(c.Correo, correo, StringComparison.OrdinalIgnoreCase) && c.Id != exceptoId));

    public Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct) => Task.FromResult(Ciudades.Contains(ciudadId));

    public Task<bool> TienePedidosPendientesAsync(int clienteId, CancellationToken ct) => Task.FromResult(ConPedidosPendientes.Contains(clienteId));

    public void FijarVersionOriginal(Cliente cliente, byte[] version) => VersionesFijadas++;

    public void Agregar(Cliente cliente)
    {
        if (cliente.Id == 0) cliente.Id = _siguienteId++;
        Clientes.Add(cliente);
    }
}

/// <summary>Unidad de trabajo que simula que otra persona modificó el registro mientras se guardaba.</summary>
internal sealed class UnidadDeTrabajoQueChoca : IUnitOfWork
{
    public Task GuardarAsync(CancellationToken ct) => throw new ConcurrenciaException();
}

/// <summary>
/// Casos de uso de clientes ([Clientes.md §3]): validaciones, reglas de negocio, concurrencia optimista y eventos
/// (se publican una vez y solo si la operación tuvo éxito).
/// </summary>
public class ClientesCasosDeUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 15, 0, 0, TimeSpan.Zero);
    private const string VersionValida = "AAAAAAAAB9E=";   // base64 de 8 bytes, como la devuelve SQL Server

    private readonly FakeTimeProvider _reloj = new(Ahora);
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly ClienteRepositoryFake _repo = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _publicados = new();

    public ClientesCasosDeUsoTests() => _eventos.EntidadCambiada += (_, e) => _publicados.Add(e);

    private RegistrarCliente Registrar(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos);
    private ActualizarCliente Actualizar(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos);
    private CambiarEstadoCliente CambiarEstado(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos, _reloj);

    private static CrearClienteSolicitud Nuevo(string nombres = "Ana", string apellidos = "López", string telefono = "7123-4567",
        string direccion = "Calle 1", string? correo = "ana@ejemplo.com", int ciudad = 1) =>
        new(nombres, apellidos, telefono, direccion, correo, ciudad);

    private static ActualizarClienteSolicitud Edicion(string nombres = "Ana María", string? correo = "ana@ejemplo.com", string? version = VersionValida,
        string telefono = "7123-4567", int ciudad = 1) =>
        new(nombres, "López", telefono, "Calle 2", correo, ciudad, version);

    private Cliente Existente(int id = 1, string correo = "ana@ejemplo.com", bool activo = true)
    {
        var c = Cliente.Crear("Ana", "López", "7123-4567", "Calle 1", correo, 1);
        c.Id = id;
        if (!activo) c.CambiarEstado(false, Ahora.AddDays(-3));
        _repo.Clientes.Add(c);
        return c;
    }

    private static void AssertError(Resultado res, TipoError tipo)
    {
        Assert.False(res.Exito);
        Assert.Equal(tipo, res.Error!.Tipo);
    }

    // ===================== Registrar =====================

    [Fact]
    public async Task Registrar_Valido_GuardaUnaVez_YPublicaUnEvento()
    {
        var res = await Registrar().EjecutarAsync(Nuevo(), 7, default);

        Assert.True(res.Exito);
        Assert.Equal(1, _uow.Guardados);
        Assert.True(res.Valor!.Activo);
        Assert.Equal("Ana López", res.Valor.NombreCompleto);
        var ev = Assert.Single(_publicados);
        Assert.Equal(TipoCambioServidor.Registrada, ev.Tipo);
        Assert.Equal(7, ev.UsuarioId);
    }

    [Fact]
    public async Task Registrar_SinCorreo_EsValido()
    {
        var res = await Registrar().EjecutarAsync(Nuevo(correo: null), 1, default);
        Assert.True(res.Exito);
        Assert.Null(res.Valor!.Correo);
    }

    [Theory]
    [InlineData("", "López", "7123-4567", "Calle 1")]
    [InlineData("Ana", "  ", "7123-4567", "Calle 1")]
    [InlineData("Ana", "López", "7123-4567", "")]
    [InlineData("Ana", "López", "1234", "Calle 1")]           // muy corto
    [InlineData("Ana", "López", "7123-ABCD", "Calle 1")]      // letras
    [InlineData("Ana", "López", "--------", "Calle 1")]       // 8 caracteres, cero dígitos
    [InlineData("Ana", "López", "++++++++", "Calle 1")]
    public async Task Registrar_ConCamposInvalidos_Da400_SinGuardarNiPublicar(string nombres, string apellidos, string telefono, string direccion)
    {
        var res = await Registrar().EjecutarAsync(Nuevo(nombres, apellidos, telefono, direccion), 1, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Registrar_NombreMasLargoQueElMaximo_Da400()
    {
        var res = await Registrar().EjecutarAsync(Nuevo(nombres: new string('a', 101)), 1, default);
        AssertError(res, TipoError.Validacion);
    }

    [Fact]
    public async Task Registrar_CorreoConFormatoInvalido_Da400()
    {
        AssertError(await Registrar().EjecutarAsync(Nuevo(correo: "no-es-correo"), 1, default), TipoError.Validacion);
    }

    [Fact]
    public async Task Registrar_CiudadInexistente_Da422()
    {
        AssertError(await Registrar().EjecutarAsync(Nuevo(ciudad: 999), 1, default), TipoError.Regla);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Registrar_CorreoDuplicado_Da409_AunqueCambienMayusculasOEspacios()
    {
        Existente(correo: "ana@ejemplo.com");

        AssertError(await Registrar().EjecutarAsync(Nuevo(correo: "ANA@Ejemplo.com"), 1, default), TipoError.Conflicto);
        AssertError(await Registrar().EjecutarAsync(Nuevo(correo: " ana@ejemplo.com "), 1, default), TipoError.Conflicto);
        Assert.Equal(0, _uow.Guardados);
    }

    // ===================== Actualizar =====================

    [Fact]
    public async Task Actualizar_Valido_FijaLaVersion_Guarda_YPublica()
    {
        Existente();

        var res = await Actualizar().EjecutarAsync(1, Edicion(), 9, default);

        Assert.True(res.Exito);
        Assert.Equal("Ana María", res.Valor!.Nombres);
        Assert.Equal(1, _repo.VersionesFijadas);
        Assert.Equal(1, _uow.Guardados);
        Assert.Equal(TipoCambioServidor.Actualizada, Assert.Single(_publicados).Tipo);
    }

    [Fact]
    public async Task Actualizar_ConsuPropioCorreo_NoEsConflicto()
    {
        Existente(correo: "ana@ejemplo.com");
        Assert.True((await Actualizar().EjecutarAsync(1, Edicion(correo: "ana@ejemplo.com"), 1, default)).Exito);
    }

    [Fact]
    public async Task Actualizar_ConElCorreoDeOtroCliente_Da409()
    {
        Existente(1, "ana@ejemplo.com");
        Existente(2, "luis@ejemplo.com");

        AssertError(await Actualizar().EjecutarAsync(2, Edicion(correo: "ana@ejemplo.com"), 1, default), TipoError.Conflicto);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Actualizar_Inexistente_Da404()
    {
        AssertError(await Actualizar().EjecutarAsync(99, Edicion(), 1, default), TipoError.NoEncontrado);
    }

    [Fact]
    public async Task Actualizar_ClienteInactivo_Da422()
    {
        Existente(activo: false);
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(), 1, default), TipoError.Regla);
        Assert.Equal(0, _uow.Guardados);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("###no-base64###")]
    public async Task Actualizar_ConVersionAusenteOInvalida_Da400_YNoSobrescribe(string? version)
    {
        Existente();

        var res = await Actualizar().EjecutarAsync(1, Edicion(version: version), 1, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
        Assert.Equal(0, _repo.VersionesFijadas);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Actualizar_ConTelefonoSinDigitos_Da400()
    {
        Existente();
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(telefono: "--------"), 1, default), TipoError.Validacion);
    }

    [Fact]
    public async Task Actualizar_CiudadInexistente_Da422()
    {
        Existente();
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(ciudad: 999), 1, default), TipoError.Regla);
    }

    [Fact]
    public async Task Actualizar_ConVersionVieja_Da409_SinPublicarEvento()
    {
        Existente();

        var res = await Actualizar(new UnidadDeTrabajoQueChoca()).EjecutarAsync(1, Edicion(), 1, default);

        AssertError(res, TipoError.Conflicto);
        Assert.Empty(_publicados);
    }

    // ===================== Cambiar estado =====================

    [Fact]
    public async Task Baja_GuardaLaFechaEnElRelojDeLaAplicacion_YPublica()
    {
        Existente();

        var res = await CambiarEstado().EjecutarAsync(1, false, 3, default);

        Assert.True(res.Exito);
        Assert.False(res.Valor!.Activo);
        Assert.Equal(Ahora, res.Valor.EliminadoEn);
        Assert.Equal(TipoCambioServidor.EstadoCambiado, Assert.Single(_publicados).Tipo);
    }

    [Fact]
    public async Task Reactivar_LimpiaLaFechaDeBaja()
    {
        Existente(activo: false);

        var res = await CambiarEstado().EjecutarAsync(1, true, 3, default);

        Assert.True(res.Valor!.Activo);
        Assert.Null(res.Valor.EliminadoEn);
    }

    [Fact]
    public async Task Baja_ConPedidosPendientes_Da422_YNoCambiaNada()
    {
        var c = Existente();
        _repo.ConPedidosPendientes.Add(1);

        AssertError(await CambiarEstado().EjecutarAsync(1, false, 3, default), TipoError.Regla);
        Assert.True(c.Activo);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task CambiarAlMismoEstado_NoGuardaNiPublica()
    {
        Existente();

        var res = await CambiarEstado().EjecutarAsync(1, true, 3, default);

        Assert.True(res.Exito);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task CambiarEstado_Inexistente_Da404()
    {
        AssertError(await CambiarEstado().EjecutarAsync(99, false, 3, default), TipoError.NoEncontrado);
    }

    [Fact]
    public async Task CambiarEstado_ConCambioSimultaneo_Da409_EnVezDeError500()
    {
        Existente();

        var res = await CambiarEstado(new UnidadDeTrabajoQueChoca()).EjecutarAsync(1, false, 3, default);

        AssertError(res, TipoError.Conflicto);
        Assert.Empty(_publicados);
    }

    // ===================== Obtener y listar =====================

    [Fact]
    public async Task Obtener_Existente_YNoExistente()
    {
        Existente();
        Assert.Equal(1, (await new ObtenerCliente(_repo).EjecutarAsync(1, default)).Valor!.Id);
        AssertError(await new ObtenerCliente(_repo).EjecutarAsync(99, default), TipoError.NoEncontrado);
    }

    [Fact]
    public async Task Listar_FiltraPorEstado_YPagina()
    {
        Existente(1);
        Existente(2, "b@e.com", activo: false);
        Existente(3, "c@e.com");

        var activos = await new ListarClientes(_repo).EjecutarAsync(null, null, null, true, null, 1, 1, default);

        Assert.Equal(2, activos.Total);
        Assert.Single(activos.Elementos);
    }

    [Fact]
    public async Task Listar_SinFiltroDeEstado_DevuelveSoloActivos_YConIncluirInactivosTodos()
    {
        Existente(1);
        Existente(2, "b@e.com", activo: false);

        var porDefecto = await new ListarClientes(_repo).EjecutarAsync(null, null, null, null, null, 1, 20, default);
        var todos = await new ListarClientes(_repo).EjecutarAsync(null, null, null, null, true, 1, 20, default);
        var soloInactivos = await new ListarClientes(_repo).EjecutarAsync(null, null, null, false, null, 1, 20, default);

        Assert.Equal(1, porDefecto.Total);
        Assert.All(porDefecto.Elementos, c => Assert.True(c.Activo));
        Assert.Equal(2, todos.Total);
        Assert.Equal(1, soloInactivos.Total);
        Assert.False(soloInactivos.Elementos.Single().Activo);
    }
}
