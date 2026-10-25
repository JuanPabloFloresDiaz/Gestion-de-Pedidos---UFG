using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Usuarios;

/// <summary>Caso de uso: listar usuarios con filtros y paginación.</summary>
public sealed class ListarUsuarios
{
    private readonly IUsuarioRepository _usuarios;

    /// <summary>Crea el caso de uso.</summary>
    public ListarUsuarios(IUsuarioRepository usuarios) => _usuarios = usuarios;

    /// <summary>Devuelve una página de usuarios.</summary>
    /// <param name="q">Texto buscado en usuario, nombre o correo.</param>
    /// <param name="rolId">Filtra por rol.</param>
    /// <param name="activo">Filtra por estado; <c>null</c> = todos.</param>
    /// <param name="pagina">Página (desde 1).</param>
    /// <param name="tamano">Elementos por página (máximo 100).</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<PaginaDto<UsuarioDto>> EjecutarAsync(string? q, int? rolId, bool? activo, int? pagina,
        int? tamano, CancellationToken ct)
    {
        var (p, t) = Paginacion.Normalizar(pagina, tamano);
        var (elementos, total) = await _usuarios.BuscarAsync(q?.Trim(), rolId, activo, p, t, ct);
        return new PaginaDto<UsuarioDto>(elementos.Select(u => u.ADto()).ToList(), total, p, t);
    }
}
