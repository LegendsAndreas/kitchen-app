using Microsoft.EntityFrameworkCore;

namespace WebKitchen.Services;

using Data;

public class ContextModelService
{
    public AppDbContext Context { get; set; }
    
    public ContextModelService(AppDbContext context)
    {
        Context = context;
    }

    public async Task Test()
    {
        Recipe? recipe = await Context.Recipes.FirstOrDefaultAsync(r => r.Name == "Menemen");

        if (recipe == null)
        {
            Console.WriteLine("Recipe not found");
            return;
        }

        recipe.PrintRecipe();
    }
}