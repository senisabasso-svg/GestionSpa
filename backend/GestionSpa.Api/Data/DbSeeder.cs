using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Servicios.AnyAsync()) return;

        var servicios = new List<Servicio>
        {
            new() { Nombre = "Masaje Relajante", Descripcion = "Masaje corporal completo con aceites esenciales", Categoria = CategoriaServicio.Masajes, Precio = 1200, DuracionMinutos = 50 },
            new() { Nombre = "Masaje Descontracturante", Descripcion = "Masaje profundo para aliviar tensiones musculares", Categoria = CategoriaServicio.Masajes, Precio = 1400, DuracionMinutos = 50 },
            new() { Nombre = "Masaje con Piedras Calientes", Descripcion = "Terapia con piedras volcánicas en aguas termales", Categoria = CategoriaServicio.Masajes, Precio = 1800, DuracionMinutos = 60 },
            new() { Nombre = "Reflexología Podal", Descripcion = "Masaje en pies y puntos de presión", Categoria = CategoriaServicio.Masajes, Precio = 900, DuracionMinutos = 40 },
            new() { Nombre = "Fango Facial", Descripcion = "Tratamiento facial con fango termal del Daymán", Categoria = CategoriaServicio.Facial, Precio = 650, DuracionMinutos = 30 },
            new() { Nombre = "Hidromasaje", Descripcion = "Sesión en hidromasaje individual", Categoria = CategoriaServicio.Termal, Precio = 500, DuracionMinutos = 30 },
            new() { Nombre = "Sauna Seco", Descripcion = "Acceso a sauna seco", Categoria = CategoriaServicio.Termal, Precio = 350, DuracionMinutos = 20 },
            new() { Nombre = "Sauna Húmedo", Descripcion = "Acceso a sauna de vapor", Categoria = CategoriaServicio.Termal, Precio = 350, DuracionMinutos = 20 },
            new() { Nombre = "Drenaje Linfático", Descripcion = "Masaje para estimular el sistema linfático", Categoria = CategoriaServicio.Corporal, Precio = 1300, DuracionMinutos = 50 },
            new() { Nombre = "Paquete Bienestar Daymán", Descripcion = "Masaje + fango facial + hidromasaje", Categoria = CategoriaServicio.Paquetes, Precio = 2200, DuracionMinutos = 90 },
            new() { Nombre = "Acceso Zona Termal", Descripcion = "Acceso a piscinas termales techadas y al aire libre", Categoria = CategoriaServicio.Termal, Precio = 250, DuracionMinutos = 0, SoloSocios = false },
        };

        db.Servicios.AddRange(servicios);

        var socios = new List<Socio>
        {
            new() { NumeroSocio = "1001", Nombre = "María", Apellido = "González", Cedula = "1.234.567-8", Telefono = "098 123 456", Email = "maria@email.com", CuotaMensual = 3500, MedioPago = MetodoPago.Transferencia, Ciudad = "Salto", Estado = EstadoSocio.Activo },
            new() { NumeroSocio = "1002", Nombre = "Carlos", Apellido = "Rodríguez", Cedula = "2.345.678-9", Telefono = "099 234 567", Email = "carlos@email.com", CuotaMensual = 3500, MedioPago = MetodoPago.Efectivo, Ciudad = "Salto", Estado = EstadoSocio.Activo },
            new() { NumeroSocio = "1003", Nombre = "Ana", Apellido = "Silva", Cedula = "3.456.789-0", Telefono = "091 345 678", CuotaMensual = 4200, MedioPago = MetodoPago.MercadoPago, Ciudad = "Salto", Estado = EstadoSocio.Activo },
            new() { NumeroSocio = "1004", Nombre = "Jorge", Apellido = "Pérez", Cedula = "4.567.890-1", Telefono = "092 456 789", CuotaMensual = 3500, MedioPago = MetodoPago.TarjetaDebito, Ciudad = "Salto", Estado = EstadoSocio.Suspendido },
        };

        db.Socios.AddRange(socios);
        await db.SaveChangesAsync();

        var ahora = DateTime.UtcNow;
        var mes = ahora.Month;
        var anio = ahora.Year;

        foreach (var socio in socios.Where(s => s.Estado == EstadoSocio.Activo))
        {
            db.CuotasMensuales.Add(new CuotaMensual
            {
                SocioId = socio.Id,
                Mes = mes,
                Anio = anio,
                MontoCuota = socio.CuotaMensual,
                MontoServicios = socio.NumeroSocio == "1001" ? 1200 : 0,
                EstadoPago = socio.NumeroSocio == "1001" ? EstadoPago.Pagado : EstadoPago.Pendiente,
                MontoPagado = socio.NumeroSocio == "1001" ? socio.CuotaMensual + 1200 : 0,
                FechaVencimiento = new DateTime(anio, mes, 10, 0, 0, 0, DateTimeKind.Utc),
                FechaPago = socio.NumeroSocio == "1001" ? ahora.AddDays(-5) : null
            });
        }

        await db.SaveChangesAsync();
    }
}
