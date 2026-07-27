using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloggerBazar.Infrastructure.Persistence;

public sealed class BloggerBazarDbContextFactory : IDesignTimeDbContextFactory<BloggerBazarDbContext>
{
    public BloggerBazarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=bloggerbazar";
        var options = new DbContextOptionsBuilder<BloggerBazarDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new BloggerBazarDbContext(options);
    }
}
