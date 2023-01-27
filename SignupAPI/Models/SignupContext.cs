using Microsoft.EntityFrameworkCore;

namespace SignupAPI.Models
{
    public class SignupContext: DbContext
    {
        public SignupContext(DbContextOptions<SignupContext> options)
            : base(options)
        {

        }

        public DbSet<Garagebusiness> Garagebusiness { get; set; }
        public DbSet<Users> Users { get; set; }
    }
}
