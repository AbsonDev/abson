using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("todos");
            entity.HasKey(todo => todo.Id);
            entity.Property(todo => todo.Title).HasMaxLength(200).IsRequired();
            entity.Property(todo => todo.IsDone).HasDefaultValue(false);
            entity.Property(todo => todo.CreatedAt).HasDefaultValueSql("now()");
        });
    }
}
