using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BackendInventario.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Models.Categoria> Categorias { get; set; }
        public DbSet<Models.Producto> Productos { get; set; }
        
        public DbSet<Models.Solicitud> Solicitudes { get; set; }
        public DbSet<Models.SolicitudProducto> SolicitudProductos { get; set; }
    }
}