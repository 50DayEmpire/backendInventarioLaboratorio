

using System.ComponentModel.DataAnnotations;
using BackendInventario.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // Solo los usuarios con rol Admin pueden acceder a este controlador
public class UserController : ControllerBase
{
    public ApplicationDbContext _context;
    public UserManager<IdentityUser> _userManager { get; }

    public UserController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET api/User
    [HttpGet()]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var usersWithRoles = await _context.Users
        .Select(u => new 
        {
            u.Id,
            u.UserName,
            Roles = _context.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_context.Roles, 
                    ur => ur.RoleId, 
                    r => r.Id, 
                    (ur, r) => r.Name)
                .ToList()
        })
        .ToListAsync();

        return Ok(usersWithRoles);
    }

    //POST api/User/actualizar-roles
    [HttpPost("actualizar-roles")]
    public async Task<IActionResult> UpdateUserRoles([FromBody] UpdateUserRolesDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null) return NotFound("Usuario no encontrado");

        // 1. Obtener los roles actuales
        var rolesActuales = await _userManager.GetRolesAsync(user);
        var rolAnterior = rolesActuales.FirstOrDefault();

        // 2. Validación: No hacer nada si el rol es el mismo
        if (rolAnterior == dto.Rol) 
            return BadRequest("El usuario ya tiene ese rol asignado.");

        // --- NUEVA VALIDACIÓN: Protección del último Admin ---
        const string ADMIN_ROLE = "Admin";

        // Si el usuario es Admin y el nuevo rol NO es Admin
        if (rolAnterior == ADMIN_ROLE && dto.Rol != ADMIN_ROLE)
        {
            var admins = await _userManager.GetUsersInRoleAsync(ADMIN_ROLE);
            
            if (admins.Count <= 1)
            {
                return BadRequest("No se puede quitar el rol de Admin porque es el único administrador en el sistema.");
            }
        }
        // -----------------------------------------------------

        // 3. Ejecutar cambios
        if (rolAnterior != null) 
        {
            var removeResult = await _userManager.RemoveFromRoleAsync(user, rolAnterior);
            if (!removeResult.Succeeded) return BadRequest("Error al quitar el rol anterior.");
        }

        var addResult = await _userManager.AddToRoleAsync(user, dto.Rol);
        if (!addResult.Succeeded) return BadRequest("Error al asignar el nuevo rol.");

        return Ok(new { message = $"Rol actualizado de {rolAnterior} a {dto.Rol} correctamente." });
    }

    // DELETE api/User/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound("Usuario no encontrado");

        // 1. Verificar si el usuario tiene el rol de Admin
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        
        if (isAdmin)
        {
            // Bloqueo de seguridad: No se borran administradores directamente
            return BadRequest("No se puede eliminar un usuario con rol de Administrador. " +
                            "Debe quitarle el rol de Admin antes de poder eliminarlo.");
        }

        // 2. Proceder con la eliminación si no es Admin
        var result = await _userManager.DeleteAsync(user);
        
        if (!result.Succeeded) 
        {
            return BadRequest("Error al eliminar el usuario.");
        }

        return NoContent();
    }

}

public class UpdateUserRolesDto
{
    [Required]
    public required string UserId { get; set; }
    [Required]
    [RegularExpression("^(Admin|User|Editor)$", 
        ErrorMessage = "El rol debe ser Admin, User o Editor.")]

    public required string Rol { get; set; } 
}
