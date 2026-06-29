using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CargosController(AppDbContext db, CuotaService cuotaService, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CargoDto>>> GetAll(
        [FromQuery] int? socioId, [FromQuery] int? clienteId,
        [FromQuery] EstadoPago? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var query = db.Cargos.ForTenant(tenant)
            .Include(c => c.Servicio)
            .Include(c => c.Socio)
            .Include(c => c.Cliente)
            .Where(c => c.EstadoPago != EstadoPago.Anulado)
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
        var emisorId = tenant.RequireEmisorId();
        var errors = ValidationHelper.ValidateCargo(dto.ServicioId, dto.SocioId, dto.ClienteId, dto.Cantidad);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var servicio = await db.Servicios.ForTenant(tenant).FirstOrDefaultAsync(s => s.Id == dto.ServicioId);
        if (servicio == null)
            return BadRequest(new { mensaje = "Debés seleccionar un servicio válido", errores = new[] { "Debés seleccionar un servicio" } });
        if (!servicio.Activo)
            return BadRequest(new { mensaje = "El servicio no está activo" });
        if (servicio.Precio <= 0)
            return BadRequest(new { mensaje = "El servicio seleccionado no tiene un precio válido" });

        if (dto.ClienteId != null && servicio.SoloSocios)
            return BadRequest(new { mensaje = "Este servicio es exclusivo para socios" });

        if (dto.SocioId != null)
        {
            var socio = await db.Socios.ForTenant(tenant).FirstOrDefaultAsync(s => s.Id == dto.SocioId);
            if (socio == null)
                return BadRequest(new { mensaje = "Socio no encontrado" });
            if (socio.Estado != EstadoSocio.Activo)
                return BadRequest(new { mensaje = "El socio no está activo" });
        }

        if (dto.ClienteId != null && !await db.Clientes.ForTenant(tenant).AnyAsync(c => c.Id == dto.ClienteId))
            return BadRequest(new { mensaje = "Cliente no encontrado" });

        var cargo = new Cargo
        {
            EmisorId = emisorId,
            ServicioId = dto.ServicioId,
            SocioId = dto.SocioId,
            ClienteId = dto.ClienteId,
            Monto = servicio.Precio,
            Cantidad = dto.Cantidad,
            SumarACuota = dto.SumarACuota && dto.SocioId != null,
            Notas = dto.Notas,
            AtendidoPor = dto.AtendidoPor,
            EstadoPago = EstadoPago.Pendiente
        };

        if (dto.SocioId != null && dto.SumarACuota)
        {
            var (mes, anio) = UruguayTime.MesAnioActual();
            var cuota = await cuotaService.ObtenerOCrearCuotaAsync(dto.SocioId.Value, mes, anio);
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
        var emisorId = tenant.RequireEmisorId();
        var cargo = await db.Cargos.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == id);
        if (cargo == null) return NotFound();

        if (cargo.EstadoPago == EstadoPago.Anulado)
            return BadRequest(new { mensaje = "El cargo está anulado" });

        if (cargo.SumarACuota)
            return BadRequest(new { mensaje = "Este cargo se cobra en la cuota mensual" });

        if (cargo.EstadoPago == EstadoPago.Pagado)
            return BadRequest(new { mensaje = "El cargo ya está pagado" });

        var pagoErrors = ValidationHelper.ValidateMontoPago(dto.Monto);
        if (pagoErrors.Count > 0) return ValidationHelper.ToBadRequest(pagoErrors);

        var montoTotal = cargo.Monto * cargo.Cantidad;
        var yaPagado = await db.Pagos.ForTenant(tenant).Where(p => p.CargoId == id).SumAsync(p => p.Monto);
        var saldoPendiente = montoTotal - yaPagado;

        if (dto.Monto > saldoPendiente)
            return BadRequest(new { mensaje = $"El monto supera el saldo pendiente ({saldoPendiente:N0} UYU)" });

        var pago = new Pago
        {
            EmisorId = emisorId,
            CargoId = id,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Referencia = dto.Referencia,
            RegistradoPor = dto.RegistradoPor,
            Notas = dto.Notas
        };

        db.Pagos.Add(pago);

        var totalPagado = yaPagado + dto.Monto;
        cargo.EstadoPago = totalPagado >= montoTotal ? EstadoPago.Pagado :
            totalPagado > 0 ? EstadoPago.Parcial : EstadoPago.Pendiente;

        await db.SaveChangesAsync();

        return new PagoDto(pago.Id, pago.Monto, pago.MetodoPago, pago.Fecha,
            pago.Referencia, pago.RegistradoPor, pago.CargoId, pago.CuotaMensualId);
    }

    [HttpPost("{id}/anular")]
    public async Task<ActionResult<CargoDto>> AnularCargo(int id, AnularCargoDto dto)
    {
        var cargo = await db.Cargos.ForTenant(tenant)
            .Include(c => c.Servicio)
            .Include(c => c.Socio)
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cargo == null) return NotFound();

        if (cargo.EstadoPago != EstadoPago.Pendiente)
            return BadRequest(new { mensaje = "Solo se pueden anular cargos pendientes" });

        if (await db.Pagos.ForTenant(tenant).AnyAsync(p => p.CargoId == id))
            return BadRequest(new { mensaje = "No se puede anular un cargo con pagos registrados" });

        var cuotaId = cargo.CuotaMensualId;
        cargo.EstadoPago = EstadoPago.Anulado;
        if (!string.IsNullOrWhiteSpace(dto.Motivo))
            cargo.Notas = string.IsNullOrWhiteSpace(cargo.Notas)
                ? $"[Anulado] {dto.Motivo.Trim()}"
                : $"{cargo.Notas} [Anulado] {dto.Motivo.Trim()}";

        await db.SaveChangesAsync();

        if (cuotaId.HasValue)
            await cuotaService.ActualizarMontoServiciosAsync(cuotaId.Value);

        return Map(cargo);
    }

    private static CargoDto Map(Cargo c) => new(
        c.Id, c.ServicioId, c.Servicio?.Nombre ?? "",
        c.SocioId, c.Socio != null ? $"{c.Socio.Nombre} {c.Socio.Apellido}" : null,
        c.ClienteId, c.Cliente != null ? $"{c.Cliente.Nombre} {c.Cliente.Apellido}" : null,
        c.Fecha, c.Monto, c.Cantidad, c.EstadoPago, c.SumarACuota, c.Notas);
}
