using GestionPedidos.Application.Catalogo;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

/// <summary>Repositorio en memoria de la clasificación (sin base de datos).</summary>
internal sealed class ClasificacionFake : IClasificacionRepository
{
    private int _siguiente = 100;
    public List<Categoria> Categorias { get; } = new();
    public List<Subcategoria> Subcategorias { get; } = new();
    public List<Filtro> Filtros { get; } = new();
    public List<OpcionFiltro> Opciones { get; } = new();
    public int ProductosActivos { get; set; }
    public int ProductosTotales { get; set; }
    public int UsosDeOpcion { get; set; }
    public List<object> Eliminados { get; } = new();

    public Task<IReadOnlyList<CategoriaDto>> ObtenerArbolAsync(bool incluirInactivas, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CategoriaDto>>(Categorias.Where(c => incluirInactivas || c.Activo)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo, c.EliminadoEn, "", Array.Empty<SubcategoriaDto>())).ToList());
    public Task<IReadOnlyList<FiltroDto>> ObtenerFiltrosAsync(int subcategoriaId, bool incluirInactivos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FiltroDto>>(Filtros.Where(f => f.SubcategoriaId == subcategoriaId)
            .Select(f => new FiltroDto(f.Id, f.SubcategoriaId, f.Nombre, f.Activo, f.EliminadoEn, "", Array.Empty<OpcionFiltroDto>())).ToList());

    public Task<T?> ObtenerAsync<T>(int id, CancellationToken ct) where T : ElementoDeCatalogo
    {
        ElementoDeCatalogo? e = typeof(T) == typeof(Categoria) ? Categorias.FirstOrDefault(x => x.Id == id)
            : typeof(T) == typeof(Subcategoria) ? Subcategorias.FirstOrDefault(x => x.Id == id)
            : typeof(T) == typeof(Filtro) ? Filtros.FirstOrDefault(x => x.Id == id)
            : Opciones.FirstOrDefault(x => x.Id == id);
        return Task.FromResult((T?)e);
    }

    public void Agregar<T>(T elemento) where T : ElementoDeCatalogo
    {
        elemento.Id = _siguiente++;
        switch (elemento)
        {
            case Categoria c: Categorias.Add(c); break;
            case Subcategoria s: s.Categoria = Categorias.FirstOrDefault(x => x.Id == s.CategoriaId); Subcategorias.Add(s); break;
            case Filtro f: Filtros.Add(f); break;
            case OpcionFiltro o: Opciones.Add(o); break;
        }
    }

    public void Eliminar<T>(T elemento) where T : ElementoDeCatalogo
    {
        Eliminados.Add(elemento);
        Categorias.Remove((elemento as Categoria)!); Subcategorias.Remove((elemento as Subcategoria)!);
        Filtros.Remove((elemento as Filtro)!); Opciones.Remove((elemento as OpcionFiltro)!);
    }

    public void FijarVersionOriginal(ElementoDeCatalogo elemento, byte[] rowVersion) { }
    public Task<bool> ExisteCategoriaAsync(string nombre, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Categorias.Any(c => string.Equals(c.Nombre, nombre, StringComparison.OrdinalIgnoreCase) && c.Id != exceptoId));
    public Task<bool> ExisteSubcategoriaAsync(int categoriaId, string nombre, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Subcategorias.Any(s => s.CategoriaId == categoriaId && string.Equals(s.Nombre, nombre, StringComparison.OrdinalIgnoreCase) && s.Id != exceptoId));
    public Task<bool> ExisteFiltroAsync(int subcategoriaId, string nombre, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Filtros.Any(f => f.SubcategoriaId == subcategoriaId && string.Equals(f.Nombre, nombre, StringComparison.OrdinalIgnoreCase) && f.Id != exceptoId));
    public Task<bool> ExisteOpcionAsync(int filtroId, string valor, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Opciones.Any(o => o.FiltroId == filtroId && string.Equals(o.Valor, valor, StringComparison.OrdinalIgnoreCase) && o.Id != exceptoId));
    public Task<int> ContarSubcategoriasAsync(int categoriaId, bool soloActivas, CancellationToken ct) =>
        Task.FromResult(Subcategorias.Count(s => s.CategoriaId == categoriaId && (!soloActivas || s.Activo)));
    public Task<int> ContarFiltrosAsync(int subcategoriaId, CancellationToken ct) => Task.FromResult(Filtros.Count(f => f.SubcategoriaId == subcategoriaId));
    public Task<int> ContarProductosAsync(int subcategoriaId, bool soloActivos, CancellationToken ct) =>
        Task.FromResult(soloActivos ? ProductosActivos : ProductosTotales);
    public Task<int> ContarOpcionesAsync(int filtroId, CancellationToken ct) => Task.FromResult(Opciones.Count(o => o.FiltroId == filtroId));
    public Task<int> ContarUsosDeOpcionAsync(int opcionId, CancellationToken ct) => Task.FromResult(UsosDeOpcion);
}

public class ClasificacionTests
{
    private readonly ClasificacionFake _repo = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly List<EntidadCambiadaServidorEventArgs> _cambios = new();

