using System.Globalization;
using System.Text;
using GestionPedidos.Application.Imagenes;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Domain.Catalogo;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Historial;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionPedidos.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos de ejemplo realistas para desarrollo y demostraciones ([CatalogoProductos.md §11]): catálogo completo
/// (categorías, subcategorías, filtros, opciones y productos descritos), personal, clientes (algunos con cuenta web),
/// pedidos con sus líneas en distintos estados y el historial correspondiente. Es <b>determinista</b> (misma semilla,
/// mismos datos) e <b>idempotente</b>: si la base ya tiene categorías, productos o clientes no hace nada.
/// </summary>
public sealed class DatosDeEjemplo
{
    /// <summary>Contraseña de todos los usuarios de ejemplo (personal y clientes web). Solo para desarrollo.</summary>
    public const string ClaveDeEjemplo = "Demo_Sembrado_2026";

    private const int Semilla = 20261026;
    private const int TotalClientes = 48;
    private const int TotalPedidos = 90;

    private static readonly string[] Nombres =
    {
        "María", "José", "Ana", "Carlos", "Laura", "Luis", "Sofía", "Miguel", "Daniela", "Jorge", "Gabriela", "Fernando", "Carmen", "Ricardo",
        "Patricia", "Roberto", "Verónica", "Manuel", "Claudia", "Oscar", "Karla", "Eduardo", "Rosa", "Walter", "Andrea", "Mauricio", "Lissette",
        "Héctor", "Alejandra", "Nelson", "Jessica", "Rodrigo", "Cecilia", "Josué", "Mónica", "Kevin", "Beatriz", "Samuel", "Yanira", "Alexander",
    };

    private static readonly string[] Apellidos =
    {
        "Hernández", "Martínez", "López", "García", "Rodríguez", "Pérez", "Ramírez", "Flores", "Gómez", "Castillo", "Mejía", "Rivera", "Cruz",
        "Ortiz", "Menjívar", "Alvarado", "Aguilar", "Quintanilla", "Cortez", "Henríquez", "Portillo", "Escobar", "Villalta", "Guardado", "Rosales",
        "Argueta", "Magaña", "Alfaro", "Chávez", "Barrera", "Amaya", "Zelaya", "Najarro", "Molina", "Segovia", "Rauda",
    };

    private static readonly string[] Colonias =
    {
        "Colonia Escalón", "Colonia Miramonte", "Residencial Altos de la Cima", "Colonia Médica", "Urbanización Jardines de la Hacienda",
        "Colonia San Benito", "Barrio San Jacinto", "Colonia Zacamil", "Residencial Santa Elena", "Colonia Layco", "Barrio El Calvario",
        "Colonia Centroamérica", "Urbanización Lomas de San Francisco", "Colonia Costa Rica", "Residencial Los Pinos", "Colonia Monserrat",
    };

    private static readonly string[] Departamentos = { "San Salvador", "La Libertad", "Santa Ana", "San Miguel" };

    private readonly GestionPedidosDbContext _db;
    private readonly IHasherClave _hasher;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DatosDeEjemplo> _log;
    private readonly IAlmacenImagenes? _almacen;

    /// <summary>Crea el sembrador. Sin almacén de imágenes se siembra todo menos las imágenes.</summary>
    public DatosDeEjemplo(GestionPedidosDbContext db, IHasherClave hasher, TimeProvider reloj, ILogger<DatosDeEjemplo> log, IAlmacenImagenes? almacen = null)
    {
        _db = db; _hasher = hasher; _reloj = reloj; _log = log; _almacen = almacen;
    }

    /// <summary>Siembra los datos de ejemplo si la base está vacía de catálogo, productos y clientes.</summary>
    /// <param name="ct">Cancelación.</param>
    public async Task SembrarAsync(CancellationToken ct)
    {
        if (await _db.Categorias.AnyAsync(ct) || await _db.Productos.AnyAsync(ct) || await _db.Clientes.AnyAsync(ct))
        {
            _log.LogInformation("Datos de ejemplo: la base ya tiene datos, no se siembra nada.");
            return;
        }
        var admin = await _db.Usuarios.OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.RolId == 1, ct);
        if (admin is null)
        {
            _log.LogWarning("Datos de ejemplo: no hay un administrador; se omite la siembra.");
            return;
        }

        var rnd = new Random(Semilla);
        var ahora = _reloj.GetUtcNow();

