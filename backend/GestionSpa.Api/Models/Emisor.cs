namespace GestionSpa.Api.Models;

public class Emisor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    /// <summary>Si el admin del emisor ve Configuración → Portero.</summary>
    public bool MostrarConfigPortero { get; set; }

    /// <summary>Si el admin del emisor ve el botón de sorteo en Informes.</summary>
    public bool MostrarSorteo { get; set; }

    /// <summary>Si se muestra el kiosk Control de Ingreso (menú, panel y URL pública).</summary>
    public bool MostrarControlIngreso { get; set; } = true;

    /// <summary>Si al iniciar sesión se muestra un aviso informativo de pago pendiente (solo cartel).</summary>
    public bool MostrarAvisoPagoPendiente { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<Familia> Familias { get; set; } = [];
    public ICollection<Socio> Socios { get; set; } = [];
    public ICollection<Cliente> Clientes { get; set; } = [];
    public ICollection<Servicio> Servicios { get; set; } = [];
    public EmisorPorteroConfig? PorteroConfig { get; set; }
}