    public ClasificacionTests() => _eventos.EntidadCambiada += (_, e) => _cambios.Add(e);

    private GestionarCategorias Categorias => new(_repo, _uow, _eventos, _reloj);
    private GestionarSubcategorias Subcategorias => new(_repo, _uow, _eventos, _reloj);
    private GestionarFiltros Filtros => new(_repo, _uow, _eventos, _reloj);
    private GestionarOpciones Opciones => new(_repo, _uow, _eventos, _reloj);
    private static GuardarClasificacionSolicitud Nombre(string n) => new(n, null);

    // ---- Altas, validación, unicidad y eventos ----
    [Fact]
    public async Task CrearCategoria_Guarda_YPublicaElEventoSoloDespuesDeGuardar()
    {
        var r = await Categorias.CrearAsync(new GuardarClasificacionSolicitud("  Ropa ", "Prendas"), 1, default);
        Assert.True(r.Exito);
        Assert.Equal("Ropa", r.Valor!.Nombre);
        Assert.Equal(1, _uow.Guardados);
        var e = Assert.Single(_cambios);
        Assert.Equal(("Categoria", TipoCambioServidor.Registrada), (e.Entidad, e.Tipo));
        Assert.Contains("Ropa", e.Detalle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearCategoria_SinNombre_EsValidacion_YNoPublicaNada(string nombre)
    {
        var r = await Categorias.CrearAsync(Nombre(nombre), 1, default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
        Assert.Empty(_cambios);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task CrearCategoria_NombreRepetido_SinImportarMayusculas_EsConflicto()
    {
        await Categorias.CrearAsync(Nombre("Ropa"), 1, default);
        var r = await Categorias.CrearAsync(Nombre("ROPA"), 1, default);
        Assert.Equal(TipoError.Conflicto, r.Error!.Tipo);
        Assert.Single(_cambios);
    }

    [Fact]
    public async Task Jerarquia_CadaNivelSeCreaBajoSuPadre_YNoSeCrea_BajoUnPadreInactivo()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        var filtro = (await Filtros.CrearAsync(sub.Id, new GuardarFiltroSolicitud("Color"), 1, default)).Valor!;
        var opcion = await Opciones.CrearAsync(filtro.Id, new GuardarOpcionSolicitud("Negro"), 1, default);
        Assert.True(opcion.Exito);
        Assert.Equal(4, _cambios.Count);

        // La misma subcategoría en otra categoría sí se permite; repetida en la misma, no
        var otra = (await Categorias.CrearAsync(Nombre("Hogar"), 1, default)).Valor!;
        Assert.True((await Subcategorias.CrearAsync(otra.Id, Nombre("Camisas"), 1, default)).Exito);
        Assert.Equal(TipoError.Conflicto, (await Subcategorias.CrearAsync(cat.Id, Nombre("camisas"), 1, default)).Error!.Tipo);

        _repo.Opciones.Single().CambiarEstado(false, _reloj.GetUtcNow());
        _repo.Filtros.Single().CambiarEstado(false, _reloj.GetUtcNow());
        var enFiltroInactivo = await Opciones.CrearAsync(filtro.Id, new GuardarOpcionSolicitud("Blanco"), 1, default);
        Assert.Equal(TipoError.Regla, enFiltroInactivo.Error!.Tipo);
    }

    [Fact]
    public async Task CategoriaInexistente_EsNoEncontrado()
    {
        Assert.Equal(TipoError.NoEncontrado, (await Categorias.ActualizarAsync(99, Nombre("X"), 1, default)).Error!.Tipo);
        Assert.Equal(TipoError.NoEncontrado, (await Subcategorias.CrearAsync(99, Nombre("X"), 1, default)).Error!.Tipo);
        Assert.Equal(TipoError.NoEncontrado, (await Filtros.CambiarEstadoAsync(99, false, 1, default)).Error!.Tipo);
        Assert.Equal(TipoError.NoEncontrado, (await Opciones.EliminarAsync(99, 1, default)).Error!.Tipo);
    }

    [Fact]
    public async Task Actualizar_PublicaElNombreAnteriorYElNuevo_YRechazaLasInactivas()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        _cambios.Clear();
        var r = await Categorias.ActualizarAsync(cat.Id, Nombre("Vestimenta"), 1, default);
        Assert.True(r.Exito);
        Assert.Contains("«Ropa»", _cambios.Single().Detalle);
        Assert.Contains("«Vestimenta»", _cambios.Single().Detalle);

        await Categorias.CambiarEstadoAsync(cat.Id, false, 1, default);
        var inactiva = await Categorias.ActualizarAsync(cat.Id, Nombre("Otra"), 1, default);
        Assert.Equal(TipoError.Regla, inactiva.Error!.Tipo);
    }

    // ---- Baja suave y reactivación ----
    [Fact]
    public async Task BajaDeCategoria_ConSubcategoriasActivas_SeRechaza_YSinEllas_GuardaLaFecha()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;

        Assert.Equal(TipoError.Regla, (await Categorias.CambiarEstadoAsync(cat.Id, false, 1, default)).Error!.Tipo);

        await Subcategorias.CambiarEstadoAsync(sub.Id, false, 1, default);
        var baja = await Categorias.CambiarEstadoAsync(cat.Id, false, 1, default);
        Assert.True(baja.Exito);
        Assert.False(baja.Valor!.Activo);
        Assert.Equal(_reloj.GetUtcNow(), baja.Valor.EliminadoEn);

        var alta = await Categorias.CambiarEstadoAsync(cat.Id, true, 1, default);
        Assert.True(alta.Valor!.Activo);
        Assert.Null(alta.Valor.EliminadoEn);
    }

    [Fact]
    public async Task BajaDeSubcategoria_ConProductosActivos_SeRechaza()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        _repo.ProductosActivos = 3;
        var r = await Subcategorias.CambiarEstadoAsync(sub.Id, false, 1, default);
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.Contains("productos activos", r.Error.Mensaje);
    }

