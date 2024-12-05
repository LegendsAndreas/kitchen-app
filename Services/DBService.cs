using System.Collections;
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
    public async Task<NpgsqlConnection> GetConnection()
    {
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
    
    public async Task AddRecipeToDatabase(Recipe recipe)
    {
        recipe.PrintRecipe();
        try
        {
            var conn = await GetConnection();

            // Maybe just insert the ingredients one at a time, where you first insert the recipe variables, then macros
            // and then just array_append to the ingredients.

            string query =
                "INSERT INTO recipes (meal_type, name, image, macros) VALUES (@type, @name, @image, ROW(@calories, @fats, @carbs,@protein)::recipe_macros)";
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@type", recipe.MealType);
            cmd.Parameters.AddWithValue("@name", recipe.Name);
            cmd.Parameters.AddWithValue("@image", recipe.Image);
            cmd.Parameters.AddWithValue("@calories", recipe.TotalMacros.Calories);
            cmd.Parameters.AddWithValue("@fats", recipe.TotalMacros.Fat);
            cmd.Parameters.AddWithValue("@carbs", recipe.TotalMacros.Carbs);
            cmd.Parameters.AddWithValue("@protein", recipe.TotalMacros.Protein);
            await RunAsyncQuery(cmd);
            await AddIngredientsToRow(recipe.Ingredients);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error making CMD: " + e.Message);
            throw;
        }
    }
    
    private async Task AddIngredientsToRow(List<Ingredient> ingredients)
    {
        var conn = await GetConnection();
        foreach (var ingredient in ingredients)
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
    
    public async Task AddIngredientToDb(Ingredient ingredient)
    {
        try
        {
            var conn = await GetConnection();

            // Maybe just insert the ingredients one at a time, where you first insert the recipe variables, then macros
            // and then just array_append to the ingredients.

            string query =
                "INSERT INTO ingredients (name, cals, fats, carbs, protein, image) VALUES(@name, @cals, @fats, @carbs, @protein, @image)";
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.Calories);
            cmd.Parameters.AddWithValue("@fats", ingredient.Fats);
            cmd.Parameters.AddWithValue("@carbs", ingredient.Carbs);
            cmd.Parameters.AddWithValue("@protein", ingredient.Protein);
            cmd.Parameters.AddWithValue("@image", ingredient.Image);
            await RunAsyncQuery(cmd);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error making CMD: " + e.Message);
            throw;
        }
    }
    
    public async Task CorrectRecipeAsync(string type, string name)
    {
        var conn = await GetConnection();

        const string query = "UPDATE recipes SET meal_type = @type WHERE name = @name";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@name", name);

        await RunAsyncQuery(cmd);
    }
    
    private List<Recipe> GetDinnerRecipes()
    {
        List<Recipe> recipes = new();
        using var conn = new NpgsqlConnection(
            "Host=ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech;Username=neondb_owner;Password=vVljNo8xGsb5;Database=neondb;sslmode=require;");
        conn.Open();

        const string query =
            "SELECT id, meal_type, name, image, (macros).total_protein, (macros).total_fats, (macros).total_carbs, (macros).total_calories FROM recipes WHERE meal_type = 'D';";

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
                        Protein = reader.GetFloat(4),
                        Fat = reader.GetFloat(5),
                        Carbs = reader.GetFloat(6),
                        Calories = reader.GetFloat(7)
                    }
                };
                recipes.Add(recipe);
            }
        }

        return recipes;
    }
    
    public Recipe GetRandomRecipe()
    {
        List<Recipe> recipes = GetDinnerRecipes();
        Recipe randomRecipe = recipes[new Random().Next(0, recipes.Count - 1)];
        return randomRecipe;
    }
    
    public async Task CorrectRecipeImageAsync(string recipeName, string imageName)
    {
        var conn = await GetConnection();

        string query = $"UPDATE recipes SET image = '{imageName}' WHERE name = '{recipeName}'";
        await using var cmd = new NpgsqlCommand(query, conn);

        await RunAsyncQuery(cmd);
    }
    
    public async Task CorrectRecipeNameAsync(string currentName, string updatedName)
    {
        var conn = await GetConnection();

        const string query = "UPDATE recipes SET name = @currentName WHERE name = @updatedName";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@currentName", currentName);
        cmd.Parameters.AddWithValue("@updatedName", updatedName);

        await RunAsyncQuery(cmd);
    }

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        var img = File.ReadAllBytes("wwwroot/pics/PlaceHolderPic.jpg");
        var placeHolder = Convert.ToBase64String(img);
        
        List<Recipe> recipes = new();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        Hashtable base64Images = await GetRecipeImagesAsync();

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
                    Image = base64Images.ContainsKey(reader.GetString(1)) ? Convert.ToString(base64Images[reader.GetString(1)]) : placeHolder,
                    MealType = reader.GetString(3),
                    Ingredients = await GetIngredientsAsync(recipeIdTracker),
                    TotalMacros = new Macros
                    {
                        Calories = reader.GetFloat(4),
                        Fat = reader.GetFloat(5),
                        Carbs = reader.GetFloat(6),
                        Protein = reader.GetFloat(7)
                    }
                });
                recipeIdTracker++;
            }
        }

        return recipes;
    }

    private async Task<Hashtable> GetRecipeImagesAsync()
    {
        Hashtable recipeImages = new();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        string query = "SELECT image_name, image_base64 FROM images;";
        await using (var cmd = new NpgsqlCommand(query, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                recipeImages.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        return recipeImages;
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
                            Calories = reader.GetFloat(4),
                            Fat = reader.GetFloat(5),
                            Carbs = reader.GetFloat(6),
                            Protein = reader.GetFloat(7)
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

    public async Task UploadImageBase64Async(string imageName, string base64Image)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            string query = $"INSERT INTO images (image_name, image_base64) VALUES ('{imageName}', '{base64Image}');";
            await using var cmd = new NpgsqlCommand(query, conn);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error uploading image: " + ex.Message);
        }
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