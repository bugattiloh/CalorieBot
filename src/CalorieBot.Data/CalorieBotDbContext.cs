using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CalorieBot.Data;

/// <summary>
/// Контекст EF Core. Схему описываю через Fluent API, атрибутами в сущностях не сорю.
/// </summary>
public class CalorieBotDbContext : DbContext
{
    public CalorieBotDbContext(DbContextOptions<CalorieBotDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<FavoriteProduct> FavoriteProducts => Set<FavoriteProduct>();

    public DbSet<FoodLogEntry> FoodLog => Set<FoodLogEntry>();

    public DbSet<CalorieCycle> CalorieCycles => Set<CalorieCycle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);

            // Id приходит из Telegram, поэтому генерацию значения отключаю.
            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Username).HasColumnType("text");
            entity.Property(e => e.FirstName).HasColumnType("text");
            entity.Property(e => e.TrackingMode).HasConversion<int>().IsRequired().HasDefaultValue(CalorieTrackingMode.Calories);
            entity.Property(e => e.DailyCalorieLimit).IsRequired().HasDefaultValue(2000);
            entity.Property(e => e.DailyProteinsLimit).HasColumnType("numeric(7,2)");
            entity.Property(e => e.DailyFatsLimit).HasColumnType("numeric(7,2)");
            entity.Property(e => e.DailyCarbsLimit).HasColumnType("numeric(7,2)");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.Property(e => e.GoalSetAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CycleStartedAt).IsRequired().HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<FavoriteProduct>(entity =>
        {
            entity.ToTable("FavoriteProducts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasColumnType("text").IsRequired();
            entity.Property(e => e.Calories).IsRequired();
            entity.Property(e => e.Proteins).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Fats).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Carbs).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.IsFixedServing).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.ServingSize).HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.FavoriteProducts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Один и тот же продукт дважды в избранном мне не нужен.
            entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<FoodLogEntry>(entity =>
        {
            entity.ToTable("FoodLog");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductName).HasColumnType("text").IsRequired();
            entity.Property(e => e.Calories).IsRequired();
            entity.Property(e => e.Proteins).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Fats).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Carbs).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.ServingSize).HasColumnType("text");
            entity.Property(e => e.MealType).HasConversion<int>();
            entity.Property(e => e.LoggedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.Property(e => e.IsFavorite).HasDefaultValue(false);

            entity.HasOne(e => e.User)
                .WithMany(u => u.FoodLog)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Избранное могут удалить, а история должна остаться — поэтому SetNull.
            entity.HasOne(e => e.FavoriteProduct)
                .WithMany()
                .HasForeignKey(e => e.FavoriteProductId)
                .OnDelete(DeleteBehavior.SetNull);

            // Главный запрос бота — «что съедено за сегодня», под него и индекс.
            entity.HasIndex(e => new { e.UserId, e.LoggedAt });
        });

        modelBuilder.Entity<CalorieCycle>(entity =>
        {
            entity.ToTable("CalorieCycles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CalorieLimit).IsRequired();
            entity.Property(e => e.ConsumedCalories).IsRequired();
            entity.Property(e => e.Proteins).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Fats).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.Carbs).HasColumnType("numeric(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.StartedAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.EndedAt).HasColumnType("timestamp with time zone").IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Cycles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // История листается новыми сверху — под этот порядок и индекс.
            entity.HasIndex(e => new { e.UserId, e.EndedAt });
        });
    }
}
