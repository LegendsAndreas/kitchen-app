using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace WebKitchen;

public class Macros
{
    public float TotalCalories { get; set; }
    public float TotalFats { get; set; }
    public float TotalCarbs { get; set; }
    public float TotalProtein { get; set; }

    public void PrintMacros()
    {
        Console.WriteLine("Printing Macros:");
        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine("Total calories: " + TotalCalories);
        Console.WriteLine("Total fats: " + TotalFats);
        Console.WriteLine("Total carbs: " + TotalCarbs);
        Console.WriteLine("Total protein: " + TotalProtein);
    }
}

public class Ingredient
{
    public string Name { get; set; } = string.Empty;
    public float Grams { get; set; }
    public float Calories { get; set; }
    public float Carbs { get; set; }
    public float Protein { get; set; }
    public float Fats { get; set; }
    public float Multiplier { get; set; }

    public void Clear()
    {
        Name = string.Empty;
        Grams = 0f;
        Calories = 0f;
        Carbs = 0f;
        Protein = 0f;
        Fats = 0f;
        Multiplier = 0f;
    }

    public void SetMultiplier()
    {
        Multiplier = Grams / 100;
    }

    public void PrintIngredient()
    {
        Console.WriteLine("Printing Ingredient___");
        Console.WriteLine("Ingredient: " + Name);
        Console.WriteLine("Calories: " + Grams);
        Console.WriteLine("Calories: " + Calories);
        Console.WriteLine("Carbs: " + Carbs);
        Console.WriteLine("Protein: " + Protein);
        Console.WriteLine("Fats: " + Fats);
        Console.WriteLine("Multiplier: " + Multiplier);
    }

    public Ingredient GetIngredient()
    {
        return this;
    }
}

