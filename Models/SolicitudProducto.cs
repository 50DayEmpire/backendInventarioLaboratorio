namespace BackendInventario.Models;

public class SolicitudProducto
{
    // LLAVE SUSTITUTA: Simplifica todo el manejo en EF y Frontend
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public virtual Solicitud Solicitud { get; set; } = null!;

    public int ProductoId { get; set; }
    public virtual Producto Producto { get; set; } = null!;

    // Datos adicionales de la relación
    public int Cantidad { get; set; }

}