        var (categorias, subcategorias, opcionesPorSub) = await SembrarClasificacionAsync(ahora, ct);
        var productos = await SembrarProductosAsync(subcategorias, opcionesPorSub, rnd, ahora, ct);
        await SembrarImagenesAsync(categorias, productos, ct);
        var personal = await SembrarPersonalAsync(admin, ahora, ct);
        var (clientes, cuentasWeb) = await SembrarClientesAsync(rnd, ahora, ct);
        var pedidos = await SembrarPedidosAsync(clientes, cuentasWeb, productos, personal, rnd, ahora, ct);
        await SembrarHistorialAsync(admin, personal, categorias, productos, clientes, pedidos, rnd, ahora, ct);

        _log.LogInformation("Datos de ejemplo sembrados: {Categorias} categorías, {Subcategorias} subcategorías, {Productos} productos, " +
            "{Clientes} clientes y {Pedidos} pedidos.", categorias.Count, subcategorias.Count, productos.Count, clientes.Count, pedidos.Count);
    }

    // ------------------------------------------------------------------ catálogo
    private async Task<(List<Categoria>, Dictionary<string, Subcategoria>, Dictionary<string, Dictionary<string, Dictionary<string, OpcionFiltro>>>)>
        SembrarClasificacionAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var categorias = new List<Categoria>();
        var subs = new Dictionary<string, Subcategoria>();
        var opciones = new Dictionary<string, Dictionary<string, Dictionary<string, OpcionFiltro>>>();   // sub → filtro → valor → opción

        foreach (var c in CatalogoDeEjemplo.Categorias)
        {
            var categoria = new Categoria { Codigo = c.Codigo, Nombre = c.Nombre, Descripcion = c.Descripcion };
            categorias.Add(categoria);
            foreach (var s in c.Subcategorias)
            {
                var sub = new Subcategoria { Categoria = categoria, Nombre = s.Nombre, Descripcion = s.Descripcion };
                subs[Clave(c.Nombre, s.Nombre)] = sub;
                var porFiltro = opciones[Clave(c.Nombre, s.Nombre)] = new();
                foreach (var f in s.Filtros)
                {
                    var filtro = new Filtro { Subcategoria = sub, Nombre = f.Nombre };
                    var porValor = porFiltro[f.Nombre] = new();
                    foreach (var valor in f.Opciones)
                    {
                        var opcion = new OpcionFiltro { Filtro = filtro, Valor = valor };
                        if (s.Nombre == "Camisas" && f.Nombre == "Color" && valor == CatalogoDeEjemplo.OpcionInactiva)
                            opcion.CambiarEstado(false, ahora.AddDays(-12));   // una opción dada de baja, para probar los estados
                        porValor[valor] = opcion;
                        _db.OpcionesFiltro.Add(opcion);
                    }
                }
            }
        }
        // Una categoría inactiva y vacía: sirve para probar la eliminación definitiva desde la pantalla
        var vieja = new Categoria { Codigo = CatalogoDeEjemplo.CodigoCategoriaInactiva, Nombre = CatalogoDeEjemplo.CategoriaInactiva, Descripcion = "Colección anterior, ya sin productos" };
        vieja.CambiarEstado(false, ahora.AddDays(-30));
        categorias.Add(vieja);

        _db.Categorias.AddRange(categorias);
        await _db.SaveChangesAsync(ct);
        return (categorias, subs, opciones);
    }

    private static string Clave(string categoria, string sub) => $"{categoria}/{sub}";

    private async Task<List<Producto>> SembrarProductosAsync(Dictionary<string, Subcategoria> subs,
        Dictionary<string, Dictionary<string, Dictionary<string, OpcionFiltro>>> opciones, Random rnd, DateTimeOffset ahora, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var productos = new List<Producto>();
        var indice = 0;
        foreach (var c in CatalogoDeEjemplo.Categorias)
        foreach (var s in c.Subcategorias)
        {
            var clave = Clave(c.Nombre, s.Nombre);
            var sub = subs[clave];
            var numero = 0;
            foreach (var pe in s.Productos)
            {
                numero++;
                var fijas = pe.Detalle.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);
                var detalle = new List<(int, int)>();
                foreach (var f in s.Filtros)
                {
                    var validas = opciones[clave][f.Nombre].Values.Where(o => o.Activo).ToList();   // no se usan opciones dadas de baja
                    var elegida = fijas.TryGetValue(f.Nombre, out var valor) ? opciones[clave][f.Nombre][valor] : validas[rnd.Next(validas.Count)];
                    detalle.Add((elegida.FiltroId, elegida.Id));
                }

                var p = new Producto
                {
                    // Código humano y estable (ROP-CAM-001): con él se nombra la imagen en el almacén y se puede recuperar si la base se pierde
                    Codigo = $"{c.Codigo}-{s.Codigo}-{numero:000}",
                    Nombre = pe.Nombre, Descripcion = pe.Descripcion, CantidadStock = pe.Stock, PrecioVenta = pe.Precio,
                    FechaIngreso = hoy.AddDays(-rnd.Next(25, 120)),
                    MostrarStock = indice % 4 == 0,   // una cuarta parte muestra la cantidad exacta en la tienda
                };
                p.EstablecerClasificacion(sub.Id, detalle);
                if (!pe.Activo) { p.Activo = false; p.EliminadoEn = ahora.AddDays(-rnd.Next(3, 20)); }
                productos.Add(p);
                indice++;
            }
        }
        _db.Productos.AddRange(productos);
        await _db.SaveChangesAsync(ct);
        return productos;
    }

    // ------------------------------------------------------------------ imágenes
    /// <summary>
    /// Sube al almacén las imágenes de ejemplo (incrustadas, nombradas con el código: <c>productos/ROP-CAM-001.jpg</c>) y las liga a su
    /// producto o categoría. Si el almacén no responde se sigue sin imágenes: el resto de los datos no depende de ellas.
    /// </summary>
    private async Task SembrarImagenesAsync(List<Categoria> categorias, List<Producto> productos, CancellationToken ct)
    {
        if (_almacen is null) { _log.LogInformation("Datos de ejemplo: sin almacén de imágenes configurado; se omiten las imágenes."); return; }
        int subidas = 0;
        try
        {
            foreach (var (tipo, codigo, asignar) in categorias.Select(c => (TipoImagen.Categoria, c.Codigo, (Action<string>)(k => c.ImagenClave = k)))
                         .Concat(productos.Select(p => (TipoImagen.Producto, p.Codigo, (Action<string>)(k => p.ImagenClave = k)))))
            {
                var clave = ClavesDeImagen.Clave(tipo, codigo, "jpg");
                await using var flujo = typeof(DatosDeEjemplo).Assembly.GetManifestResourceStream(clave);   // el nombre del recurso es la clave
                if (flujo is null) continue;   // sin foto para ese código: la tienda muestra el ícono de su categoría
                await _almacen.GuardarAsync(clave, flujo, "image/jpeg", ct);
                asignar(clave);
                subidas++;
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Datos de ejemplo: no se pudieron subir las imágenes al almacén (¿Garage está encendido?). Se continúa sin ellas.");
            _db.ChangeTracker.Clear();   // lo asignado a medias se descarta; las claves solo se guardan si todo salió bien
            foreach (var c in categorias) c.ImagenClave = null;
            foreach (var p in productos) p.ImagenClave = null;
            return;
        }
        _log.LogInformation("Datos de ejemplo: {Subidas} imágenes subidas al almacén y ligadas por código.", subidas);
    }

    // ------------------------------------------------------------------ personal
    private async Task<List<Usuario>> SembrarPersonalAsync(Usuario admin, DateTimeOffset ahora, CancellationToken ct)
    {
        var hash = _hasher.Hashear(ClaveDeEjemplo);
        Usuario U(string usuario, string nombre, int rol) =>
            Usuario.Crear(usuario, nombre, $"{usuario}@gestionpedidos.local", hash, rol, debeCambiarClave: false);

        var activos = new List<Usuario>
        {
            U("gerencia", "Carlos Menjívar", 1),
            U("mramirez", "María Ramírez", 2),
            U("jcastillo", "José Castillo", 2),
            U("lmartinez", "Laura Martínez", 2),
        };
        var exOperador = U("rcruz", "Roberto Cruz", 2);
        exOperador.CambiarEstado(false, ahora.AddDays(-20));   // un operador dado de baja (se puede reactivar o eliminar)

        _db.Usuarios.AddRange(activos);
        _db.Usuarios.Add(exOperador);
        await _db.SaveChangesAsync(ct);
        activos.Insert(0, admin);
        activos.Add(exOperador);
        return activos;
    }

    // ------------------------------------------------------------------ clientes
    private async Task<(List<Cliente>, Dictionary<int, Usuario>)> SembrarClientesAsync(Random rnd, DateTimeOffset ahora, CancellationToken ct)
    {
        var principales = await _db.Departamentos.Where(d => Departamentos.Contains(d.Nombre)).Select(d => d.Id).ToListAsync(ct);
        var ciudadesPrincipales = await _db.Ciudades.Where(c => principales.Contains(c.DepartamentoId)).Select(c => c.Id).ToListAsync(ct);
        var todasLasCiudades = await _db.Ciudades.Select(c => c.Id).ToListAsync(ct);

        var hash = _hasher.Hashear(ClaveDeEjemplo);
        var clientes = new List<Cliente>();
        var cuentas = new Dictionary<int, Usuario>();   // índice del cliente → su cuenta web
        var correos = new HashSet<string>();
        for (var i = 0; i < TotalClientes; i++)
        {
            var nombres = Nombres[rnd.Next(Nombres.Length)];
            var apellidos = $"{Apellidos[rnd.Next(Apellidos.Length)]} {Apellidos[rnd.Next(Apellidos.Length)]}";
            var ciudad = rnd.NextDouble() < 0.65 ? ciudadesPrincipales[rnd.Next(ciudadesPrincipales.Count)] : todasLasCiudades[rnd.Next(todasLasCiudades.Count)];

            string? correo = null;
            if (rnd.NextDouble() < 0.75)
            {
                var baseCorreo = $"{SinAcentos(nombres)}.{SinAcentos(apellidos.Split(' ')[0])}".ToLowerInvariant();
                var candidato = $"{baseCorreo}@ejemplo.com";
                for (var n = 2; !correos.Add(candidato); n++) candidato = $"{baseCorreo}{n}@ejemplo.com";
                correo = candidato;
            }

            var c = new Cliente
            {
                Nombres = nombres, Apellidos = apellidos,
                Telefono = $"{(rnd.Next(2) == 0 ? 7 : 6)}{rnd.Next(1_000_000, 9_999_999)}",
                Direccion = $"{Colonias[rnd.Next(Colonias.Length)]}, pasaje {rnd.Next(1, 15)}, casa #{rnd.Next(1, 90)}",
                Correo = correo, CiudadId = ciudad,
            };
            if (i == 5 || i == 31) { c.Activo = false; c.EliminadoEn = ahora.AddDays(-rnd.Next(5, 40)); }   // dos clientes dados de baja

            // Seis clientes con cuenta web (rol Cliente) para probar la tienda: iniciar sesión con su correo
            if (correo is not null && c.Activo && cuentas.Count < 6 && i % 5 == 0)
            {
                var cuenta = Usuario.Crear(correo, $"{nombres} {apellidos}", correo, hash, 3, debeCambiarClave: false);
                cuentas[i] = cuenta;
                c.Usuario = cuenta;
            }
            clientes.Add(c);
        }
        _db.Clientes.AddRange(clientes);
        await _db.SaveChangesAsync(ct);
        return (clientes, cuentas.ToDictionary(kv => clientes[kv.Key].Id, kv => kv.Value));
    }

    private static string SinAcentos(string texto)
    {
        var sb = new StringBuilder();
        foreach (var ch in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // ------------------------------------------------------------------ pedidos
    private async Task<List<Pedido>> SembrarPedidosAsync(List<Cliente> clientes, Dictionary<int, Usuario> cuentasWeb, List<Producto> productos,
        List<Usuario> personal, Random rnd, DateTimeOffset ahora, CancellationToken ct)
    {
        var comentarios = new[]
        {
            null, null, null, "Entregar por la tarde, por favor.", "Llamar antes de llegar.", "Es un regalo, favor empacar.",
            "Dejar con el vigilante si no estoy.", "Prefiero entrega en la mañana.",
        };
        var clientesActivos = clientes.Where(c => c.Activo).ToList();
        var personalActivo = personal.Where(u => u.Activo).ToList();
        var stock = productos.Where(p => p.Activo).ToDictionary(p => p, p => p.CantidadStock);   // inventario que va quedando
        var pedidos = new List<Pedido>();

        for (var i = 0; i < TotalPedidos; i++)
        {
            var cliente = clientesActivos[rnd.Next(clientesActivos.Count)];
            var diasAtras = rnd.Next(0, 75);
            var fecha = ahora.AddDays(-diasAtras).AddHours(-rnd.Next(0, 10)).AddMinutes(-rnd.Next(0, 60));

            // Quien lo registró: el propio cliente si tiene cuenta web (a veces), o alguien del personal
            Usuario registra;
            if (cuentasWeb.TryGetValue(cliente.Id, out var cuenta) && rnd.NextDouble() < 0.8) registra = cuenta;
            else
            {
                var candidatos = diasAtras > 20 ? personal : personalActivo;   // el operador dado de baja trabajó antes de su baja
                registra = candidatos[rnd.Next(candidatos.Count)];
            }

            var estado = ElegirEstado(diasAtras, rnd);
            var pedido = new Pedido
            {
                Cliente = cliente, UsuarioId = registra.Id,
                FechaPedido = fecha, FechaEntrega = DateOnly.FromDateTime(fecha.UtcDateTime).AddDays(rnd.Next(2, 9)),
                Comentarios = comentarios[rnd.Next(comentarios.Length)], Estado = estado,
            };

            var disponibles = stock.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 5)).ToList();
            foreach (var p in disponibles)
            {
                var maximo = p.PrecioVenta < 15 ? 5 : 3;
                var cantidad = Math.Min(rnd.Next(1, maximo + 1), stock[p]);
                if (cantidad <= 0) continue;
                pedido.Detalles.Add(new DetallePedido { Producto = p, Cantidad = cantidad, PrecioUnitario = p.PrecioVenta });
                if (estado != EstadoPedido.Cancelado) stock[p] -= cantidad;   // cancelado = el stock se repuso
            }
            if (pedido.Detalles.Count == 0) continue;
            pedido.Total = pedido.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
            pedidos.Add(pedido);
        }

        foreach (var (p, restante) in stock) p.CantidadStock = restante;   // el inventario refleja lo vendido
        _db.Pedidos.AddRange(pedidos);
        await _db.SaveChangesAsync(ct);
        return pedidos;
    }

    private static string ElegirEstado(int diasAtras, Random rnd)
    {
        var x = rnd.NextDouble();
        if (diasAtras > 14) return x < 0.82 ? EstadoPedido.Entregado : EstadoPedido.Cancelado;
        if (diasAtras > 4) return x < 0.50 ? EstadoPedido.Entregado : x < 0.85 ? EstadoPedido.Pendiente : EstadoPedido.Cancelado;
        return x < 0.80 ? EstadoPedido.Pendiente : x < 0.90 ? EstadoPedido.Entregado : EstadoPedido.Cancelado;
    }

    // ------------------------------------------------------------------ historial
    private async Task SembrarHistorialAsync(Usuario admin, List<Usuario> personal, List<Categoria> categorias, List<Producto> productos,
        List<Cliente> clientes, List<Pedido> pedidos, Random rnd, DateTimeOffset ahora, CancellationToken ct)
    {
        var actividades = new List<Actividad>();
        Usuario Alguien() => personal[rnd.Next(personal.Count - 1)];   // sin el operador dado de baja
        void A(int? usuarioId, DateTimeOffset fecha, string accion, string entidad, int? id, string detalle) =>
            actividades.Add(new Actividad { UsuarioId = usuarioId, Fecha = fecha, Accion = accion, Entidad = entidad, EntidadId = id, Detalle = detalle });

        foreach (var c in categorias.Where(c => c.Activo))
            A(admin.Id, ahora.AddDays(-100), "Registrar", "Categoria", c.Id, $"Categoría «{c.Nombre}» creada");
        foreach (var p in productos)
            A(Alguien().Id, new DateTimeOffset(p.FechaIngreso.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero), "Registrar", "Producto", p.Id, $"Producto {p.Nombre} registrado");
        foreach (var c in clientes)
            A(Alguien().Id, ahora.AddDays(-rnd.Next(30, 95)), "Registrar", "Cliente", c.Id, $"Cliente {c.Nombres} {c.Apellidos} registrado");
        foreach (var p in pedidos)
        {
            A(p.UsuarioId, p.FechaPedido, "Registrar", "Pedido", p.Id, $"Pedido #{p.Id} registrado por ${p.Total:0.00}");
            if (p.Estado != EstadoPedido.Pendiente)
                A(Alguien().Id, p.FechaPedido.AddDays(1).AddHours(rnd.Next(1, 8)), "CambiarEstado", "Pedido", p.Id, $"Pedido #{p.Id}: {p.Estado.ToLowerInvariant()}");
        }
        // Inicios de sesión de ejemplo, repartidos en las últimas semanas
        for (var i = 0; i < 40; i++)
        {
            var u = personal[rnd.Next(personal.Count - 1)];
            A(u.Id, ahora.AddDays(-rnd.Next(0, 30)).AddMinutes(-rnd.Next(0, 600)), "Login", "Usuario", u.Id, $"Usuario intentado: {u.NombreUsuario}");
        }
        A(admin.Id, ahora, "Registrar", "Sistema", null, "Datos de ejemplo sembrados para desarrollo y demostraciones");

        _db.Actividades.AddRange(actividades);
        await _db.SaveChangesAsync(ct);
    }
}
