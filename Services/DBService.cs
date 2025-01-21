using System.Text.Json;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]
// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&user=[USERNAME]
// Remember that a connection string largely opperates on regex logic, so it does not matter too much where you put your variables.
// So, if you don't need sslmode, you can just delete it entirely.
// jdbc:postgresql://ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech/neondb?sslmode=require&user=neondb_owner&password=vVljNo8xGsb5

namespace WebKitchen.Services;

public class DBService
{
    private readonly string _connectionString;

    public DBService(string connectionString)
    {
        Console.WriteLine("Connecting DBService:" + connectionString);
        _connectionString = connectionString;
    }

    /// Asynchronously creates and returns an open database connection using the configured connection string.
    /// <returns>An instance of NpgsqlConnection that is open and ready for database operations.</returns>
    public async Task<NpgsqlConnection> GetConnection()
    {
        Console.WriteLine("Getting connection...");
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<string> GetPlaceHolderPic()
    {
        Console.WriteLine("Getting place holder pic...");
        var img = await File.ReadAllBytesAsync("wwwroot/pics/PlaceHolderPic.jpg");
        var base64PlaceHolderPic = Convert.ToBase64String(img);
        return base64PlaceHolderPic;
    }

    private async Task<List<Recipe>> GetDinnerRecipes()
    {
        Console.WriteLine("Getting dinner recipes...");

        var base64PlaceHolderPic = await GetPlaceHolderPic();
        List<Recipe> recipes = [];
        await using var conn = await GetConnection();

        const string query =
            "SELECT id, meal_type, name, image, (macros).total_protein, (macros).total_fats, (macros).total_carbs, (macros).total_calories FROM recipes WHERE meal_type = 'D';";

        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var recipe = new Recipe
            {
                RecipeId = reader.GetInt32(0),
                MealType = Convert.ToChar(reader.GetString(1)),
                Name = reader.GetString(2),
                Base64Image = reader.GetString(3) != "PlaceHolderPic.jpg"
                    ? reader.GetString(3)
                    : base64PlaceHolderPic,
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

    public async Task<Recipe?> GetRecipeById(int recipeId)
    {
        Console.WriteLine("Getting recipe by id...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1.");
            return null;
        }

        Recipe recipe;

        const string query = "SELECT id, name, image, meal_type," +
                             "(macros).total_calories," +
                             "(macros).total_fats," +
                             "(macros).total_carbs," +
                             "(macros).total_protein " +
                             "FROM recipes " +
                             "WHERE id = @id";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", recipeId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                recipe = await BuildRecipe(reader);
            }
            else
            {
                Console.WriteLine("Recipe not found.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting recipe by id: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return recipe;
    }

    private async Task<Recipe> BuildRecipe(NpgsqlDataReader reader)
    {
        Console.WriteLine("Building recipe...");
        var base64PlaceHolderPic = await GetPlaceHolderPic();

        var recipe = new Recipe
        {
            RecipeId = reader.GetInt32(0),
            Name = reader.GetString(1),
            Base64Image = reader.GetString(2) != "PlaceHolderPic.jpg"
                ? reader.GetString(2)
                : base64PlaceHolderPic,
            MealType = Convert.ToChar(reader.GetString(3)),
            TotalMacros = new Macros
            {
                Calories = reader.GetFloat(4),
                Fat = reader.GetFloat(5),
                Carbs = reader.GetFloat(6),
                Protein = reader.GetFloat(7)
            }
        };

        try
        {
            recipe.Ingredients = await GetIngredientByRecipeIdAsync(recipe.RecipeId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error building recipe: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return recipe;
    }

    public async Task<RecipeInstructionRecord?> GetRecipeInstructionByRecipeId(int RecipeId)
    {
        Console.WriteLine("Getting recipe instructions by id...");
        RecipeInstructionRecord instructionsRecord;
        const string query = "SELECT id, instructions, recipe_id FROM recipe_instructions WHERE recipe_id = @recipeId;";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@recipeId", RecipeId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var jsonData = reader.GetString(1);
                var recipeId = reader.GetInt32(2);

                var instructions = JsonSerializer.Deserialize<RecipeInstructions>(jsonData);

                if (instructions == null)
                {
                    Console.WriteLine("Error deserializing instructions.");
                    return null;
                }

                var tempInstructionsRecord = new RecipeInstructionRecord { Instructions = instructions };
                tempInstructionsRecord.SetId(id);
                tempInstructionsRecord.SetRecipeId(recipeId);

                instructionsRecord = tempInstructionsRecord;
            }
            else
            {
                Console.WriteLine("Recipe instructions not found.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting recipe instructions by id: " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            throw;
        }

        return instructionsRecord;
    }

    public async Task<Recipe> GetRandomRecipe()
    {
        Console.WriteLine("Getting random recipe...");
        var recipes = await GetDinnerRecipes();
        var randomRecipe = recipes[new Random().Next(0, recipes.Count - 1)];
        return randomRecipe;
    }

    public async Task<List<RecipeInstructionRecord>> GetAllRecipeInstructions()
    {
        Console.WriteLine("Getting all recipe instructions...");
        var recipeInstructionsRecords = new List<RecipeInstructionRecord>();

        const string query = "SELECT id, instructions, recipe_id FROM recipe_instructions";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Extract columns
                var id = reader.GetInt32(0); // Get 'id' column
                var jsonData = reader.GetString(1); // Get 'instructions' JSON column
                var recipeId = reader.GetInt32(2); // Get 'recipe_id' column

                Console.WriteLine("JsonData: " + jsonData);

                // Deserialize JSON data into RecipeInstructions class
                var instructions = JsonSerializer.Deserialize<RecipeInstructions>(jsonData);

                // Map everything into a RecipeInstructionRecord object
                var recipeRecord = new RecipeInstructionRecord { Instructions = instructions };
                recipeRecord.SetId(id);
                recipeRecord.SetRecipeId(recipeId);

                recipeRecord.PrintRecipeInstructionsRecord();
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

    public async Task<List<Recipe>> GetAllRecipes()
    {
        Console.WriteLine("Getting recipes...");

        // var base64PlaceHolderPic = await GetPlaceHolderPic();
        List<Recipe> recipes = new();
        await using var conn = await GetConnection();

        const string query = "SELECT id, name, image, meal_type," +
                             "(macros).total_calories," +
                             "(macros).total_fats," +
                             "(macros).total_carbs," +
                             "(macros).total_protein " +
                             "FROM recipes " +
                             "ORDER BY id";
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tempRecipe = await BuildRecipe(reader);
            recipes.Add(tempRecipe);
        }

        return recipes;
    }

    public async Task<List<Recipe>> GetRecipesByCategory(string category)
    {
        Console.WriteLine("Getting recipes by category...");
        List<Recipe> recipes = [];
        try
        {
            await using var conn = await GetConnection();
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
                    MealType = Convert.ToChar(reader.GetString(3)),
                    Ingredients = await GetIngredientByRecipeIdAsync(recipeIdTracker),
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

    private async Task<List<Ingredient>> GetIngredientByRecipeIdAsync(int id)
    {
        Console.WriteLine("Getting ingredients by id...");
        List<Ingredient> ingredients = new();
        try
        {
            await using var conn = await GetConnection();
            const string query = "SELECT ingredient.name," +
                                 "ingredient.grams," +
                                 "ingredient.calories_pr_hectogram," +
                                 "ingredient.carbs_pr_hectogram," +
                                 "ingredient.protein_pr_hectogram," +
                                 "ingredient.fats_pr_hectogram," +
                                 "ingredient.multiplier " +
                                 "FROM recipes, unnest(ingredients) AS ingredient WHERE id = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var tempIngredient = new Ingredient
                {
                    Name = reader.GetString(0),
                    Grams = reader.GetFloat(1),
                    CaloriesPer100g = reader.GetFloat(2),
                    CarbsPer100g = reader.GetFloat(3),
                    ProteinPer100g = reader.GetFloat(4),
                    FatsPer100g = reader.GetFloat(5)
                };
                tempIngredient.SetMultiplier();
                ingredients.Add(tempIngredient);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting ingredients async: " + ex.Message);
            throw;
        }

        return ingredients;
    }

    /// Asynchronously retrieves all ingredients from the database.
    /// Each ingredient includes its name, nutritional values, and associated image.
    /// If the image is a placeholder, it returns the base64-encoded placeholder image.
    /// <returns>A list of Ingredient objects retrieved from the database.</returns>
    public async Task<List<Ingredient>> GetAllDbIngredients()
    {
        Console.WriteLine("Getting all database ingredients...");

        var base64PlaceHolderPic = await GetPlaceHolderPic();

        List<Ingredient> ingredients = [];
        await using var conn = await GetConnection();

        const string query = "SELECT id, name, cals, fats, carbs, protein, image FROM ingredients ORDER BY id";
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Ingredient tempIngredient = new()
            {
                Name = reader.GetString(1),
                CaloriesPer100g = reader.GetFloat(2),
                FatsPer100g = reader.GetFloat(3),
                CarbsPer100g = reader.GetFloat(4),
                ProteinPer100g = reader.GetFloat(5),
                Base64Image = reader.GetString(6) != "PlaceHolderPic.jpg"
                    ? reader.GetString(6)
                    : base64PlaceHolderPic
            };
            var uintValue = unchecked((uint)reader.GetInt32(0));
            tempIngredient.SetId(uintValue);
            ingredients.Add(tempIngredient);
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
        await using var conn = await GetConnection();
        const string query = "SELECT id FROM recipes WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", recipeName);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            recipeId = reader.GetInt32(0);
        else
            Console.WriteLine("Recipe not found.");

        return recipeId;
    }

    public async Task<Ingredient> GetIngredientByName(string ingredientName)
    {
        Console.WriteLine("Getting ingredient by name...");

        await using var conn = await GetConnection();

        const string query = "SELECT * FROM ingredients WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", ingredientName);
        var tempIngredient = new Ingredient();

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var uintValue = unchecked((uint)reader.GetInt32(0));
                tempIngredient.Name = reader.GetString(1);
                tempIngredient.CaloriesPer100g = reader.GetFloat(2);
                tempIngredient.FatsPer100g = reader.GetFloat(3);
                tempIngredient.CarbsPer100g = reader.GetFloat(4);
                tempIngredient.ProteinPer100g = reader.GetFloat(5);
                tempIngredient.Base64Image = reader.GetString(6);
                tempIngredient.SetId(uintValue);
            }
            else
            {
                Console.WriteLine("Ingredient not found.");
                return tempIngredient;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting ingredient by name: " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            throw;
        }

        return tempIngredient;
    }

    /// Asynchronously adds a recipe to the database by inserting its details, including meal type, name, image, macros, and ingredients.
    /// <param name="recipe">The recipe object containing all the required details to be added to the database.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task AddRecipeToDb(Recipe recipe)
    {
        Console.WriteLine("Adding recipe to database...");
        recipe.PrintRecipe();
        try
        {
            await using var conn = await GetConnection();
            const string query = "INSERT INTO recipes " +
                                 "(meal_type, name, image, macros) " +
                                 "VALUES (" +
                                 "@type," +
                                 "@name," +
                                 "@image," +
                                 "ROW(" +
                                 "@calories," +
                                 "@fats," +
                                 "@carbs," +
                                 "@protein)::recipe_macros)";
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
        catch (Exception ex)
        {
            Console.WriteLine("Error adding recipe to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Asynchronously adds a recipe's instructions to the database.
    /// <param name="instructions">An object containing the instructions and the associated recipe ID.</param>
    /// <return>A Task representing the asynchronous operation.</return>
    public async Task<string> AddInstructionToDb(RecipeInstructionRecord instructions)
    {
        Console.WriteLine("Adding instructions to database...");

        var statusMessage = "Instructions successfully added.";
        var serializedJsonData = JsonSerializer.Serialize(instructions.Instructions);
        const string query = "INSERT INTO recipe_instructions " +
                             "(instructions, recipe_id) " +
                             "VALUES (" +
                             "@json_data," +
                             "@recipe_id)";
        try
        {
            await using var conn = await GetConnection();
            NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@json_data", NpgsqlTypes.NpgsqlDbType.Json, serializedJsonData);
            cmd.Parameters.AddWithValue("@recipe_id", instructions.GetRecipeId());
            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                statusMessage = "Instructions did not get added to database; recipe ID was not found in database.";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding instructions to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    /// Adds a list of ingredients to the corresponding row within the recipes database table.
    /// <param name="ingredients">The list of ingredients to add, each represented by an Ingredient object.</param>
    /// <return>A Task that represents the asynchronous operation.</return>
    private async Task AddIngredientsToRow(List<Ingredient> ingredients)
    {
        Console.WriteLine("Adding ingredients to row...");

        try
        {
            await using var conn = await GetConnection();
            foreach (var ingredient in ingredients)
            {
                const string query = "UPDATE recipes " +
                                     "SET ingredients =" +
                                     "array_append(ingredients," +
                                     "ROW(" +
                                     "@name," +
                                     "@grams," +
                                     "@cals," +
                                     "@fats," +
                                     "@carbs," +
                                     "@protein," +
                                     "@multiplier" +
                                     ")::ingredient) " +
                                     "WHERE id IN (SELECT COUNT(*) FROM recipes)"; // Since every new recipe is actually the last one in the table, we can just add all the occurrences of id into one and that will be our recipe id.
                await using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@name", ingredient.Name);
                cmd.Parameters.AddWithValue("@grams", ingredient.Grams);
                cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
                cmd.Parameters.AddWithValue("@fats", ingredient.FatsPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                await RunAsyncQuery(cmd);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Asynchronously adds an ingredient to the database.
    /// <param name="ingredient">The ingredient object containing details such as name, calories, fats, carbs, protein, and image to be added to the database.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task AddIngredientToDb(Ingredient ingredient)
    {
        Console.WriteLine("Adding ingredient to database...");

        try
        {
            await using var conn = await GetConnection();
            const string query =
                "INSERT INTO ingredients " +
                "(name, cals, fats, carbs, protein, image) " +
                "VALUES(@name, @cals, @fats, @carbs, @protein, @image)";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatsPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }


    /// Updates the meal type of a recipe identified by its ID in the database.
    /// <param name="mealType">The new meal type to be assigned to the recipe.</param>
    /// <param name="recipeId">The unique identifier of the recipe to be updated.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<string> UpdateRecipeMealTypeRecipeId(char mealType, int recipeId)
    {
        Console.WriteLine("Updating recipe meal type by name...");

        var statusMessage = "Meal type got updated.";

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Meal type did not get updated; recipe ID is less than 1";
        }

        const string query = "UPDATE recipes SET meal_type = @type WHERE id = @recipe_id";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@type", mealType);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);

            var result = await RunAsyncQuery(cmd);

            if (result == 0)
            {
                Console.WriteLine("Recipe ID is not found in database.");
                statusMessage = "Meal type did not get updated; recipe ID was not found in database.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating recipe meal type by recipe id: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    public async Task<string> UpdateRecipeIngredientsById(List<Ingredient> ingredients, int recipeId)
    {
        Console.WriteLine("Updating recipe ingredients by id...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Recipe ingredients did not get updated; recipe ID is less than 1";
        }

        var statusMessage = "Recipe ingredients got updated.";
        try
        {
            // We can reuse AddIngredientsToRow, because the functions actually already updates an existing element in the database.
            await UpdateIngredients(ingredients, recipeId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("error updating recipe ingredients by id: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    private async Task EmptyRecipeIngredientsByRecipeId(int recipeId)
    {
        Console.WriteLine("emptying recipe ingredients by recipe id...");

        try
        {
            await using var conn = await GetConnection();
            const string query = "UPDATE recipes " +
                                 "SET ingredients = array[]::ingredient[] " +
                                 "WHERE id = @recipe_id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    private async Task UpdateIngredients(List<Ingredient> ingredients, int recipeId)
    {
        Console.WriteLine("Updating ingredients...");

        await EmptyRecipeIngredientsByRecipeId(recipeId);

        try
        {
            await using var conn = await GetConnection();
            foreach (var ingredient in ingredients)
            {
                const string query = "UPDATE recipes " +
                                     "SET ingredients =" +
                                     "array_append(ingredients," +
                                     "ROW(" +
                                     "@name," +
                                     "@grams," +
                                     "@cals," +
                                     "@fats," +
                                     "@carbs," +
                                     "@protein," +
                                     "@multiplier" +
                                     ")::ingredient) " +
                                     "WHERE id = @recipe_id";
                await using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@name", ingredient.Name);
                cmd.Parameters.AddWithValue("@grams", ingredient.Grams);
                cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
                cmd.Parameters.AddWithValue("@fats", ingredient.FatsPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                cmd.Parameters.AddWithValue("@recipe_id", recipeId);
                await RunAsyncQuery(cmd);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Updates the image of a recipe based on its name in the database.
    /// <param name="recipeName">The name of the recipe to update.</param>
    /// <param name="base64Image">The new image represented as a Base64 string.</param>
    /// <return>A task that represents the asynchronous update operation.</return>
    public async Task<string> UpdateRecipeImageByRecipeId(string base64Image, int recipeId)
    {
        Console.WriteLine("Updating recipe image by id...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe id is below 1.");
            return "Recipe image did not get updated; recipe ID is less than 1.";
        }

        var statusMessage = "Recipe image got updated.";
        const string query = "UPDATE recipes SET image = @base64_image WHERE id = @recipe_id";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@base64_image", base64Image);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);
            var result = await RunAsyncQuery(cmd);

            if (result == 0)
            {
                Console.WriteLine("Recipe ID is not found in database.");
                statusMessage = "Recipe image did not get updated; recipe ID was not found in database.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating recipe image by recipe id: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    /// Updates the name of a recipe in the database based on its current name.
    /// <param name="currentName">The current name of the recipe to be updated.</param>
    /// <param name="updatedName">The new name to update the recipe to.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task<string> UpdateRecipeNameByRecipeId(string updatedName, int recipeId)
    {
        Console.WriteLine("Updating recipe name by name...");

        var statusMessage = "Recipe name got updated.";

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Recipe name did not get updated; recipe ID is less than 1";
        }

        const string query = "UPDATE recipes SET name = @updatedName WHERE id = @recipe_id";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@updatedName", updatedName);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);

            var result = await RunAsyncQuery(cmd);

            if (result == 0)
            {
                Console.WriteLine("Recipe ID is not found in database.");
                statusMessage = "Recipe name did not get updated; recipe ID was not found in database.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating recipe name by recipe id: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    /// Updates the image of an ingredient in the database by its name.
    /// <param name="ingredientName">The name of the ingredient to update.</param>
    /// <param name="base64Image">The Base64-encoded string representing the new image for the ingredient.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task UpdateDbIngredientImageByName(string ingredientName, string base64Image)
    {
        Console.WriteLine("Updating database ingredient image by name...");

        const string query = "UPDATE ingredients SET image = @base64_image WHERE name = @ingredient_name";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@base64_image", base64Image);
            cmd.Parameters.AddWithValue("@ingredient_name", ingredientName);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating database ingredient image by name: " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Asynchronously updates the ID sequence values for a specified database table to ensure sequential numbering.
    /// <param name="tableName">The name of the database table whose ID sequence will be updated.</param>
    /// <return>An asynchronous task representing the operation. Does not return a value.</return>
    /// <remarks>You HAVE to use this when you delete an element in a table, to make sure the the IDs stay correct.</remarks>
    private async Task UpdateTableIds(string tableName)
    {
        Console.WriteLine("Updating table IDs...");

        var query = $"CREATE TEMP TABLE temp_{tableName} AS " +
                    "SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id " +
                    $"FROM {tableName};" +
                    $"UPDATE {tableName} " +
                    $"SET id = temp_{tableName}.new_id " +
                    $"FROM temp_{tableName} " +
                    $"WHERE {tableName}.id = temp_{tableName}.id;" +
                    $"SELECT setval('{tableName}_id_seq', (SELECT MAX(id) FROM {tableName}));" +
                    $"DROP TABLE temp_{tableName};";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating table ids: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }


    /// Updates the details of an existing ingredient in the database.
    /// <param name="ingredient">The ingredient object containing updated values including name, calories, fats, carbohydrates, protein, and base64 image data.</param>
    /// <return>Task.</return>
    /// <remarks>Remember that this technically updates EVERY value in a database Ingredient. So don't just send an Ingredient
    /// with one active value, while the rest are just null.</remarks>
    public async Task UpdateDbIngredient(Ingredient ingredient)
    {
        Console.WriteLine("Updating database ingredient...");

        try
        {
            await using var conn = await GetConnection();
            const string query = "UPDATE ingredients " +
                                 "SET " +
                                 "name = @name," +
                                 "cals = @cals," +
                                 "fats = @fats," +
                                 "carbs = @carbs," +
                                 "protein = @protein," +
                                 "image = @image " +
                                 "WHERE id = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatsPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            cmd.Parameters.AddWithValue("@id", ingredient.GetIntId());

            if (isIngredientIdZero(ingredient))
            {
                Console.WriteLine("Ingredient ID is zero. Not updating.");
                return;
            }

            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating database ingredient: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Asynchronously removes an ingredient with the specified name from the database.
    /// <param name="name">The name of the ingredient to delete.</param>
    /// <return>A string message indicating whether the deletion was successful or if the ingredient was not found.</return>
    public async Task<string> DeleteDbIngredientByName(string name)
    {
        Console.WriteLine("Deleting database ingredient by name...");
        var statusMessage = $"Ingredient {name} has been deleted.";

        await using var conn = await GetConnection();
        const string query = "DELETE FROM ingredients WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", name);

        int result = await RunAsyncQuery(cmd);
        if (result < 1)
            statusMessage = $"Ingredient {name} was not found.";
        else
            await UpdateTableIds("ingredients");

        return statusMessage;
    }

    /// Asynchronously deletes a recipe by its name from the database.
    /// <param name="recipeName">The name of the recipe to delete.</param>
    /// <return>A string indicating the status of the deletion operation, such as success or not found.</return>
    public async Task<string> DeleteRecipeById(int recipeId)
    {
        Console.WriteLine("Deleting recipe by name...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1.");
            return "Recipe was not deleted; recipe ID is less than 1.";
        }

        var statusMessage = $"Ingredient {recipeId} has been deleted.";

        await using var conn = await GetConnection();

        const string query = "DELETE FROM recipes WHERE id = @recipe_id";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@recipe_id", recipeId);

        var result = await RunAsyncQuery(cmd);
        if (result < 1)
            statusMessage = $"Recipe {recipeId} was not found.";
        else
            await UpdateTableIds("recipes");

        return statusMessage;
    }

    public async Task<(User? User, string Message)> GetUserByUsernameAndPassword(string username, string password)
    {
        Console.WriteLine("Getting user by username and password...");
        var user = new User();
        var statusMessage = "User found; no errors";
        List<int> recipeIds = [];

        const string query = "SELECT id, username, email, password, recipe_ids " +
                             "FROM users " +
                             "WHERE username = @username " +
                             "AND password = @password";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@password", password);

            await using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                user.SetId((uint)reader.GetInt32(0));
                user.Username = reader.GetString(1);
                user.SetEmail(reader.GetString(2));
                user.SetPassword(reader.GetString(3));
                var recipeIdArray = reader.GetFieldValue<int[]>(4);
                recipeIds = recipeIdArray?.ToList() ?? [];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user by username ({username}) and password ({password}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, statusMessage = "Error getting user by username and password");
        }

        if (recipeIds.Count != 0)
        {
            foreach (var recipeId in recipeIds)
            {
                user.Recipes.Add(await GetRecipeById(recipeId));
            }
        }
        
        Console.WriteLine(user.GetUserId());

        foreach (var recipe in user.Recipes)
        {
            recipe.PrintRecipe();
        }

        return (user, statusMessage);
    }

    private bool isIngredientIdZero(Ingredient ingredient)
    {
        Console.WriteLine("Checking if ingredient ID is 0...");
        if (ingredient.GetId() != 0) return false;
        Console.WriteLine("Ingredient ID is 0");
        return true;
    }


    /// Executes the provided SQL query asynchronously and processes the number of affected rows.
    /// <param name="query">An instance of NpgsqlCommand representing the query to be executed.</param>
    /// <return>An integer representing the number of rows affected by the query.</return>
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