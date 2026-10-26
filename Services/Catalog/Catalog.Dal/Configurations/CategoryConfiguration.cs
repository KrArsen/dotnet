using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category", "dbo");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("system");

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // M:N зв'язок з Product через ProductCategory
        builder.HasMany(c => c.ProductCategories)
            .WithOne(pc => pc.Category)
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Статичний сідінг (HasData) довідника категорій із фіксованими Id (з Лаб 01 db/p2/seed.sql)
        builder.HasData(
            new Category
            {
                Id = 1,
                Name = "Веб-розробка",
                Description = "Створення односторінкових сайтів, порталів та веб-сервісів",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            },
            new Category
            {
                Id = 2,
                Name = "Технічна підтримка",
                Description = "Супровід, оновлення безпеки та моніторинг стабільності",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            },
            new Category
            {
                Id = 3,
                Name = "Пошукова оптимізація (SEO)",
                Description = "Технічний аудит, оптимізація семантики та просування",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            },
            new Category
            {
                Id = 4,
                Name = "Маркетинг та реклама (SMM/PPC)",
                Description = "Таргетована реклама, контекст та просування в соцмережах",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            },
            new Category
            {
                Id = 5,
                Name = "Дизайн та айдентика",
                Description = "Розробка логотипів, брендбуків та користувацьких інтерфейсів UI/UX",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            },
            new Category
            {
                Id = 6,
                Name = "Хмарна інфраструктура та ліцензії",
                Description = "Хостинг, SSL-сертифікати та ліцензійне ПЗ",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed",
                IsDeleted = false
            }
        );
    }
}
