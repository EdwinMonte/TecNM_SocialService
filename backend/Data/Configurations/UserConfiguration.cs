using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class UserConfiguration : AuditableEntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.ToTable("users");
        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(254).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.FirstSurname).HasColumnName("first_surname").HasMaxLength(70).IsRequired();
        builder.Property(u => u.SecondSurname).HasColumnName("second_surname").HasMaxLength(70);
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(u => u.SessionVersion).HasColumnName("session_version").HasDefaultValue(0).IsConcurrencyToken();
        builder.Ignore(u => u.FullName);

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_users__username");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_users__email");
    }

    protected override string GetTableName() => "users";
}
