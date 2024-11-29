using System.Data;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]

namespace WebKitchen.Services;

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

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        List<Recipe> recipes = new();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        int recipeIdTracker = 1;

        await using (var cmd = new NpgsqlCommand(
                         "SELECT id, name, image, meal_type, (macros).total_calories, (macros).total_fats, (macros).total_carbs, (macros).total_protein FROM recipes",
                         conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                recipes.Add(new Recipe
                {
                    Number = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Image = $"pics/{reader.GetString(2)}",
                    MealType = reader.GetString(3),
                    Ingredients = await GetIngredientsAsync(recipeIdTracker),
                    TotalMacros = new Macros
                    {
                        TotalCalories = reader.GetFloat(4),
                        TotalFats = reader.GetFloat(5),
                        TotalCarbs = reader.GetFloat(6),
                        TotalProtein = reader.GetFloat(7)
                    }
                });
                recipeIdTracker++;
            }
        }

        return recipes;
    }

    public async Task<List<Recipe>> GetRecipesByCategoryAsync(string category)
    {
        List<Recipe> recipes = new();
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            int recipeIdTracker = 1;

            await using (var cmd = new NpgsqlCommand(
                             $"SELECT id, name, image, meal_type, (macros).total_calories, (macros).total_fats, (macros).total_carbs, (macros).total_protein FROM recipes ORDER BY {category}",
                             conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    recipes.Add(new Recipe
                    {
                        Number = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Image = $"pics/{reader.GetString(2)}",
                        MealType = reader.GetString(3),
                        Ingredients = await GetIngredientsAsync(recipeIdTracker),
                        TotalMacros = new Macros
                        {
                            TotalCalories = reader.GetFloat(4),
                            TotalFats = reader.GetFloat(5),
                            TotalCarbs = reader.GetFloat(6),
                            TotalProtein = reader.GetFloat(7)
                        }
                    });
                    recipeIdTracker++;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error getting recipes by categories: " + e.Message);
            throw;
        }


        return recipes;
    }


    private async Task<List<Ingredient>> GetIngredientsAsync(int id)
    {
        List<Ingredient> ingredients = new();
        try
        {
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
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting ingredients async: " + ex.Message);
            throw;
        }
        
        return ingredients;
    }

    public async Task<List<Ingredient>> GetIngredientsFromTableAsync()
    {
        Console.WriteLine("Getting ingredients...");
        List<Ingredient> ingredients = new();

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand(
                             "SELECT name, cals, fats, carbs, protein, image FROM ingredients;", conn))

            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    ingredients.Add(new Ingredient
                    {
                        Name = reader.GetString(0),
                        Calories = reader.GetFloat(1),
                        Fats = reader.GetFloat(2),
                        Carbs = reader.GetFloat(3),
                        Protein = reader.GetFloat(4),
                        Image = reader.GetString(5)
                    });
                }
            }
        }

        return ingredients;
    }

    public async Task<string> DeleteIngredientAsync(string name)
    {
        string statusMessage = $"Ingredient {name} has been deleted.";

        await using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        string query = $"DELETE FROM ingredients WHERE name = '{name}';";
        await using var cmd = new NpgsqlCommand(query, conn);

        int result = await RunAsyncQuery(cmd);
        if (result < 1)
            statusMessage = $"Ingredient {name} was not found.";
        else
            await UpdateTableIdsAsync("ingredients");
        
        return statusMessage;
    }

    private async Task UpdateTableIdsAsync(string tableName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        
        string query = $"CREATE TEMP TABLE temp_{tableName} AS" +
                       "SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id" +
                       $"FROM {tableName};" +
                       $"UPDATE {tableName}" +
                       $"SET id = temp_{tableName}.new_id" +
                       $"FROM temp_{tableName}" +
                       $"WHERE {tableName}.id = temp_{tableName}.id;" +
                       $"SELECT setval('{tableName}_id_seq', (SELECT MAX(id) FROM {tableName}));" +
                       $"DROP TABLE temp_{tableName};";
        await using var cmd = new NpgsqlCommand(query, conn);
        
        await RunAsyncQuery(cmd);
    }
    
    public async Task<int> RunAsyncQuery(NpgsqlCommand query)
    {
        int result = await query.ExecuteNonQueryAsync();
        if (result <= 0)
            Console.WriteLine("No records affected.");
        else
            Console.WriteLine("Records affected: " + result);

        return result;
    }
}