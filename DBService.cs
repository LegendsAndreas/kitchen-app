using System.Data;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]

namespace WebKitchen;

public class DBService
{
    private readonly string _connectionString;

    public DBService(string connectionString)
    {
        Console.WriteLine("Conneting DBService:" + connectionString);
        _connectionString = connectionString;
    }

    // We then made a NpgsqlConnection, open it and then returns it.
    public NpgsqlConnection GetConnection()
    {
        NpgsqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<List<Recipe>> GetRecipeAsync()
    {
        List<Recipe> recipes = new();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        int recipeIdTracker = 1;

        await using (var cmd = new NpgsqlCommand(
                         "SELECT id, name, image, meal_type FROM recipes",
                         conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                recipes.Add(new Recipe
                {
                    Number = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Image = $"pics/PlaceHolderPic.jpg",
                    Ingredients = await GetIngredientsAsync(recipeIdTracker),
                    TotalMacros =
                        await GetMacrosAsync(recipeIdTracker)
                });
                recipeIdTracker++;
            }
        }

        return recipes;
    }

    private async Task<List<Ingredient>> GetIngredientsAsync(int id)
    {
        List<Ingredient> ingredients = new();
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand(
                             "SELECT ingredient.name," +
                             "ingredient.grams," +
                             "ingredient.calories_pr_hectogram," +
                             "ingredient.carbs_pr_hectogram," +
                             "ingredient.protein_pr_hectogram," +
                             "ingredient.fats_pr_hectogram," +
                             "ingredient.multiplier " +
                             $"FROM recipes, unnest(ingredients) AS ingredient WHERE id = {id}",
                             conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    ingredients.Add(new Ingredient
                    {
                        Name = reader.GetString(0),
                        Grams = reader.GetFloat(1),
                        Calories = reader.GetFloat(2),
                        Carbs = reader.GetFloat(3),
                        Protein = reader.GetFloat(4),
                        Fats = reader.GetFloat(5),
                        Multiplier = reader.GetFloat(6)
                    });
                }
            }
        }

        return ingredients;
    }

    private async Task<Macros> GetMacrosAsync(int id)
    {
        Macros macros = new();

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand(
                             "SELECT" +
                             "(macros).total_calories," +
                             "(macros).total_carbs," +
                             "(macros).total_fats," +
                             "(macros).total_protein " +
                             "FROM recipes " +
                             $"WHERE id ={id};",
                             conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    macros.TotalCalories = reader.GetFloat(0);
                    macros.TotalCarbs = reader.GetFloat(1);
                    macros.TotalFats = reader.GetFloat(2);
                    macros.TotalProtein = reader.GetFloat(3);
                }
            }
        }


        return macros;
    }
}