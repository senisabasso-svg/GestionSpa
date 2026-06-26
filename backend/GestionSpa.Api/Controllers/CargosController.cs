using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CargosController(AppDbContext db, CuotaService cuotaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CargoDto>>> GetAll(
        [FromQuery] int? socioId, [FromQuery] int? clienteId,
        [FromQuery] EstadoPago? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var query = db.Cargos
            .Include(c => c.Servicio)
            .Include(c => c.Socio)
            .Include(c => c.Cliente)
            .AsQueryable();

        if (socioId.HasValue) query = query.Where(c => c.SocioId == socioId);
        if (clienteId.HasValue) query = query.Where(c => c.ClienteId == clienteId);
        if (estado.HasValue) query = query.Where(c => c.EstadoPago == estado);
        if (desde.HasValue) query = query.Where(c => c.Fecha >= desde);
        if (hasta.HasValue) query = query.Where(c => c.Fecha <= hasta);

        var cargos = await query.OrderByDescending(c => c.Fecha).ToListAsync();
        return cargos.Select(Map).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CargoDto>> Create(CrearCargoDto dto)
    {
        if (dto.SocioId == null && dto.ClienteId == null)
            return BadRequest(new { mensaje = "Debe indicar un socio o un cliente" });

        if (dto.SocioId != null && dto.ClienteId != null)
            return BadRequest(new { mensaje = "No puede indicar socio y cliente a la vez" });

        var servicio = await db.Servicios.FindAsync(dto.ServicioId);
        if (servicio == null) return NotFound(new { mensaje = "Servicio no encontrado" });
        if (!servicio.Activo) return BadRequest(new { mensaje = "El servicio no está activo" });

        if (dto.ClienteId != null && servicio.SoloSocios)
            return BadRequest(new { mensaje = "Este servicio es exclusivo para socios" });

        var cargo = new Cargo
        {
            ServicioId = dto.ServicioId,
            SocioId = dto.SocioId,
            ClienteId = dto.ClienteId,
            Monto = servicio.Precio,
            Cantidad = dto.Cantidad,
            SumarACuota = dto.SumarACuota && dto.SocioId != null,
            Notas = dto.Notas,
            AtendidoPor = dto.AtendidoPor,
            EstadoPago = dto.SocioId != null && dto.SumarACuota ? EstadoPago.Pendiente : EstadoPago.Pendiente
        };

        if (dto.SocioId != null && dto.SumarACuota)
        {
            var ahora = DateTime.UtcNow;
            var cuota = await cuotaService.ObtenerOCrearCuotaAsync(dto.SocioId.Value, ahora.Month, ahora.Year);
            cargo.CuotaMensualId = cuota.Id;
        }

        db.Cargos.Add(cargo);
        await db.SaveChangesAsync();

        if (cargo.CuotaMensualId.HasValue)
            await cuotaService.ActualizarMontoServiciosAsync(cargo.CuotaMensualId.Value);

        await db.Entry(cargo).Reference(c => c.Servicio).LoadAsync();
        if (cargo.SocioId.HasValue) await db.Entry(cargo).Reference(c => c.Socio).LoadAsync();
        if (cargo.ClienteId.HasValue) await db.Entry(cargo).Reference(c => c.Cliente).LoadAsync();

        return CreatedAtAction(nameof(GetAll), Map(cargo));
    }

    [HttpPost("{id}/pagar")]
    public async Task<ActionResult<PagoDto>> PagarCargo(int id, RegistrarPagoDto dto)
    {
        var cargo = await db.Cargos.FindAsync(id);
        if (cargo == null) return NotFound();

        var pago = new Pago
        {
            CargoId = id,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Referencia = dto.Referencia,
            RegistradoPor = dto.RegistradoPor,
            Notas = dto.Notas
        };

        db.Pagos.Add(pago);

        var totalPagado = await db.Pagos.Where(p => p.CargoId == id).SumAsync(p => p.Monto) + dto.Monto;
        var montoTotal = cargo.Monto * cargo.Cantidad;

        cargo.EstadoPago = totalPagado >= montoTotal ? EstadoPago.Pagado :
            totalPagado > 0 ? EstadoPago.Parcial : EstadoPago.Pendiente;

        await db.SaveChangesAsync();

        return new PagoDto(pago.Id, pago.Monto, pago.MetodoPago, pago.Fecha,
            pago.Referencia, pago.RegistradoPor, pago.CargoId, pago.CuotaMensualId);
    }

    private static CargoDto Map(Cargo c) => new(
        c.Id, c.ServicioId, c.Servicio?.Nombre ?? "",
        c.SocioId, c.Socio != null ? $"{c.Socio.Nombre} {c.Socio.Apellido}" : null,
        c.ClienteId, c.Cliente != null ? $"{c.Cliente.Nombre} {c.Cliente.Apellido}" : null,
        c.Fecha, c.Monto, c.Cantidad, c.EstadoPago, c.SumarACuota, c.Notas);
}
