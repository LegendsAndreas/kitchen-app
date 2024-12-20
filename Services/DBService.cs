using System.Text.Json;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]

namespace WebKitchen.Services;

public class DBService
{
    private readonly string _connectionString;

    public DBService(string connectionString)
    {
        Console.WriteLine("Connecting DBService:" + connectionString);
        _connectionString = connectionString;
    }

    private List<Recipe> GetDinnerRecipes()
    {
        Console.WriteLine("Getting dinner recipes...");
        List<Recipe> recipes = new();
        using var conn = new NpgsqlConnection(
            "Host=ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech;Username=neondb_owner;Password=vVljNo8xGsb5;Database=neondb;sslmode=require;");
        conn.Open();

        const string query =
            "SELECT id, meal_type, name, image, (macros).total_protein, (macros).total_fats, (macros).total_carbs, (macros).total_calories FROM recipes WHERE meal_type = 'D';";

        using var cmd = new NpgsqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Recipe recipe = new Recipe
            {
                RecipeId = reader.GetInt32(0),
                MealType = reader.GetString(1),
                Name = reader.GetString(2),
                Base64Image = reader.GetString(3),
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

        return recipes;
    }

    public Recipe GetRandomRecipe()
    {
        Console.WriteLine("Getting random recipe...");
        List<Recipe> recipes = GetDinnerRecipes();
        Recipe randomRecipe = recipes[new Random().Next(0, recipes.Count - 1)];
        return randomRecipe;
    }

    public async Task<List<RecipeInstructionRecord>> GetAllRecipeInstructions()
    {
        Console.WriteLine("Getting all recipe instructions...");
        var recipeInstructionsRecords = new List<RecipeInstructionRecord>();

        var query = "SELECT id, instructions, recipe_id FROM recipe_instructions";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Extract columns
                int id = reader.GetInt32(0); // Get 'id' column
                var jsonData = reader.GetString(1); // Get 'instructions' JSON column
                int recipeId = reader.GetInt32(2); // Get 'recipe_id' column

                Console.WriteLine("JsonData: " + jsonData);

                // Deserialize JSON data into RecipeInstructions class
                var instructions = JsonSerializer.Deserialize<RecipeInstructions>(jsonData);

                // Map everything into a RecipeInstructionRecord object
                var recipeRecord = new RecipeInstructionRecord { Instructions = instructions };
                recipeRecord.SetId(id);
                recipeRecord.SetRecipeId(recipeId);

                // Output the deserialized data
                Console.WriteLine($"Record ID: {recipeRecord.GetId()}");
                Console.WriteLine($"Recipe Name: {recipeRecord.Instructions.Name}");
                Console.WriteLine($"Recipe ID: {recipeRecord.GetRecipeId()}");
                Console.WriteLine("Steps:");
                foreach (var step in recipeRecord.Instructions.Steps)
                {
                    Console.WriteLine($"  Step {step.StepNumber}: {step.StepText}");
                }

                Console.WriteLine("Notes:");
                foreach (var note in recipeRecord.Instructions.Notes)
                {
                    Console.WriteLine($"  Note {note.NoteNumber}: {note.NoteText}");
                }

                Console.WriteLine("-------------------------");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting all recipe instructions: " + ex.Message);
            Console.WriteLine("StackTrace:  " + ex.StackTrace);
            throw;
        }

        return recipeInstructionsRecords;
    }

