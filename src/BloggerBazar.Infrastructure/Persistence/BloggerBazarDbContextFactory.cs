using BloggerBazar.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloggerBazar.Infrastructure.Persistence;

public sealed class BloggerBazarDbContextFactory : IDesignTimeDbContextFactory<BloggerBazarDbContext>
{
    public BloggerBazarDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings__Postgres must be configured.");
        var options = new DbContextOptionsBuilder<BloggerBazarDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new BloggerBazarDbContext(options);
    }
}
