using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: registrar un cliente desde la administración.</summary>
public sealed class RegistrarCliente
{
    private readonly IClienteRepository _clientes;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso.</summary>
    public RegistrarCliente(IClienteRepository clientes, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _clientes = clientes;
        _uow = uow;
        _eventos = eventos;
    }

    /// <summary>Registra el cliente tras validar todas las reglas de negocio.</summary>
    public async Task<Resultado<ClienteDto>> EjecutarAsync(CrearClienteSolicitud s, int actorId, CancellationToken ct)
    {
        var invalido = ValidacionesCliente.Campos(s.Nombres, s.Apellidos, s.Telefono, s.Direccion, s.Correo);
        if (invalido is not null) return invalido;

        if (!await _clientes.ExisteCiudadAsync(s.CiudadId, ct))
            return Resultado<ClienteDto>.Falla(TipoError.Regla, CodigosError.Regla, "La ciudad seleccionada no existe.");

        if (!string.IsNullOrWhiteSpace(s.Correo) && await _clientes.ExisteCorreoAsync(s.Correo.Trim(), null, ct))
            return Resultado<ClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Ya existe un cliente con ese correo electrónico.");

        var cliente = Cliente.Crear(s.Nombres, s.Apellidos, s.Telefono, s.Direccion, s.Correo, s.CiudadId);
        _clientes.Agregar(cliente);
        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Cliente",
            EntidadId = cliente.Id,
            Tipo = TipoCambioServidor.Registrada,
            UsuarioId = actorId,
            Detalle = $"Cliente {cliente.NombreCompleto} registrado",
        });

        var cargado = await _clientes.ObtenerPorIdAsync(cliente.Id, ct);
        return Resultado<ClienteDto>.Ok((cargado ?? cliente).ADto());
    }
}
