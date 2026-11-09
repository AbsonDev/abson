using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SiteContent> SiteContents => Set<SiteContent>();
    public DbSet<CareerOpsSnapshot> CareerOpsSnapshots => Set<CareerOpsSnapshot>();
    public DbSet<CareerOpsAction> CareerOpsActions => Set<CareerOpsAction>();

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

        modelBuilder.Entity<CareerOpsSnapshot>(entity =>
        {
            entity.ToTable("career_ops_snapshots");
            entity.HasKey(snapshot => snapshot.Id);
            entity.Property(snapshot => snapshot.SnapshotJson).HasColumnType("jsonb").IsRequired();
            entity.Property(snapshot => snapshot.SchemaVersion).HasDefaultValue(1);
            entity.Property(snapshot => snapshot.Source).HasMaxLength(40).HasDefaultValue("career-ops");
        });

        modelBuilder.Entity<CareerOpsAction>(entity =>
        {
            entity.ToTable("career_ops_actions");
            entity.HasKey(action => action.Id);
            entity.Property(action => action.ActionType).HasMaxLength(40).IsRequired();
            entity.Property(action => action.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(action => action.Status).HasMaxLength(20).HasDefaultValue("pending").IsRequired();
            entity.Property(action => action.Result).HasMaxLength(2000);
            entity.Property(action => action.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(action => action.Status);
            entity.HasIndex(action => action.CreatedAt);
        });
    }
}
