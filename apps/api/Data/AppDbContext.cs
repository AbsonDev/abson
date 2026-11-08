using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SiteContent> SiteContents => Set<SiteContent>();

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
            entity.Property(message => message.Status).HasMaxLength(20).HasDefaultValue("new").IsRequired();
            entity.Property(message => message.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(message => message.CreatedAt);
            entity.HasIndex(message => message.Status);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(120).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<SiteContent>(entity =>
        {
            entity.ToTable("site_content");
            entity.HasKey(content => content.Id);
            entity.Property(content => content.ContentJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