    [Fact]
    public async Task ReactivarHijo_BajoUnPadreInactivo_SeRechaza()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        await Subcategorias.CambiarEstadoAsync(sub.Id, false, 1, default);
        await Categorias.CambiarEstadoAsync(cat.Id, false, 1, default);

        var r = await Subcategorias.CambiarEstadoAsync(sub.Id, true, 1, default);
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
    }

    [Fact]
    public async Task CambiarAlMismoEstado_NoGuardaNiPublica()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        _cambios.Clear(); var guardados = _uow.Guardados;
        var r = await Categorias.CambiarEstadoAsync(cat.Id, true, 1, default);
        Assert.True(r.Exito);
        Assert.Empty(_cambios);
        Assert.Equal(guardados, _uow.Guardados);
    }

    // ---- Eliminación definitiva ----
    [Fact]
    public async Task Eliminar_ExigeEstarInactivo_YNoTenerHijosNiUsos()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        var filtro = (await Filtros.CrearAsync(sub.Id, new GuardarFiltroSolicitud("Color"), 1, default)).Valor!;
        var opcion = (await Opciones.CrearAsync(filtro.Id, new GuardarOpcionSolicitud("Negro"), 1, default)).Valor!;

        Assert.Equal(TipoError.Regla, (await Categorias.EliminarAsync(cat.Id, 1, default)).Error!.Tipo);   // activa
        await Opciones.CambiarEstadoAsync(opcion.Id, false, 1, default);

        _repo.UsosDeOpcion = 2;
        Assert.Equal(TipoError.Conflicto, (await Opciones.EliminarAsync(opcion.Id, 1, default)).Error!.Tipo);   // en uso
        _repo.UsosDeOpcion = 0;
        Assert.True((await Opciones.EliminarAsync(opcion.Id, 1, default)).Exito);
        Assert.Equal(TipoCambioServidor.Eliminada, _cambios.Last().Tipo);

        await Filtros.CambiarEstadoAsync(filtro.Id, false, 1, default);
        Assert.True((await Filtros.EliminarAsync(filtro.Id, 1, default)).Exito);

        await Subcategorias.CambiarEstadoAsync(sub.Id, false, 1, default);
        _repo.ProductosTotales = 1;
        Assert.Equal(TipoError.Conflicto, (await Subcategorias.EliminarAsync(sub.Id, 1, default)).Error!.Tipo);   // tiene productos
        _repo.ProductosTotales = 0;
        Assert.True((await Subcategorias.EliminarAsync(sub.Id, 1, default)).Exito);

        await Categorias.CambiarEstadoAsync(cat.Id, false, 1, default);
        Assert.True((await Categorias.EliminarAsync(cat.Id, 1, default)).Exito);
        Assert.Equal(4, _repo.Eliminados.Count);
    }

    [Fact]
    public async Task EliminarFiltro_ConOpciones_EsConflicto()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        var filtro = (await Filtros.CrearAsync(sub.Id, new GuardarFiltroSolicitud("Color"), 1, default)).Valor!;
        await Opciones.CrearAsync(filtro.Id, new GuardarOpcionSolicitud("Negro"), 1, default);
        await Filtros.CambiarEstadoAsync(filtro.Id, false, 1, default);

        Assert.Equal(TipoError.Conflicto, (await Filtros.EliminarAsync(filtro.Id, 1, default)).Error!.Tipo);
    }

    // ---- Consultas ----
    [Fact]
    public async Task Consultas_ArbolYFiltros()
    {
        var cat = (await Categorias.CrearAsync(Nombre("Ropa"), 1, default)).Valor!;
        var sub = (await Subcategorias.CrearAsync(cat.Id, Nombre("Camisas"), 1, default)).Valor!;
        await Filtros.CrearAsync(sub.Id, new GuardarFiltroSolicitud("Color"), 1, default);

        Assert.Single(await new ObtenerArbolDeCatalogo(_repo).EjecutarAsync(false, default));
        var filtros = await new ObtenerFiltrosDeSubcategoria(_repo).EjecutarAsync(sub.Id, false, default);
        Assert.Single(filtros.Valor!);
        Assert.Equal(TipoError.NoEncontrado, (await new ObtenerFiltrosDeSubcategoria(_repo).EjecutarAsync(999, false, default)).Error!.Tipo);
    }
}
