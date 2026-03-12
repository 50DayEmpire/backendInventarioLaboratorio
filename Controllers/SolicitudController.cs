using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using BackendInventario.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BackendInventario.Models;
using System.ComponentModel.DataAnnotations;

[Authorize] // Requiere autenticación para acceder a cualquier acción en este controlador
[ApiController]
[Route("api/[controller]")]
public class SolicitudController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SolicitudController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Solicitud
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetSolicitudes()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        IQueryable<Solicitud> query = _context.Solicitudes
            .Include(s => s.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(s => s.Usuario); // Para saber quién hizo la solicitud

        if (!isAdmin)
        {
            query = query.Where(s => s.UsuarioId == userId);
        }

        // Usamos LINQ (.Select) para proyectar el resultado a un formato limpio
        // Esto evita enviar datos innecesarios de la tabla IdentityUser al Frontend
        var resultado = await query
            .Select(s => new {
                s.Id,
                s.FechaSolicitud,
                s.Estado,
                s.Comentarios,
                s.MotivoRechazo,
                Usuario = s.Usuario.UserName, // Solo el nombre, no todo el objeto User
                Detalles = s.Detalles.Select(d => new {
                    d.ProductoId,
                    NombreProducto = d.Producto.Nombre,
                    d.Cantidad
                })
            })
            .ToListAsync();

        return Ok(resultado);
    }

    // POST: api/Solicitud
    [HttpPost]
    public async Task<ActionResult> PostSolicitud(SolicitudDto dto)
    {
        // 1. Obtener el ID del usuario directamente del Token
        // Busca específicamente el valor que tiene el GUID en tu JSON
        // 1. Buscamos todos los claims que tengan esa misma URL/Llave
        var claimsIdentificador = User.FindAll("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        // 2. Filtramos el que NO es un correo (el que no tiene '@')
        var userId = claimsIdentificador
            .FirstOrDefault(c => !c.Value.Contains("@"))?.Value;
        Console.WriteLine($"ID de usuario extraído del token2: {userId}");
        
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        // Extraemos todos los IDs de productos que vienen en el DTO
        var idsProductosEnviados = dto.Detalles.Select(d => d.ProductoId).Distinct().ToList();

        // Contamos cuántos de esos IDs existen realmente en la base de datos
        var productosExistentesCount = await _context.Productos
            .Where(p => idsProductosEnviados.Contains(p.Id))
            .CountAsync();

        // Si el conteo no coincide, significa que enviaron al menos un ID falso
        if (productosExistentesCount != idsProductosEnviados.Count)
        {
            return BadRequest("Uno o más IDs de productos no son válidos o no existen.");
        }


        // 2. Mapear del DTO a la Entidad Real
        var nuevaSolicitud = new Solicitud
        {
            UsuarioId = userId,
            Comentarios = dto.Comentarios,
            // Mapeamos la lista de detalles del DTO a la entidad SolicitudProducto
            Detalles = dto.Detalles.Select(d => new SolicitudProducto
            {
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad
            }).ToList()
        };

        // 3. Guardar en la base de datos
        _context.Solicitudes.Add(nuevaSolicitud);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSolicitudes), new { id = nuevaSolicitud.Id }, new {
        nuevaSolicitud.Id,
        nuevaSolicitud.FechaSolicitud,
        nuevaSolicitud.Estado,
        TotalProductos = nuevaSolicitud.Detalles.Count
    });
    }

    [HttpPost("{id}/aprobar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Aprobar(int id)
    {
        // Iniciamos una transacción para que si algo falla, no se guarde nada a medias
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var solicitud = await _context.Solicitudes
                .Include(s => s.Detalles)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null) return NotFound();
            if (solicitud.Estado == "Aprobado") return BadRequest("Esta solicitud ya fue aprobada previamente.");

            solicitud.Estado = "Aprobado";
            solicitud.FechaRespuesta = DateTime.Now;
            solicitud.MotivoRechazo = ""; // Limpiamos cualquier motivo de rechazo previo

            // 1. Obtener IDs y productos de forma eficiente
            var productoIds = solicitud.Detalles.Select(d => d.ProductoId).ToList();
            var productos = await _context.Productos
                .Where(p => productoIds.Contains(p.Id))
                .ToListAsync();

            // 2. Validar Stock y Restar
            foreach (var detalle in solicitud.Detalles)
            {
                var producto = productos.FirstOrDefault(p => p.Id == detalle.ProductoId);
                
                if (producto == null)
                {
                    await transaction.RollbackAsync(); // <--- IMPORTANTE: Limpiar antes de salir
                    return BadRequest($"El producto con ID {detalle.ProductoId} ya no existe.");
                }

                if (producto.Cantidad < detalle.Cantidad)
                {
                    await transaction.RollbackAsync(); // <--- IMPORTANTE: Limpiar antes de salir
                    return BadRequest($"Stock insuficiente para '{producto.Nombre}'.");
                }

                producto.Cantidad -= detalle.Cantidad;
            }

            // 3. Guardar cambios
            await _context.SaveChangesAsync();
            
            // 4. Confirmar la transacción
            await transaction.CommitAsync();

            return Ok(new { message = "Solicitud aprobada e inventario actualizado con éxito." });
        }
        catch (Exception)
        {
            // Si hay un error inesperado (ej. se cayó la DB), deshacemos los cambios
            await transaction.RollbackAsync();
            return StatusCode(500, "Error interno al procesar la aprobación.");
        }
    }

    // POST: api/Solicitud/5/rechazar
    [HttpPost("{id}/rechazar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] string motivo)
    {
        var solicitud = await _context.Solicitudes.FindAsync(id);
        if (solicitud == null) return NotFound();
        if (solicitud.Estado == "Rechazado") return BadRequest("Esta solicitud ya fue rechazada previamente.");

        solicitud.Estado = "Rechazado";
        solicitud.MotivoRechazo = motivo;
        solicitud.FechaRespuesta = DateTime.Now;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Solicitud rechazada" });
    }


}

public class SolicitudDto
{
    public string? Comentarios { get; set; }

    [MinLength(1, ErrorMessage = "La solicitud debe tener al menos un producto")]
    public List<SolicitudProductoDto> Detalles { get; set; } = new List<SolicitudProductoDto>();
}

public class SolicitudProductoDto
{
    [Required]
    public int ProductoId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; }
}