public class Recipe
{
    public int Number;
    [Required] public string MealType { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Image { get; set; } = string.Empty;
    public List<Ingredient> Ingredients { get; set; } = new();
    public Macros TotalMacros { get; set; } = new();

    public void PrintRecipe()
    {
        Console.WriteLine("Reciping recipe...");
        Console.WriteLine("Recipe Number: " + Number);
        Console.WriteLine("MealType: " + MealType);
        Console.WriteLine("Name: " + Name);
        Console.WriteLine("Image: " + Image);

        if (Ingredients.Count != 0)
        {
            foreach (var ingredient in Ingredients)
            {
                ingredient.PrintIngredient();
            }
        }
        else
        {
            Console.WriteLine("No ingredients available.");
        }

        TotalMacros.PrintMacros();
    }

    public void SetTotalMacros()
    {
        if (Ingredients.Count == 0)
        {
            Console.WriteLine("No ingredients available.");
        }
        else
        {
            foreach (var ingredient in Ingredients)
            {
                Console.WriteLine("Printing ingredient at Macrows");
                ingredient.PrintIngredient();
                TotalMacros.TotalCalories += (ingredient.Calories * ingredient.Multiplier);
                TotalMacros.TotalFats += (ingredient.Fats * ingredient.Multiplier);
                TotalMacros.TotalCarbs += (ingredient.Carbs * ingredient.Multiplier);
                TotalMacros.TotalProtein += (ingredient.Protein * ingredient.Multiplier);
            }
        }

        Console.WriteLine("At SetTotalMacros()");
        TotalMacros.PrintMacros();
    }

    public void Clear()
    {
        Number = 0;
        MealType = String.Empty;
        Name = String.Empty;
        Image = String.Empty;
        Ingredients = new();
        TotalMacros = new();
    }

    public async Task AddRecipeToDatabase()
    {
        PrintRecipe();
        try
        {
            await using var conn = new NpgsqlConnection(
                "Host=ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech;Username=neondb_owner;Password=vVljNo8xGsb5;Database=neondb;sslmode=require;");
            await conn.OpenAsync();

            // Maybe just insert the ingredients one at a time, where you first insert the recipe variables, then macros
            // and then just array_append to the ingredients.

            string query =
                "INSERT INTO recipes (meal_type, name, image, macros) VALUES (@type, @name, @image, ROW(@calories, @fats, @carbs,@protein)::recipe_macros)";
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@type", MealType);
            cmd.Parameters.AddWithValue("@name", Name);
            cmd.Parameters.AddWithValue("@image", Image);
            cmd.Parameters.AddWithValue("@calories", TotalMacros.TotalCalories);
            cmd.Parameters.AddWithValue("@fats", TotalMacros.TotalFats);
            cmd.Parameters.AddWithValue("@carbs", TotalMacros.TotalCarbs);
            cmd.Parameters.AddWithValue("@protein", TotalMacros.TotalProtein);
            await RunAsyncQuery(cmd);
            await AddIngredientsToRow();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error making CMD: " + e.Message);
            throw;
        }
    }

    private async Task AddIngredientsToRow()
    {
        await using var conn = new NpgsqlConnection(
            "Host=ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech;Username=neondb_owner;Password=vVljNo8xGsb5;Database=neondb;sslmode=require;");
        await conn.OpenAsync();
        foreach (var ingredient in Ingredients)
        {
            string query =
                "UPDATE recipes SET ingredients = array_append(ingredients, ROW(@name, @grams, @cals, @fats, @carbs, @protein, @multiplier)::ingredient) WHERE id IN (SELECT COUNT(*) FROM recipes)";
            NpgsqlCommand cmd = new(query, conn);

            string name = ingredient.Name;
            float grams = ingredient.Grams;
            float cals = ingredient.Calories;
            float fats = ingredient.Fats;
            float carbs = ingredient.Carbs;
            float protein = ingredient.Protein;
            float multiplier = ingredient.Multiplier;

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@grams", grams);
            cmd.Parameters.AddWithValue("@cals", cals);
            cmd.Parameters.AddWithValue("@fats", fats);
            cmd.Parameters.AddWithValue("@carbs", carbs);
            cmd.Parameters.AddWithValue("@protein", protein);
            cmd.Parameters.AddWithValue("@multiplier", multiplier);

            await RunAsyncQuery(cmd);
        }
    }

    public void GetRandomRecipe()
    {
        List<Recipe> recipes = GetDinnerRecipes();
        Recipe randomRecipe = recipes[new Random().Next(0, recipes.Count-1)];
        SetRecipe(randomRecipe);
    }

    private void SetRecipe(Recipe recipe)
    {
        Number = recipe.Number;
        MealType = recipe.MealType;
        Name = recipe.Name;
        Image = recipe.Image;
        if (recipe.Ingredients.Count > 0)
        {
            Ingredients = recipe.Ingredients;
        }
        if (recipe.TotalMacros.TotalCalories != 0)
        {
            TotalMacros = recipe.TotalMacros;
        }
    }

    private List<Recipe> GetDinnerRecipes()
    {
        List<Recipe> recipes = new();
        using var conn = new NpgsqlConnection(
            "Host=ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech;Username=neondb_owner;Password=vVljNo8xGsb5;Database=neondb;sslmode=require;");
        conn.Open();
        
        const string query = "SELECT id, meal_type, name, image, (macros).total_protein, (macros).total_fats, (macros).total_carbs, (macros).total_calories FROM recipes WHERE meal_type = 'D';";
        
        using (var cmd = new NpgsqlCommand(query, conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                Recipe recipe = new Recipe
                {
                    Number = reader.GetInt32(0),
                    MealType = reader.GetString(1),
                    Name = reader.GetString(2),
                    Image = reader.GetString(3),
                    TotalMacros = new Macros
                    {
                        TotalProtein = reader.GetFloat(4),
                        TotalFats = reader.GetFloat(5),
                        TotalCarbs = reader.GetFloat(6),
                        TotalCalories = reader.GetFloat(7)
                    }
                };
                recipes.Add(recipe);
            }
        }

        return recipes;
    }

    private async Task RunAsyncQuery(NpgsqlCommand query)
    {
        int result = await query.ExecuteNonQueryAsync();
        if (result < 0)
            Console.WriteLine("No records affected.");
        else
            Console.WriteLine("Records affected: " + result);
    }
}