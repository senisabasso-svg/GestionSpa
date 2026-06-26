using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<CuotaMensual> CuotasMensuales => Set<CuotaMensual>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Ingreso> Ingresos => Set<Ingreso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Socio>(e =>
        {
            e.HasIndex(s => s.NumeroSocio).IsUnique();
            e.HasIndex(s => s.Cedula);
            e.Property(s => s.CuotaMensual).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Servicio>(e =>
        {
            e.Property(s => s.Precio).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Cargo>(e =>
        {
            e.Property(c => c.Monto).HasPrecision(12, 2);
            e.HasOne(c => c.Socio).WithMany(s => s.Cargos).HasForeignKey(c => c.SocioId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Cliente).WithMany(cl => cl.Cargos).HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CuotaMensual>(e =>
        {
            e.HasIndex(c => new { c.SocioId, c.Mes, c.Anio }).IsUnique();
            e.Property(c => c.MontoCuota).HasPrecision(12, 2);
            e.Property(c => c.MontoServicios).HasPrecision(12, 2);
            e.Property(c => c.MontoPagado).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Pago>(e =>
        {
            e.Property(p => p.Monto).HasPrecision(12, 2);
        });
    }
}
