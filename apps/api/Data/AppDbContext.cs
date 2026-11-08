using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

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

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.ToTable("contact_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Name).HasMaxLength(120).IsRequired();
            entity.Property(message => message.Email).HasMaxLength(254).IsRequired();
            entity.Property(message => message.Subject).HasMaxLength(120).IsRequired();
            entity.Property(message => message.Message).HasMaxLength(4000).IsRequired();
            entity.Property(message => message.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(message => message.CreatedAt);
        });
    }
}