    public async Task<List<Recipe>> GetRecipes()
    {
        Console.WriteLine("Getting recipes...");
        var img = await File.ReadAllBytesAsync("wwwroot/pics/PlaceHolderPic.jpg");
        var base64PlaceHolderPic = Convert.ToBase64String(img);
        List<Recipe> recipes = new();
        var conn = await GetConnection();
        int recipeIdTracker = 1;
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, image, meal_type, (macros).total_calories, (macros).total_fats, (macros).total_carbs, (macros).total_protein FROM recipes",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            recipes.Add(new Recipe
            {
                RecipeId = reader.GetInt32(0),
                Name = reader.GetString(1),
                Base64Image = reader.GetString(2) != "PlaceHolderPic.jpg"
                    ? reader.GetString(2)
                    : base64PlaceHolderPic,
                MealType = reader.GetString(3),
                Ingredients = await GetIngredientByIdAsync(recipeIdTracker),
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

        return recipes;
    }

    public async Task<List<Recipe>> GetRecipesByCategory(string category)
    {
        Console.WriteLine("Getting recipes by category...");
        List<Recipe> recipes = new();
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            int recipeIdTracker = 1;
            await using var cmd = new NpgsqlCommand(
                $"SELECT id, name, image, meal_type, (macros).total_calories, (macros).total_fats, (macros).total_carbs, (macros).total_protein FROM recipes ORDER BY {category}",
                conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recipes.Add(new Recipe
                {
                    RecipeId = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Base64Image = $"pics/{reader.GetString(2)}",
                    MealType = reader.GetString(3),
                    Ingredients = await GetIngredientByIdAsync(recipeIdTracker),
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
        catch (Exception e)
        {
            Console.WriteLine("Error getting recipes by categories: " + e.Message);
            throw;
        }

        return recipes;
    }

    private async Task<List<Ingredient>> GetIngredientByIdAsync(int id)
    {
        List<Ingredient> ingredients = new();
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
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
                    var tempIngredient = new Ingredient
                    {
                        Name = reader.GetString(0),
                        Grams = reader.GetFloat(1),
                        Calories = reader.GetFloat(2),
                        Carbs = reader.GetFloat(3),
                        Protein = reader.GetFloat(4),
                        Fats = reader.GetFloat(5)
                    };
                    tempIngredient.SetMultiplier();
                    ingredients.Add(tempIngredient);
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

    public async Task<List<Ingredient>> GetDatabaseIngredients()
    {
        Console.WriteLine("Getting ingredients...");
        var img = await File.ReadAllBytesAsync("wwwroot/pics/PlaceHolderPic.jpg");
        var base64PlaceHolderPic = Convert.ToBase64String(img);

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
                        Base64Image = reader.GetString(5) != "PlaceHolderPic.jpg"
                            ? reader.GetString(5)
                            : base64PlaceHolderPic
                    });
                }
            }
        }

        return ingredients;
    }

