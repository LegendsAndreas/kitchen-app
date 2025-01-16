using System.Text.Json;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]
// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&user=[USERNAME]
// Remember that a connection string largely opperates on regex logic, so it does not matter too much where you put your variables.
// So, if you don't need sslmode, you can just delete it entirely.
// jdbc:postgresql://ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech/neondb?sslmode=require&user=neondb_owner&password=vVljNo8xGsb5

namespace WebKitchen.Services;

public enum TableName
{
    recipes,
    ingredients,
    recipe_instructions
}

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
    private async Task<NpgsqlConnection> GetConnection()
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

    /// Retrieves a list of dinner recipes from the database, including their details and nutritional information.
    /// If a recipe does not have a specific image, a placeholder image is substituted.
    /// <returns>A tuple containing a list of Recipe objects and a status message indicating the result of the operation.</returns>
    private async Task<(List<Recipe> Recipes, string Message)> GetDinnerRecipes()
    {
        Console.WriteLine("Getting dinner recipes...");

        var base64PlaceHolderPic = await GetPlaceHolderPic();
        List<Recipe> recipes = [];
        string statusMessage = "Recipes successfully retrieved.";
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
                MealType = reader.GetString(1),
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

        return (recipes, statusMessage);
    }

    /// Asynchronously retrieves a recipe by its unique identifier from the database.
    /// <param name="recipeId">The unique identifier of the recipe to retrieve. Must be greater than 0.</param>
    /// <returns>A tuple containing the retrieved Recipe object (or null if not found) and a status message indicating the result.</returns>
    public async Task<(Recipe? Recipe, string Message)> GetRecipeById(int recipeId)
    {
        Console.WriteLine("Getting recipe by id...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1.");
            return (null, "Recipe ID is less than 1.");
        }

        Recipe recipe;
        string statusMessage = "Recipe successfully retrieved.";

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
                return (null, "Recipe not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting recipe by id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting recipe by id ({recipeId}): {ex.Message}.");
        }

        return (recipe, statusMessage);
    }

    /// Constructs and returns a Recipe object populated with data from the given database reader,
    /// including associated ingredients, macros, and optionally replaces a placeholder image.
    /// <param name="reader">An NpgsqlDataReader containing the recipe data fetched from the database.</param>
    /// <returns>A fully constructed Recipe object with associated data.</returns>
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
            MealType = reader.GetString(3),
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

    /// Retrieves a record of recipe instructions associated with the specified recipe ID from the database.
    /// <param name="RecipeId">The unique identifier of the recipe for which instructions are to be retrieved.</param>
    /// <returns>A RecipeInstructionRecord containing the instructions if found, or null if not found.</returns>
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

    /// Asynchronously retrieves a random recipe from the collection of dinner recipes available in the database.
    /// This method fetches all dinner recipes, selects one randomly, and returns it as a single recipe object.
    /// <returns>An instance of Recipe that represents a random dinner recipe.</returns>
    public async Task<Recipe> GetRandomRecipe()
    {
        Console.WriteLine("Getting random recipe...");
        var result = await GetDinnerRecipes();
        var randomRecipe = result.Recipes[new Random().Next(0, result.Recipes.Count - 1)];
        return randomRecipe;
    }

    public async Task<(List<RecipeInstructionRecord>? Instructions, string Message)> GetAllRecipeInstructions()
    {
        Console.WriteLine("Getting all recipe instructions...");
        var recipeInstructionsRecords = new List<RecipeInstructionRecord>();
        string statusMessage = "Recipe instructions successfully retrieved.";

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

                if (instructions == null)
                {
                    Console.WriteLine("Error deserializing instructions.");
                    return (null, "Error deserializing instructions; instructions are null.");
                }

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
            return (null, $"Error getting all recipe instructions: {ex.Message}.");
        }

        return (recipeInstructionsRecords, statusMessage);
    }

    /// Asynchronously retrieves all recipes from the database and returns them along with a status message.
    /// <returns>A tuple containing a list of Recipe objects (nullable) and a status message as a string. If an error occurs, the list will be null, and the status message will indicate the error.</returns>
    public async Task<(List<Recipe>? Recipes, string Message)> GetAllRecipes()
    {
        Console.WriteLine("Getting recipes...");

        // var base64PlaceHolderPic = await GetPlaceHolderPic();
        List<Recipe> recipes = new();
        string statusMessage = "Recipes successfully retrieved.";

        const string query = "SELECT id, name, image, meal_type," +
                             "(macros).total_calories," +
                             "(macros).total_fats," +
                             "(macros).total_carbs," +
                             "(macros).total_protein " +
                             "FROM recipes " +
                             "ORDER BY id";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tempRecipe = await BuildRecipe(reader);
                recipes.Add(tempRecipe);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting all recipes: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting all recipes: {ex.Message}.");
        }

        return (recipes, statusMessage);
    }

    /// Retrieves a list of ingredients associated with a specific recipe by its unique identifier.
    /// <param name="id">The unique identifier of the recipe for which ingredients are to be fetched.</param>
    /// <returns>A task that represents the asynchronous operation, returning a list of ingredients associated with the specified recipe.</returns>
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
                    FatPer100g = reader.GetFloat(5)
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
    public async Task<(List<Ingredient>? DbIngredients, string Message)> GetAllDbIngredients()
    {
        Console.WriteLine("Getting all database ingredients...");

        var base64PlaceHolderPic = await GetPlaceHolderPic();

        List<Ingredient> ingredients = [];
        string statusMessage = "Ingredients successfully retrieved.";

        const string query = "SELECT id, name, cals, fats, carbs, protein, image FROM ingredients ORDER BY id";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Ingredient tempIngredient = new()
                {
                    Name = reader.GetString(1),
                    CaloriesPer100g = reader.GetFloat(2),
                    FatPer100g = reader.GetFloat(3),
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
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting all database ingredients: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting all database ingredients: {ex.Message}.");
        }

        return (ingredients, statusMessage);
    }

    /// Retrieves an ingredient from the database by its unique identifier.
    /// <param name="id">The unique identifier of the ingredient to retrieve.</param>
    /// <returns>A tuple containing the retrieved Ingredient object and a status message. If the ingredient is not found or an error occurs, the Ingredient object will be null and the message will describe the outcome.</returns>
    public async Task<(Ingredient? Ingredient, string Message)> GetDbIngredientById(int id)
    {
        Console.WriteLine("Getting ingredient by id...");

        await using var conn = await GetConnection();
        var ingredient = new Ingredient();
        string statusMessage = "Ingredient successfully retrieved.";

        const string query = "SELECT * FROM ingredients WHERE id = @id";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var uintValue = unchecked((uint)reader.GetInt32(0));
                ingredient.Name = reader.GetString(1);
                ingredient.CaloriesPer100g = reader.GetFloat(2);
                ingredient.FatPer100g = reader.GetFloat(3);
                ingredient.CarbsPer100g = reader.GetFloat(4);
                ingredient.ProteinPer100g = reader.GetFloat(5);
                ingredient.Base64Image = reader.GetString(6);
                ingredient.SetId(uintValue);
            }
            else
            {
                Console.WriteLine("Ingredient not found.");
                return (null, "Ingredient not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting ingredient by id ({id}): " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            return (null, $"Error getting ingredient by id: {ex.Message}.");
        }

        return (ingredient, statusMessage);
    }

    /// Asynchronously adds a recipe to the database by inserting its details, including meal type, name, image, macros, and ingredients.
    /// <param name="recipe">The recipe object containing all the required details to be added to the database.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task<(bool Status, string Message)> AddRecipeToDb(Recipe recipe)
    {
        Console.WriteLine("Adding recipe to database...");

        string statusMessage = "Recipe successfully added.";
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

        recipe.PrintRecipe();
        try
        {
            await using var conn = await GetConnection();
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
            return (false, $"Error adding recipe to database: {ex.Message}.");
        }

        return (true, statusMessage);
    }

    /// Asynchronously adds a recipe's instructions to the database.
    /// <param name="instructions">An object containing the instructions and the associated recipe ID.</param>
    /// <return>A Task representing the asynchronous operation.</return>
    public async Task<(bool Status, string Message)> AddInstructionToDb(RecipeInstructionRecord instructions)
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
            Console.WriteLine($"Error adding instructions ({instructions}) to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, $"Error adding instructions to database: {ex.Message}.");
        }

        return (true, statusMessage);
    }

    /// Adds a list of ingredients to the corresponding row within the recipes database table.
    /// <param name="ingredients">The list of ingredients to add, each represented by an Ingredient object.</param>
    /// <return>A Task that represents the asynchronous operation.</return>
    private async Task<(bool Status, string Message)> AddIngredientsToRow(List<Ingredient> ingredients)
    {
        Console.WriteLine("Adding ingredients to row...");

        string statusMessage = "Ingredients successfully added.";

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

        try
        {
            await using var conn = await GetConnection();
            foreach (var ingredient in ingredients)
            {
                await using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@name", ingredient.Name);
                cmd.Parameters.AddWithValue("@grams", ingredient.Grams);
                cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
                cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                await RunAsyncQuery(cmd);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding ingredients ({ingredients}) to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, "Error adding ingredients to row: " + ex.Message);
        }

        return (true, statusMessage);
    }

    /// Asynchronously adds an ingredient to the database.
    /// <param name="ingredient">The ingredient object containing details such as name, calories, fats, carbs, protein, and image to be added to the database.</param>
    /// <return>A task representing the asynchronous operation.</return>
    public async Task<(bool Status, string Message)> AddIngredientToDb(Ingredient ingredient)
    {
        Console.WriteLine("Adding ingredient to database...");

        string statusMessage = "Ingredient successfully added.";

        const string query =
            "INSERT INTO ingredients " +
            "(name, cals, fats, carbs, protein, image) " +
            "VALUES(@name, @cals, @fats, @carbs, @protein, @image)";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding ingredients ({ingredient}) to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, $"Error adding ingredients to database: {ex.Message}.");
        }

        return (true, statusMessage);
    }

    /// Updates the meal type of a recipe in the database based on the provided recipe ID.
    /// <param name="mealType">The new meal type to set for the specified recipe.</param>
    /// <param name="recipeId">The ID of the recipe to be updated.</param>
    /// <returns>A status message indicating the success or failure of the operation.</returns>
    public async Task<string> UpdateRecipeMealTypeRecipeId(string mealType, int recipeId)
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
            Console.WriteLine($"Error updating recipe meal type ({mealType}) by recipe id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return statusMessage;
    }

    /// Updates the macros and ingredients of a recipe in the database by its ID.
    /// <param name="recipe">The Recipe object containing the updated macros and ingredients to be saved to the database.</param>
    /// <param name="recipeId">The unique identifier of the recipe to be updated in the database.</param>
    /// <returns>A string message indicating the status of the update operation.</returns>
    public async Task<string> UpdateRecipeMacrosAndIngredientsById(Recipe recipe, int recipeId)
    {
        Console.WriteLine($"Updating recipe macros and ingredients by id ({recipeId})...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Recipe ingredients did not get updated; recipe ID is less than 1";
        }

        const string query = "UPDATE recipes " +
                             "SET macros = ROW(" +
                             "@calories," +
                             "@fat," +
                             "@carbs," +
                             "@protein) " +
                             "WHERE id = @recipe_id";

        var statusMessage = "Recipe ingredients got updated.";
        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@calories", recipe.TotalMacros.Calories);
            cmd.Parameters.AddWithValue("@fat", recipe.TotalMacros.Fat);
            cmd.Parameters.AddWithValue("@carbs", recipe.TotalMacros.Carbs);
            cmd.Parameters.AddWithValue("@protein", recipe.TotalMacros.Protein);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);
            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                return "Recipe ingredients did not get updated; recipe ID was not found in database.";

            await UpdateIngredients(recipe.Ingredients, recipeId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error updating recipe ingredients by id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Recipe ingredients did not get updated; error updating recipe ingredients by id ({recipeId}): " +
                   ex.Message;
        }

        return statusMessage;
    }

    /// Removes all ingredients associated with the specified recipe ID by updating the recipe entry to have an empty ingredients array.
    /// <param name="recipeId">The unique identifier of the recipe whose ingredients are to be removed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// Updates the ingredients for a specific recipe in the database by first clearing the existing ingredients
    /// associated with the recipe and then inserting the new list of provided ingredients.
    /// <param name="ingredients">A list of ingredients to be added to the recipe.</param>
    /// <param name="recipeId">The unique identifier of the recipe whose ingredients are being updated.</param>
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
                cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                cmd.Parameters.AddWithValue("@recipe_id", recipeId);
                var result = await RunAsyncQuery(cmd);
                if (result < 1)
                    Console.WriteLine("Recipe ID is not found in database.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }
    }

    /// Updates the image of a recipe identified by its ID with the provided base64-encoded string representation of the image.
    /// <param name="base64Image">The base64-encoded string representation of the new image to update.</param>
    /// <param name="recipeId">The unique identifier of the recipe whose image is being updated. Must be greater than 0.</param>
    /// <returns>A message indicating whether the image update operation was successful or detailing any error that occurred.</returns>
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
            Console.WriteLine($"Error updating recipe image by recipe id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating recipe image by recipe id ({recipeId}): " + ex.Message;
        }

        return statusMessage;
    }

    /// Updates the name of a recipe in the database based on the provided recipe ID.
    /// <param name="updatedName">The new name to update the recipe with.</param>
    /// <param name="recipeId">The ID of the recipe to be updated.</param>
    /// <returns>A string indicating the status of the update operation.</returns>
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
            Console.WriteLine($"Error updating recipe name by recipe id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating recipe name by recipe id ({recipeId}): " + ex.Message;
        }

        return statusMessage;
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
    public async Task<string> UpdateDbIngredient(Ingredient ingredient)
    {
        Console.WriteLine("Updating database ingredient...");

        string statusMessage = "Ingredient got updated.";
        const string query = "UPDATE ingredients " +
                             "SET " +
                             "name = @name," +
                             "cals = @cals," +
                             "fats = @fats," +
                             "carbs = @carbs," +
                             "protein = @protein," +
                             "image = @image " +
                             "WHERE id = @id";

        try
        {
            await using var conn = await GetConnection();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            cmd.Parameters.AddWithValue("@id", ingredient.GetIntId());

            if (isIngredientIdZero(ingredient))
            {
                Console.WriteLine("Ingredient ID is zero. Not updating.");
                return "Ingredient ID is zero. Not updating.";
            }

            var result = await RunAsyncQuery(cmd);
            if (result == 0)
            {
                Console.WriteLine("Ingredient ID is not found in database.");
                statusMessage = "Ingredient did not get updated; ingredient ID was not found in database.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating database ingredient ({ingredient}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating database ingredient ({ingredient}): " + ex.Message;
        }

        return statusMessage;
    }

    /// Deletes an ingredient from the database using its unique identifier.
    /// <param name="id">The unique identifier of the ingredient to be deleted.</param>
    /// <returns>A status message indicating whether the deletion was successful or if the ingredient was not found.</returns>
    public async Task<string> DeleteDbIngredientById(int id)
    {
        Console.WriteLine("Deleting database ingredient by name...");
        var statusMessage = $"Ingredient {id} has been deleted.";

        try
        {
            await using var conn = await GetConnection();
            const string query = "DELETE FROM ingredients WHERE id = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);

            int result = await RunAsyncQuery(cmd);
            if (result < 1)
                statusMessage = $"Ingredient {id} was not found.";
            else
                await UpdateTableIds("ingredients");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting database ingredient by id ({id})" + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error deleting database ingredient by id ({id}): " + ex.Message;
        }

        return statusMessage;
    }

    /// Asynchronously deletes a recipe from the database by its unique identifier.
    /// <param name="recipeId">The unique identifier of the recipe to be deleted.</param>
    /// <returns>A string indicating the result of the operation, including success or failure messages.</returns>
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
        {
            await UpdateTableIds("recipes");
            await UpdateTableIds("recipe_instructions");
        }

        return statusMessage;
    }

    /// Determines whether the ingredient's ID is zero.
    /// <param name="ingredient">The ingredient to check.</param>
    /// <returns>True if the ingredient's ID is zero; otherwise, false.</returns>
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