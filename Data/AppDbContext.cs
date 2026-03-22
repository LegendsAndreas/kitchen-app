using Microsoft.EntityFrameworkCore;
using WebKitchen.Services;

namespace WebKitchen.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<RecipeInstructionRecord> RecipeInstructions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("recipes");
            entity.HasKey(r => r.RecipeId);
            entity.Property(r => r.RecipeId).HasColumnName("id"); // Map RecipeId to 'id' column
            entity.Property(r => r.Name).IsRequired().HasColumnName("name");
            entity.Property(r => r.MealType).IsRequired().HasMaxLength(1).HasColumnName("meal_type");
            entity.Property(r => r.Base64Image).IsRequired().HasColumnName("image");
            entity.Property(r => r.TotalCost).HasColumnName("cost");

            // Owned type for Macros (stored in the same table)
            entity.Property(r => r.TotalMacros)
                .HasColumnName("macros")
                .HasColumnType("recipe_macros");

            // One-to-many relationship with Ingredients
            entity.HasMany(r => r.Ingredients)
                .WithOne()
                .HasForeignKey("RecipeId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Ingredient configuration
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.ToTable("ingredients");
            entity.HasKey("Id");
            entity.Property(i => i.Name).IsRequired();
            entity.Property(i => i.Grams);
            entity.Property(i => i.CaloriesPer100g);
            entity.Property(i => i.CarbsPer100g);
            entity.Property(i => i.ProteinPer100g);
            entity.Property(i => i.FatPer100g);
            entity.Property(i => i.CostPer100g);
            entity.Property(i => i.Multiplier);
            entity.Property(i => i.Base64Image);
        });

        // RecipeInstructionRecord configuration
        modelBuilder.Entity<RecipeInstructionRecord>(entity =>
        {
            entity.ToTable("recipe_instructions");
            entity.HasKey("Id");
            entity.Property("RecipeId").IsRequired();

            // Owned type for RecipeInstructions (stored as JSON)
            entity.OwnsOne(r => r.Instructions, instructions =>
            {
                instructions.Property(i => i.Name);
                instructions.OwnsMany(i => i.Steps, steps =>
                {
                    steps.Property(s => s.StepNumber);
                    steps.Property(s => s.StepText);
                });
                instructions.OwnsMany(i => i.Notes, notes =>
                {
                    notes.Property(n => n.NoteNumber);
                    notes.Property(n => n.NoteText);
                });
            });
        });
    }
}