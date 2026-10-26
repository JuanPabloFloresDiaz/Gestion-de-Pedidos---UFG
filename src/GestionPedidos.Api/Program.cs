using System.Text;
using System.Threading.RateLimiting;
using GestionPedidos.Api.Modulos.Auth;
using GestionPedidos.Api.Modulos.Catalogos;
using GestionPedidos.Api.Modulos.Clasificacion;
using GestionPedidos.Api.Modulos.Historial;
using GestionPedidos.Api.Modulos.Roles;
using GestionPedidos.Api.Modulos.Tienda;
using GestionPedidos.Api.Modulos.Usuarios;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Historial;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Infrastructure;
using GestionPedidos.Infrastructure.Persistence;
using GestionPedidos.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// --- Configuración y servicios --------------------------------------------------------------
var conexion = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");
builder.Services.AddGestionPedidos(conexion);

builder.Services.Configure<OpcionesJwt>(config.GetSection(OpcionesJwt.Seccion));
var jwt = config.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 caracteres (256 bits).");
builder.Services.AddSingleton<IEmisorTokens, EmisorTokens>();
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;   // conserva los nombres originales (sub, name…)
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name", RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
    // Una baja de usuario surte efecto en ≤ 1 minuto (caché de 60 s), no al vencer los 60 min del token.
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var id = ctx.Principal!.IdUsuario();
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var activo = await cache.GetOrCreateAsync($"usuario-activo:{id}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                var repo = ctx.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
                return (await repo.ObtenerPorIdAsync(id, ctx.HttpContext.RequestAborted))?.Activo ?? false;
            });
            if (activo != true) ctx.Fail("Usuario inactivo.");
        },
    };
});
builder.Services.AddPoliticasDePermisos();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var login = config.GetValue("RateLimiting:LoginPorMinuto", 10);
    var registro = config.GetValue("RateLimiting:RegistroPorHora", 5);
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = login, Window = TimeSpan.FromMinutes(1) }));
    var tienda = config.GetValue("RateLimiting:TiendaPorMinuto", 240);
    o.AddPolicy("tienda", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = tienda, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("registro", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = registro, Window = TimeSpan.FromHours(1) }));
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Gestión de Pedidos (UFG)", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

// --- Pipeline -------------------------------------------------------------------------------
app.UseExceptionHandler(errores => errores.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (estado, titulo, codigo) = ex switch
    {
        ConcurrenciaException c => (StatusCodes.Status409Conflict, c.Message, "CONFLICTO"),
        _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.", "ERROR_INTERNO"),
    };
    if (estado == 500) app.Logger.LogError(ex, "Error no controlado");   // el detalle técnico solo va a logs
    ctx.Response.StatusCode = estado;
    await Results.Problem(statusCode: estado, title: titulo,
        extensions: new Dictionary<string, object?> { ["codigo"] = codigo }).ExecuteAsync(ctx);
}));

if (config.GetValue("Swagger:Habilitado", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();

// Con "debe cambiar la clave" solo se permite cambiarla y consultar el propio usuario.
app.Use(async (ctx, next) =>
{
    var ruta = ctx.Request.Path.Value ?? "";
    if (ctx.User.HasClaim(EmisorTokens.ClaimDebeCambiarClave, "true")
        && !ruta.EndsWith("/auth/cambiar-clave", StringComparison.OrdinalIgnoreCase)
        && !ruta.EndsWith("/auth/yo", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await Results.Problem(statusCode: 403, title: "Debe cambiar su contraseña antes de continuar.",
            extensions: new Dictionary<string, object?> { ["codigo"] = "DEBE_CAMBIAR_CLAVE" }).ExecuteAsync(ctx);
        return;
    }
    await next();
});

// Va DESPUÉS del bloqueo anterior: quien debe cambiar la clave recibe ese aviso y no un 403 genérico de permisos.
app.UseAuthorization();


app.MapGet("/health", () => Results.Ok(new { estado = "vivo" })).AllowAnonymous();
app.MapGet("/health/ready", async (GestionPedidosDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { estado = "listo" })
            : Results.Problem(statusCode: 503, title: "Sin conexión a la base de datos."))
    .AllowAnonymous();
app.MapAuth();
app.MapUsuarios();
app.MapRoles();
app.MapCatalogos();
app.MapHistorial();
app.MapClasificacion();
app.MapTienda();

// --- Arranque de la base de datos (migraciones con el usuario dueño + semillas) -------------------
if (config.GetValue("Database:MigrarAlIniciar", false))
{
    var conexionMigraciones = config.GetConnectionString("Migraciones") ?? conexion;
    var opciones = new DbContextOptionsBuilder<GestionPedidosDbContext>()
        .UseSqlServer(conexionMigraciones, sql => sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null)).Options;
    await using (var dbMigraciones = new GestionPedidosDbContext(opciones))
        await dbMigraciones.Database.MigrateAsync();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdministradorInicial>()
        .SembrarAsync(config["Admin:ClaveInicial"], CancellationToken.None);

    // Datos de ejemplo (catálogo, clientes, pedidos…) solo si se pide; en desarrollo el compose lo activa.
    if (config.GetValue("Database:SembrarDatosDeEjemplo", false))
        await scope.ServiceProvider.GetRequiredService<DatosDeEjemplo>().SembrarAsync(CancellationToken.None);
}

// El historial es un suscriptor: se instancia aquí para que escuche desde el primer evento.
app.Services.GetRequiredService<HistorialSubscriber>();

app.Run();

/// <summary>Punto de entrada de la API; público para que las pruebas de integración lo arranquen.</summary>
public partial class Program { }
