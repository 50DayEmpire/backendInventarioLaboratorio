using BackendInventario.Data;
using Microsoft.AspNetCore.Mvc;
using BackendInventario.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriaController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriaController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Categoria
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Categoria>>> GetCategorias()
    {
        var response = await _context.Categorias.Select(c=> new {
            c.Id,
            c.Nombre,
            c.Descripcion,
        }).ToListAsync();
        return Ok(response);    
    }

    // POST: api/Categoria
    [Authorize(Roles = "Admin, Editor")] // Solo Admin y Editor pueden crear categorías
    [HttpPost]
    public async Task<IActionResult> PostCategoria(dtoCategoria categoriaDto)
    {
        var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Nombre == categoriaDto.Nombre);
        if (categoriaExiste)
        {
            return BadRequest($"La categoría '{categoriaDto.Nombre}' ya existe.");
        }

        var categoriaModel = new Categoria
        {
            Nombre = categoriaDto.Nombre,
            Descripcion = categoriaDto.Descripcion
        };

        _context.Categorias.Add(categoriaModel);
        await _context.SaveChangesAsync();

        return Ok($"Categoría '{categoriaDto.Nombre}' creada con éxito.");
    }

    // PUT: api/Categoria/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutCategoria(int id, [FromBody] dtoCategoria categoriaDto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound($"No se encontró la categoría con ID {id}.");
        }

        categoria.Nombre = categoriaDto.Nombre;
        categoria.Descripcion = categoriaDto.Descripcion;

        await _context.SaveChangesAsync();

        return Ok($"Categoría con ID {id} actualizada con éxito.");
    }

    // DELETE: api/Categoria/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategoria(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound($"No se encontró la categoría con ID {id}.");
        }

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        return Ok($"Categoría con ID {id} eliminada con éxito.");
    }
}

public class dtoCategoria
{
    [Required]
    public required string Nombre { get; set; }
    public required string Descripcion { get; set; }
}