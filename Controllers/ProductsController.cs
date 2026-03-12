using Microsoft.AspNetCore.Mvc;
using BackendInventario.Data;
using System.ComponentModel.DataAnnotations;
using BackendInventario.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]

public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    private readonly IWebHostEnvironment _env;

    public ProductsController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [Authorize (Roles = "Editor,Admin")] // Solo usuarios con rol Editor pueden crear productos
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] dtoProducto producto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (producto.Imagen != null){
            try
            {
                //Guardamos el nombre del archivo en la base de datos para poder acceder a él posteriormente
                producto.RutaImagen = await GuardarImagenAsync(producto.Imagen);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        } else{
            // Si no se sube imagen, asignar un valor por defecto
            producto.RutaImagen = "imagenes/default.jpg";
        }

        Producto nuevoProducto = new Producto
        {
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion, 
            CategoriaId = producto.CategoriaId,
            Cantidad = producto.Cantidad,
            RutaImagen = producto.RutaImagen
        };

        _context.Productos.Add(nuevoProducto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = nuevoProducto.Id }, nuevoProducto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Productos.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        

        if (!string.IsNullOrEmpty(product.RutaImagen)){
            EliminarImagen(product.RutaImagen);
        }

        _context.Productos.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize] // Solo usuarios autenticados pueden acceder a esta acción
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Productos.ToListAsync();
        return Ok(products);
    }

    [Authorize] // Solo usuarios autenticados pueden acceder a esta acción
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _context.Productos.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        return Ok(product);
    }

    [Authorize(Roles = "Editor,Admin")] // Solo usuarios con rol Editor pueden actualizar productos
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(int id, [FromForm] dtoActualizarProducto updatedProduct)
    {
        var existingProduct = await _context.Productos.FindAsync(id);
        if (existingProduct == null)
        {
            return NotFound();
        }

        if(!string.IsNullOrEmpty(updatedProduct.Nombre)){
            existingProduct.Nombre = updatedProduct.Nombre;
        }

        if (updatedProduct.Descripcion != null)
        {
            existingProduct.Descripcion = updatedProduct.Descripcion;
        }

        bool categoriaIdPresente = Request.Form.ContainsKey("CategoriaId");
        if (categoriaIdPresente && !updatedProduct.CategoriaId.HasValue)
        {
            existingProduct.CategoriaId = null; // Eliminar la categoría si se envía pero no tiene valor
        }
        if (updatedProduct.CategoriaId.HasValue)
        {
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == updatedProduct.CategoriaId);
            if (!categoriaExiste) return BadRequest("La categoría especificada no existe.");
            existingProduct.CategoriaId = updatedProduct.CategoriaId;
        }
        
        if (updatedProduct.Cantidad.HasValue){
            existingProduct.Cantidad = updatedProduct.Cantidad.Value;
        }

        if(updatedProduct.EliminarImagen){
            EliminarImagen(existingProduct.RutaImagen!);
            existingProduct.RutaImagen = "imagenes/default.jpg"; // O asignar una imagen por defecto si lo prefieres
        }

        if (updatedProduct.Imagen != null)
        {
            if (!string.IsNullOrEmpty(existingProduct.RutaImagen) && existingProduct.RutaImagen != "imagenes/default.jpg"){
                EliminarImagen(existingProduct.RutaImagen);
            }
            existingProduct.RutaImagen = await GuardarImagenAsync(updatedProduct.Imagen);
        }

        await _context.SaveChangesAsync();

        return Ok(existingProduct);
    }

    private async Task<string> GuardarImagenAsync(IFormFile imagen)
    {
        if (imagen == null || imagen.Length == 0){
            throw new ArgumentException("Archivo inválido.");
        }

        //Obtenemos la extensión del archivo para asegurarnos de que se guarde con el formato correcto
        var extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();
        //Validampos que la extensión sea de un formato de imagen permitido
        var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (!extensionesPermitidas.Contains(extension))
        {
            throw new InvalidOperationException("Formato de imagen no permitido.");
        }
        //Generamos un nombre único para el archivo para evitar colisiones
        var nombreUnico = $"{Guid.NewGuid()}{extension}";
        var ruta = Path.Combine("wwwroot/imagenes", nombreUnico);

        //Guardamos el archivo en el servidor
        using var stream = new FileStream(ruta, FileMode.Create);
        await imagen.CopyToAsync(stream);

        return "imagenes/" + nombreUnico; // Devolvemos la ruta relativa para guardarla en la base de datos
    }

    private void EliminarImagen(string rutaImagen)
    {
        
        var imagePath = Path.Combine(_env.WebRootPath, rutaImagen);
        
        if (System.IO.File.Exists(imagePath)){
            System.IO.File.Delete(imagePath);
        }
    }
}

public class dtoProducto
{
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    
    public int? CategoriaId { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa")]
    public required int Cantidad { get; set; }
    public string RutaImagen { get; set; } = string.Empty;
    public IFormFile? Imagen { get; set; }
}   
public class dtoActualizarProducto
{
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public int? CategoriaId { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa")]
    public int? Cantidad { get; set; }
    public string RutaImagen { get; set; } = string.Empty;
    public IFormFile? Imagen { get; set; }
    public bool EliminarImagen { get; set; } = false; // Nueva propiedad para indicar si se debe eliminar la imagen actual
}   