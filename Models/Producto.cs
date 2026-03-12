using System.ComponentModel.DataAnnotations;

namespace BackendInventario.Models;

public class Producto
{
    public int Id { get; set; }

    [Required]
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? RutaImagen { get; set; }
    [Required]
    public int Cantidad { get; set; }

    public int? CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }
}