    /// Asynchronously retrieves the ID of a recipe by its name from the database.
    /// <param name="recipeName">The name of the recipe to search for.</param>
    /// <return>An integer representing the ID of the recipe if found; otherwise, returns -1.</return>
    public async Task<int> GetRecipeIdByName(string recipeName)
    {
        Console.WriteLine("Getting recipe id by name...");
        var recipeId = -1;
        var conn = await GetConnection();
        var query = "SELECT id FROM recipes WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", recipeName);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            recipeId = reader.GetInt32(0);
        }
        else
        {
            Console.WriteLine("Recipe not found.");
        }

        return recipeId;
    }

    // We then made a NpgsqlConnection, open it and then returns it.
    public async Task<NpgsqlConnection> GetConnection()
    {
        Console.WriteLine("Getting connection...");
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task AddRecipeToDatabase(Recipe recipe)
    {
        Console.WriteLine("Adding recipe to database...");
        recipe.PrintRecipe();
        try
        {
            var conn = await GetConnection();
            string query =
                "INSERT INTO recipes (meal_type, name, image, macros) VALUES (@type, @name, @image, ROW(@calories, @fats, @carbs,@protein)::recipe_macros)";
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@type", recipe.MealType);
            cmd.Parameters.AddWithValue("@name", recipe.Name);
            cmd.Parameters.AddWithValue("@image", recipe.Base64Image);
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

    public async Task AddInstructionToDb(RecipeInstructionRecord instructions)
    {
        try
        {
            var conn = await GetConnection();
            var serializedJsonData = JsonSerializer.Serialize(instructions.Instructions);
            var query = "INSERT INTO recipe_instructions (instructions, recipe_id) VALUES (@json_data, @recipe_id)";
            NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@json_data", serializedJsonData);
            cmd.Parameters.AddWithValue("@recipe_id", instructions.GetRecipeId());
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding instructions to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    private async Task AddIngredientsToRow(List<Ingredient> ingredients)
    {
        Console.WriteLine("Adding ingredients to row...");
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
            float multiplier = ingredient.GetMultiplier();
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
        Console.WriteLine("Adding ingredient to database...");
        try
        {
            var conn = await GetConnection();
            string query =
                "INSERT INTO ingredients (name, cals, fats, carbs, protein, image) VALUES(@name, @cals, @fats, @carbs, @protein, @image)";
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.Calories);
            cmd.Parameters.AddWithValue("@fats", ingredient.Fats);
            cmd.Parameters.AddWithValue("@carbs", ingredient.Carbs);
            cmd.Parameters.AddWithValue("@protein", ingredient.Protein);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            await RunAsyncQuery(cmd);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error making CMD: " + e.Message);
            throw;
        }
    }

    public async Task UpdateRecipe(string type, string name)
    {
        Console.WriteLine("Correcting recipe...");
        var conn = await GetConnection();
        const string query = "UPDATE recipes SET meal_type = @type WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@name", name);
        await RunAsyncQuery(cmd);
    }


    public async Task UpdateRecipeImage(string recipeName, string base64Image)
    {
        Console.WriteLine("Correcting recipe image...");
        var conn = await GetConnection();
        string query = $"UPDATE recipes SET image = '{base64Image}' WHERE name = '{recipeName}'";
        await using var cmd = new NpgsqlCommand(query, conn);
        await RunAsyncQuery(cmd);
    }

    public async Task UpdateRecipeName(string currentName, string updatedName)
    {
        Console.WriteLine("Correcting recipe name...");
        var conn = await GetConnection();
        const string query = "UPDATE recipes SET name = @currentName WHERE name = @updatedName";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@currentName", currentName);
        cmd.Parameters.AddWithValue("@updatedName", updatedName);
        await RunAsyncQuery(cmd);
    }


    public async Task UpdateIngredientImage(string ingredientName, string base64Image)
    {
        var conn = await GetConnection();
        string query = $"UPDATE ingredients SET image = '{base64Image}' WHERE name = '{ingredientName}'";
        await using var cmd = new NpgsqlCommand(query, conn);
        await RunAsyncQuery(cmd);
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

    /// Updates the details of an existing ingredient in the database.
    /// <param name="ingredient">The ingredient object containing updated values including name, calories, fats, carbohydrates, protein, and base64 image data.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task UpdateDatabaseIngredient(Ingredient ingredient)
    {
        try
        {
            var conn = await GetConnection();
            string query = "UPDATE ingredients " +
                           "SET " +
                           "name = @name," +
                           "cals = @cals,"+
                           "fats = @fats,"+
                           "carbs = @carbs,"+
                           "protein = @protein,"+
                           "image = @image "+
                           "WHERE id = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.Calories);
            cmd.Parameters.AddWithValue("@fats", ingredient.Fats);
            cmd.Parameters.AddWithValue("@carbs", ingredient.Carbs);
            cmd.Parameters.AddWithValue("@protein", ingredient.Protein);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating database ingredient: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    public async Task<string> DeleteIngredient(string name)
    {
        Console.WriteLine("Deleting ingredient...");
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

    public async Task<string> DeleteRecipeByName(string recipeName)
    {
        Console.WriteLine("Deleting recipe by name...");
        string statusMessage = $"Ingredient {recipeName} has been deleted.";

        await using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        string query = "DELETE FROM ingredients WHERE name = @recipe_name"; // MAYBE, add the ''.
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@recipe_name", recipeName);

        int result = await RunAsyncQuery(cmd);
        if (result < 1)
            statusMessage = $"Recipe {recipeName} was not found.";
        else
            await UpdateTableIdsAsync("ingredients");

        return statusMessage;
    }


    private async Task<int> RunAsyncQuery(NpgsqlCommand query)
    {
        int result = await query.ExecuteNonQueryAsync();
        if (result <= 0)
            Console.WriteLine("No records affected.");
        else
            Console.WriteLine("Records affected: " + result);
        return result;
    }
}