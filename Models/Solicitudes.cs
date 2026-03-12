namespace BackendInventario.Models;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

public class Solicitud
{
    public int Id { get; set; }

    [Required]
    public required string UsuarioId { get; set; }
    // Propiedad de navegación a IdentityUser
    public virtual IdentityUser Usuario { get; set; } = null!;

    public string Estado { get; set; } = "Pendiente";
    public DateTime FechaSolicitud { get; set; } = DateTime.Now;
    public DateTime? FechaRespuesta { get; set; }
    public string? Comentarios { get; set; }
    public string? MotivoRechazo { get; set; } = "";

    // Relación con el detalle (la tabla intermedia)
    public virtual ICollection<SolicitudProducto> Detalles { get; set; } = new List<SolicitudProducto>();
}