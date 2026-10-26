using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Comun.Notificaciones;
using GestionPedidos.Ui.Modulos.Categorias;

namespace GestionPedidos.Ui.Tests;

public class CategoriasTests
{
    private static CategoriaDto Cat(int id, string n, bool activo = true, params SubcategoriaDto[] subs) =>
        new(id, n, null, activo, null, "AAAA", subs);
    private static SubcategoriaDto Sub(int id, int catId, string n) => new(id, catId, n, null, true, null, "AAAA", 1, 2);
    private static FiltroDto Filtro(int id, int subId, string n, params OpcionFiltroDto[] o) => new(id, subId, n, true, null, "AAAA", o);
    private static OpcionFiltroDto Opcion(int id, int filtroId, string v) => new(id, filtroId, v, true, null, "AAAA");

    private static (Entorno, CategoriasService, CategoriasViewModel) Crear(Func<HttpRequestMessage, (HttpStatusCode, object?)>? extra = null)
    {
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            // Una respuesta especial de la prueba (con cuerpo) tiene prioridad sobre las normales
            if (extra?.Invoke(r) is { Item2: not null } especial) return especial;
            var ruta = r.RequestUri!.AbsolutePath;
            if (r.Method == HttpMethod.Get && ruta.EndsWith("/categorias"))
                return (HttpStatusCode.OK, new[] { Cat(1, "Ropa", true, Sub(10, 1, "Camisas")), Cat(2, "Hogar") });
            if (r.Method == HttpMethod.Get && ruta.EndsWith("/filtros"))
                return (HttpStatusCode.OK, new[] { Filtro(100, 10, "Color", Opcion(1000, 100, "Negro")) });
            return (HttpStatusCode.OK, Cat(3, "Nueva"));
        };
        var servicio = new CategoriasService(e.Api);
        return (e, servicio, new CategoriasViewModel(servicio));
    }

    [Fact]
    public async Task Iniciar_CargaElArbol_YSeleccionaLaPrimeraCategoria()
    {
        var (_, _, vm) = Crear();
        await vm.IniciarAsync();
        Assert.Equal(2, vm.Arbol.Count);
        Assert.Equal("Ropa", vm.CategoriaSel!.Nombre);
        Assert.Null(vm.SubcategoriaSel);
    }

    [Fact]
    public async Task SeleccionarSubcategoria_CargaSusFiltrosConOpciones()
    {
        var (_, _, vm) = Crear();
        await vm.IniciarAsync();
        await vm.SeleccionarSubcategoriaAsync(vm.Arbol[0], vm.Arbol[0].Subcategorias[0]);
        var f = Assert.Single(vm.Filtros);
        Assert.Equal("Negro", f.Opciones.Single().Valor);
    }

    [Fact]
    public async Task Editor_ValidaAntesDeLlamarALaApi()
    {
        var (e, _, vm) = Crear();
        await vm.IniciarAsync();
        var peticiones = e.Manejador.Peticiones.Count;

        vm.NuevaCategoria();
        Assert.True(vm.EditorAbierto);
        Assert.Equal("Nueva categoría", vm.TituloEditor);
        vm.Nombre = "  ";
        await vm.GuardarEditorAsync();
        Assert.Equal("Nombre es obligatorio.", vm.ErrorEditor);
        Assert.True(vm.EditorAbierto);
        Assert.Equal(peticiones, e.Manejador.Peticiones.Count);   // no se llamó a la API

        vm.Nombre = new string('x', 101);
        await vm.GuardarEditorAsync();
        Assert.Contains("100", vm.ErrorEditor);
    }

    [Fact]
    public async Task Editor_AltaCorrecta_Publica_CierraYRecargaElArbol()
    {
        var (e, servicio, vm) = Crear();
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);
        await vm.IniciarAsync();

        vm.NuevaCategoria();
        vm.Nombre = "Deportes";
        await vm.GuardarEditorAsync();

        Assert.False(vm.EditorAbierto);
        var ev = Assert.Single(cambios);
        Assert.Equal((TipoEntidad.Categoria, TipoCambio.Registrada), (ev.Entidad, ev.Tipo));
        Assert.Contains(e.Manejador.Peticiones, p => p.Method == HttpMethod.Post && p.RequestUri!.AbsolutePath.EndsWith("/categorias"));
    }

    [Fact]
    public async Task Editor_SiLaApiRechaza_ElEditorSigueAbiertoConElMotivo_YNoPublica()
    {
        var (_, servicio, vm) = Crear(r => r.Method == HttpMethod.Post
            ? (HttpStatusCode.Conflict, new { title = "Ya existe una categoría con ese nombre.", codigo = "CONFLICTO" })
            : (HttpStatusCode.OK, null));
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);
        await vm.IniciarAsync();

        vm.NuevaCategoria();
        vm.Nombre = "Ropa";
        await vm.GuardarEditorAsync();

        Assert.True(vm.EditorAbierto);
        Assert.Contains("Ya existe", vm.ErrorEditor);
        Assert.Empty(cambios);
    }

    [Fact]
    public async Task Editar_EnviaLaVersionLeida_ParaDetectarEdicionesSimultaneas()
    {
        var (e, _, vm) = Crear();
        await vm.IniciarAsync();
        vm.Editar(vm.Arbol[0]);
        Assert.Equal("Editar categoría", vm.TituloEditor);
        vm.Nombre = "Vestimenta";
        await vm.GuardarEditorAsync();
        var i = e.Manejador.Peticiones.FindIndex(p => p.Method == HttpMethod.Put);
        Assert.Contains("\"rowVersion\":\"AAAA\"", e.Manejador.Cuerpos[i]);
    }

    [Fact]
    public async Task BajaYEliminar_PidenConfirmacion_ElBorradoExigeEscribirElNombre_SoloEnCategoriasYSubcategorias()
    {
        var (e, servicio, vm) = Crear();
        var cambios = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => cambios.Add(a);
        await vm.IniciarAsync();

        vm.SolicitarCambioEstado(new Objetivo(ElementoClasificacion.Categoria, 2, "Hogar", true));
        Assert.Equal(AccionClasificacion.DarDeBaja, vm.Accion);
        Assert.Null(vm.TextoAConfirmar);
        vm.CancelarSolicitud();
        Assert.False(vm.HayConfirmacion);
        Assert.DoesNotContain(e.Manejador.Peticiones, p => p.Method == HttpMethod.Patch);

        vm.SolicitarEliminacion(new Objetivo(ElementoClasificacion.Categoria, 2, "Hogar", false));
        Assert.Equal("Hogar", vm.TextoAConfirmar);
        vm.SolicitarEliminacion(new Objetivo(ElementoClasificacion.Opcion, 1000, "Negro", false));
        Assert.Null(vm.TextoAConfirmar);   // una opción se borra con un solo clic de confirmación

        await vm.ConfirmarAsync();
        Assert.Contains(e.Manejador.Peticiones, p => p.Method == HttpMethod.Delete && p.RequestUri!.AbsolutePath.EndsWith("/opciones/1000"));
        Assert.Equal(TipoCambio.Eliminada, Assert.Single(cambios).Tipo);
        Assert.False(vm.HayConfirmacion);
    }

    [Fact]
    public void Mensajes_ConcuerdanEnGenero()
    {
        string M(TipoEntidad ent, TipoCambio t, bool activo = true) =>
            Mensajes.Cambio(new EntidadCambiadaEventArgs { Entidad = ent, Tipo = t, Descripcion = "X", Activo = activo, Id = 1 });
        Assert.Equal("Categoría X registrada.", M(TipoEntidad.Categoria, TipoCambio.Registrada));
        Assert.Equal("Opción X dada de baja.", M(TipoEntidad.OpcionFiltro, TipoCambio.EstadoCambiado, false));
        Assert.Equal("Filtro X reactivado.", M(TipoEntidad.Filtro, TipoCambio.EstadoCambiado));
        Assert.Equal("Subcategoría X eliminada definitivamente.", M(TipoEntidad.Subcategoria, TipoCambio.Eliminada));
    }
}
