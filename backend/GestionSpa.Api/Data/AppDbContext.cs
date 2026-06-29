using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Emisor> Emisores => Set<Emisor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Familia> Familias => Set<Familia>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<CuotaMensual> CuotasMensuales => Set<CuotaMensual>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Ingreso> Ingresos => Set<Ingreso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Emisor>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Emisor).WithMany(em => em.Usuarios).HasForeignKey(x => x.EmisorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Socio>(e =>
        {
            e.HasIndex(s => new { s.EmisorId, s.NumeroSocio }).IsUnique();
            e.HasIndex(s => new { s.EmisorId, s.Cedula }).IsUnique();
            e.Property(s => s.CuotaMensual).HasPrecision(12, 2);
            e.HasOne(s => s.Emisor).WithMany(em => em.Socios).HasForeignKey(s => s.EmisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Familia).WithMany(f => f.Socios).HasForeignKey(s => s.FamiliaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Familia>(e =>
        {
            e.HasIndex(f => new { f.EmisorId, f.Nombre }).IsUnique();
            e.Property(f => f.CuotaMensual).HasPrecision(12, 2);
            e.HasOne(f => f.Emisor).WithMany(em => em.Familias).HasForeignKey(f => f.EmisorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.HasOne(c => c.Emisor).WithMany(em => em.Clientes).HasForeignKey(c => c.EmisorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Servicio>(e =>
        {
            e.Property(s => s.Precio).HasPrecision(12, 2);
            e.HasOne(s => s.Emisor).WithMany(em => em.Servicios).HasForeignKey(s => s.EmisorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cargo>(e =>
        {
            e.Property(c => c.Monto).HasPrecision(12, 2);
            e.HasOne(c => c.Emisor).WithMany().HasForeignKey(c => c.EmisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Servicio).WithMany(s => s.Cargos).HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Socio).WithMany(s => s.Cargos).HasForeignKey(c => c.SocioId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Cliente).WithMany(cl => cl.Cargos).HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.CuotaMensual).WithMany(q => q.Cargos).HasForeignKey(c => c.CuotaMensualId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CuotaMensual>(e =>
        {
            e.HasIndex(c => new { c.SocioId, c.Mes, c.Anio }).IsUnique();
            e.Property(c => c.MontoCuota).HasPrecision(12, 2);
            e.Property(c => c.MontoServicios).HasPrecision(12, 2);
            e.Property(c => c.MontoPagado).HasPrecision(12, 2);
            e.HasOne(c => c.Emisor).WithMany().HasForeignKey(c => c.EmisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Socio).WithMany(s => s.Cuotas).HasForeignKey(c => c.SocioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pago>(e =>
        {
            e.Property(p => p.Monto).HasPrecision(12, 2);
            e.HasOne(p => p.Emisor).WithMany().HasForeignKey(p => p.EmisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Cargo).WithMany(c => c.Pagos).HasForeignKey(p => p.CargoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.CuotaMensual).WithMany(c => c.Pagos).HasForeignKey(p => p.CuotaMensualId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Ingreso>(e =>
        {
            e.HasOne(i => i.Emisor).WithMany().HasForeignKey(i => i.EmisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Socio).WithMany(s => s.Ingresos).HasForeignKey(i => i.SocioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
