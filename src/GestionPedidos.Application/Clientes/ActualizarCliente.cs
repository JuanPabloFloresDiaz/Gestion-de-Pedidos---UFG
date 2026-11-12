using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: actualizar datos de un cliente existente ([Clientes.md §3]).</summary>
public sealed class ActualizarCliente
{
    private readonly IClienteRepository _clientes;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso.</summary>
    public ActualizarCliente(IClienteRepository clientes, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _clientes = clientes;
        _uow = uow;
        _eventos = eventos;
    }

    /// <summary>Actualiza el cliente si está activo y cumple las reglas de negocio.</summary>
    public async Task<Resultado<ClienteDto>> EjecutarAsync(int id, ActualizarClienteSolicitud s, int actorId, CancellationToken ct)
    {
        var invalido = ValidacionesCliente.Campos(s.Nombres, s.Apellidos, s.Telefono, s.Direccion, s.Correo);
        if (invalido is not null) return invalido;

        invalido = ValidacionesCliente.Version(s.RowVersion, out var version);
        if (invalido is not null) return invalido;

        var cliente = await _clientes.ObtenerPorIdAsync(id, ct);
        if (cliente is null)
            return Resultado<ClienteDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El cliente no existe.");

        if (!cliente.Activo)
            return Resultado<ClienteDto>.Falla(TipoError.Regla, CodigosError.Regla, "El cliente está inactivo: primero debe reactivarlo antes de editarlo.");

        if (!await _clientes.ExisteCiudadAsync(s.CiudadId, ct))
            return Resultado<ClienteDto>.Falla(TipoError.Regla, CodigosError.Regla, "La ciudad seleccionada no existe.");

        if (!string.IsNullOrWhiteSpace(s.Correo) && await _clientes.ExisteCorreoAsync(s.Correo.Trim(), cliente.Id, ct))
            return Resultado<ClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Ya existe otro cliente con ese correo electrónico.");

        _clientes.FijarVersionOriginal(cliente, version);

        cliente.Actualizar(s.Nombres, s.Apellidos, s.Telefono, s.Direccion, s.Correo, s.CiudadId);

        try
        {
            await _uow.GuardarAsync(ct);
        }
        catch (ConcurrenciaException)
        {
            return Resultado<ClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El cliente fue modificado por otra persona. Recargue la pantalla e intente de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Cliente",
            EntidadId = cliente.Id,
            Tipo = TipoCambioServidor.Actualizada,
            UsuarioId = actorId,
            Detalle = $"Cliente {cliente.NombreCompleto} actualizado",
        });

        var actualizado = await _clientes.ObtenerPorIdAsync(cliente.Id, ct);
        return Resultado<ClienteDto>.Ok((actualizado ?? cliente).ADto());
    }
}
