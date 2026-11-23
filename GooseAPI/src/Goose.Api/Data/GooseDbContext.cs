using Goose.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Goose.Api.Data;

/// <summary>
/// SQL Server store behind the Goose API: conversations, their messages, and
/// the settings the desktop clients sync.
/// </summary>
public sealed class GooseDbContext : DbContext
{
    public GooseDbContext(DbContextOptions<GooseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>(conversation =>
        {
            conversation.ToTable("Conversations");
            conversation.HasKey(x => x.Id);
            conversation.Property(x => x.Title).HasMaxLength(200).IsRequired();
            conversation.Property(x => x.CreatedAt).IsRequired();
            conversation.Property(x => x.UpdatedAt).IsRequired();
            conversation.HasIndex(x => x.UpdatedAt);
            conversation
                .HasMany(x => x.Messages)
                .WithOne(x => x.Conversation!)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(message =>
        {
            message.ToTable("Messages");
            message.HasKey(x => x.Id);
            message.Property(x => x.Role).HasMaxLength(32).IsRequired();
            message.Property(x => x.Content).IsRequired();
            message.Property(x => x.CreatedAt).IsRequired();
            message.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        });

        modelBuilder.Entity<Setting>(setting =>
        {
            setting.ToTable("Settings");
            setting.HasKey(x => x.Key);
            setting.Property(x => x.Key).HasMaxLength(200).IsRequired();
            setting.Property(x => x.Value).IsRequired();
            setting.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}
