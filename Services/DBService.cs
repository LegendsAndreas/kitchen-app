using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]
// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&user=[USERNAME]
// Remember that a connection string largely opperates on regex logic, so it does not matter too much where you put your variables.
// So, if you don't need sslmode, you can just delete it entirely.
// jdbc:postgresql://ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech/neondb?sslmode=require&user=neondb_owner&password=vVljNo8xGsb5

namespace WebKitchen.Services;

public class DBService
{
    private int _totalRecipes;
    public int _maxRecipesPages;
    private int _totalIngredients;
    public int _maxIngredientsPages;
    private readonly string _connectionString;
    const int ITEMS_PER_PAGE = 20;

    public DBService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SetTotalVariables()
    {
        _totalRecipes = await GetTotalRecipes();
        _totalIngredients = await GetTotalIngredients();
        Console.WriteLine($"Total Recipes: {_totalRecipes}");
        Console.WriteLine($"Total Ingredients: {_totalIngredients}");
        _maxRecipesPages = (int)Math.Ceiling((double)_totalRecipes / ITEMS_PER_PAGE);
        _maxIngredientsPages = (int)Math.Ceiling((double)_totalIngredients / ITEMS_PER_PAGE);
        Console.WriteLine($"Max Recipes Pages: {_maxRecipesPages}");
        Console.WriteLine($"Max Ingredients Pages: {_maxIngredientsPages}");
    }

    private async Task<int> GetTotalRecipes()
    {
        int totalRecipes = 0;
        const string query = "SELECT COUNT(*) FROM recipes";
        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totalRecipes = reader.GetInt32(0);
            }
            else
            {
                Console.WriteLine("Error getting total recipes.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }

        return totalRecipes;
    }

    private async Task<int> GetTotalIngredients()
    {
        int totalIngredients = 0;
        const string query = "SELECT COUNT(*) FROM ingredients";
        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totalIngredients = reader.GetInt32(0);
            }
            else
            {
                Console.WriteLine("Error getting total recipes.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }

        return totalIngredients;
    }

    /// Asynchronously creates and returns an open database connection using the configured connection string.
    /// Remember to close it when done, or by using the 'using' keyword it.
    private async Task<NpgsqlConnection> GetConnectionAsync()
    {
        // Console.WriteLine("Getting connection...");
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<(List<Recipe>? Recipes, string Message)> GetAllRecipesAsync(CancellationToken ct = new())
    {
        List<Recipe> recipes = [];

        const string query = "SELECT r.id, " +
                             "r.name, " +
                             "r.meal_type, " +
                             "r.image, " +
                             "r.cost, " +
                             "(r.macros).total_calories, " +
                             "(r.macros).total_carbs, " +
                             "(r.macros).total_fats, " +
                             "(r.macros).total_protein, " +
                             "json_agg(" +
                             "    json_build_object(" +
                             "         'name', i.name," +
                             "         'grams', i.grams," +
                             "         'calories_pr_hectogram', i.calories_pr_hectogram," +
                             "         'fats_pr_hectogram', i.fats_pr_hectogram," +
                             "         'carbs_pr_hectogram', i.carbs_pr_hectogram," +
                             "         'protein_pr_hectogram', i.protein_pr_hectogram," +
                             "         'cost_per_hectogram', COALESCE(i.cost_per_100g, 0)," +
                             "         'multiplier', i.multiplier" +
                             "     )" +
                             ") AS ingredients " +
                             "FROM recipes AS r, unnest(r.ingredients) AS i " +
                             "GROUP BY r.id " +
                             "ORDER BY r.id ";

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                Recipe tempRecipe = MakeRecipe(reader);
                recipes.Add(tempRecipe);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting all recipes: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting all recipes: {ex.Message}.");
        }

        return (recipes, "Recipes successfully retrieved");
    }

    /// Retrieves a list of dinner recipes from the database, including their details and nutritional information.
    /// If a recipe does not have a specific image, a placeholder image is substituted.
    /// <returns>A tuple containing a list of Recipe objects and a status message indicating the result of the operation.</returns>
    private async Task<(List<Recipe> Recipes, string Message)> GetDinnerRecipes()
    {
        Console.WriteLine("Getting dinner recipes...");

        List<Recipe> recipes = [];
        await using var conn = await GetConnectionAsync();

        const string query = "SELECT r.id, " +
                             "r.name, " +
                             "r.meal_type, " +
                             "r.image, " +
                             "r.cost, " +
                             "(r.macros).total_calories, " +
                             "(r.macros).total_carbs, " +
                             "(r.macros).total_fats, " +
                             "(r.macros).total_protein, " +
                             "json_agg(" +
                             "    json_build_object(" +
                             "         'name', i.name," +
                             "         'grams', i.grams," +
                             "         'calories_pr_hectogram', i.calories_pr_hectogram," +
                             "         'fats_pr_hectogram', i.fats_pr_hectogram," +
                             "         'carbs_pr_hectogram', i.carbs_pr_hectogram," +
                             "         'protein_pr_hectogram', i.protein_pr_hectogram," +
                             "         'cost_per_hectogram', COALESCE(i.cost_per_100g, 0)," +
                             "         'multiplier', i.multiplier" +
                             "     )" +
                             ") AS ingredients " +
                             "FROM recipes AS r, unnest(r.ingredients) AS i " +
                             "WHERE r.meal_type = 'D' " +
                             "GROUP BY r.id " +
                             "ORDER BY r.id ";

        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Recipe recipe = MakeRecipe(reader);
            recipes.Add(recipe);
        }

        return (recipes, "Recipes successfully retrieved");
    }

    public async Task<(Recipe? Recipe, string Message)> GetRecipeByIdAsync(int recipeId)
    {
        Console.WriteLine("Getting recipe by id...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1.");
            return (null, "Recipe ID is less than 1.");
        }

        Recipe recipe;

        // In case cost_per_hectogram is null, we need to use COALESCE to replace it with 0.
        // Adding a column for the value type, with default 0 on the type did not go as planned.
        const string query = "SELECT r.id, " +
                             "r.name, " +
                             "r.meal_type, " +
                             "r.image, " +
                             "r.cost, " +
                             "(r.macros).total_calories, " +
                             "(r.macros).total_carbs, " +
                             "(r.macros).total_fats, " +
                             "(r.macros).total_protein, " +
                             "json_agg(" +
                             "    json_build_object(" +
                             "         'name', i.name," +
                             "         'grams', i.grams," +
                             "         'calories_pr_hectogram', i.calories_pr_hectogram," +
                             "         'fats_pr_hectogram', i.fats_pr_hectogram," +
                             "         'carbs_pr_hectogram', i.carbs_pr_hectogram," +
                             "         'protein_pr_hectogram', i.protein_pr_hectogram," +
                             "         'cost_per_hectogram', COALESCE(i.cost_per_100g, 0)," +
                             "         'multiplier', i.multiplier" +
                             "     )" +
                             ") AS ingredients " +
                             "FROM recipes AS r, unnest(r.ingredients) AS i " +
                             "WHERE r.id = @id " +
                             "GROUP BY r.id " +
                             "ORDER BY r.id ";
        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@id", recipeId);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                recipe = MakeRecipe(reader);
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

        return (recipe, "Recipe successfully retrieved");
    }

    // MAYBE, make this Async
    private Recipe MakeRecipe(NpgsqlDataReader reader)
    {
        return new Recipe
        {
            RecipeId = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            MealType = reader.GetString(reader.GetOrdinal("meal_type")),
            Base64Image = reader.GetString(reader.GetOrdinal("image")),
            TotalCost = reader.GetFloat(reader.GetOrdinal("cost")),
            TotalMacros = new Macros
            {
                Calories = reader.GetFloat(reader.GetOrdinal("total_calories")),
                Fat = reader.GetFloat(reader.GetOrdinal("total_fats")),
                Carbs = reader.GetFloat(reader.GetOrdinal("total_carbs")),
                Protein = reader.GetFloat(reader.GetOrdinal("total_protein"))
            },
            Ingredients =
                JsonSerializer.Deserialize<List<Ingredient>>(
                    reader.GetString(reader.GetOrdinal("ingredients"))) ??
                []
        };
    }

    public async Task<string> AddCommonItem(int itemId, string itemTable, decimal itemPrice)
    {
        Console.WriteLine("Adding common item...");

        Console.WriteLine($"Item ID: {itemId} | Item Table: {itemTable} | Item Price: {itemPrice}");

        string selectQuery;
        if (itemTable == "recipes")
            selectQuery = "SELECT name, image FROM recipes WHERE id = @id;";
        else if (itemTable == "ingredients")
        {
            selectQuery = "SELECT name, image FROM ingredients WHERE id = @id;";
        }
        else
        {
            Console.WriteLine("Invalid item type.");
            return "Error adding common item; invalid item type.";
        }

        if (itemId < 1)
        {
            Console.WriteLine("Item ID is less than 1.");
            return "Error adding common item; item ID is less than 1.";
        }

        string dbItemName;
        string dbItemImage;

        string insertQuery =
            "INSERT INTO sought_after_items (item_id, name, image, type, price) VALUES (@id, @name, @image, @type, @price);";

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using var selectCmd = new NpgsqlCommand(selectQuery, conn);
            selectCmd.Parameters.AddWithValue("@id", itemId);
            // selectCmd.Parameters.AddWithValue("@table", table);

            Console.WriteLine("Select Query: " + selectQuery);
            await using (NpgsqlDataReader selectReader = await selectCmd.ExecuteReaderAsync())
            {
                if (await selectReader.ReadAsync())
                {
                    dbItemName = selectReader.GetString(0);
                    dbItemImage = selectReader.GetString(1);
                }
                else
                {
                    Console.WriteLine("Select Query: " + selectQuery);
                    return "Error adding common item; item ID does not exist.";
                }
            }


            // await using var insertCmd = new NpgsqlCommand(insertQuery, conn);
            await using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
            {
                Console.WriteLine("Insert Query: " + insertQuery);
                insertCmd.Parameters.AddWithValue("@id", itemId);
                insertCmd.Parameters.AddWithValue("@name", dbItemName);
                insertCmd.Parameters.AddWithValue("@image", dbItemImage);
                insertCmd.Parameters.AddWithValue("@type", itemTable);
                insertCmd.Parameters.AddWithValue("@price", itemPrice);
                await RunAsyncQuery(insertCmd);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding common item: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error adding common item: {ex.Message}.";
        }

        return "Common item successfully added";
    }

    public async Task<(List<Recipe>? Recipes, string Message)> GetRecipesByCategory(string baseCategory,
        string secondaryCategory, string sortingDirection, int? minNum, int? maxNum)
    {
        Console.WriteLine("Getting recipes by category...");

        List<Recipe> recipes = new();
        var statusMessage = "Recipes successfully retrieved with query: ";
        var query = "SELECT id, name, image, meal_type," +
                    "(macros).total_calories," +
                    "(macros).total_fats," +
                    "(macros).total_carbs," +
                    "(macros).total_protein " +
                    "FROM recipes";

        if (minNum != null || maxNum != null)
        {
            if (minNum > maxNum)
                return (null,
                    $"Invalid sorting parameters; The minimum value ({minNum}) is more than the maximum value ({maxNum}).");

            if (minNum == null)
            {
                query += $" WHERE (macros).total_{secondaryCategory} <= {maxNum}";
            }
            else if (maxNum == null)
            {
                query += $" WHERE (macros).total_{secondaryCategory} >= {minNum}";
            }
            else
                query +=
                    $" WHERE (macros).total_{secondaryCategory} >= {minNum} AND (macros).total_{secondaryCategory} <= {maxNum}";
        }

        if (baseCategory == "name")
            query += " ORDER BY name";
        else if (baseCategory == "meal_type")
            query += " ORDER BY meal_type";
        else if (baseCategory == "macros")
        {
            if (secondaryCategory == "calories")
                query += " ORDER BY (macros).total_calories";
            else if (secondaryCategory == "carbs")
                query += " ORDER BY (macros).total_carbs";
            else if (secondaryCategory == "fats")
                query += " ORDER BY (macros).total_fats";
            else if (secondaryCategory == "protein")
                query += " ORDER BY (macros).total_protein";
            else
                return (null, $"Invalid category \"{secondaryCategory}\".");
        }
        else
            return (null, $"Invalid category \"{baseCategory}\".");

        if (!string.IsNullOrEmpty(sortingDirection))
        {
            if (sortingDirection != "asc" && sortingDirection != "desc")
                return (null,
                    $"Invalid sorting direction ({sortingDirection}); direction can only be 'asc' or 'desc'.");

            query += $" {sortingDirection.ToUpper()}";
        }

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tempRecipe = MakeRecipe(reader);
                recipes.Add(tempRecipe);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error getting recipes by category \"{baseCategory}\" and query \"{query}\": " +
                ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting recipes by category ({baseCategory}): {ex.Message}.");
        }

        return (recipes, statusMessage + query);
    }

    /// Retrieves a record of recipe instructions associated with the specified recipe ID from the database.
    /// <param name="recipeId">The unique identifier of the recipe for which instructions are to be retrieved.</param>
    /// <returns>A RecipeInstructionRecord containing the instructions if found, or null if not found.</returns>
    public async Task<(RecipeInstructionRecord? instructions, string message)> GetRecipeInstructionsByRecipeId(
        int recipeId)
    {
        Console.WriteLine("Getting recipe instructions by id...");
        RecipeInstructionRecord instructionsRecord;
        const string query =
            "SELECT id, instructions, recipe_id FROM recipe_instructions WHERE recipe_id = @recipe_Id;";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@recipe_Id", recipeId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var jsonData = reader.GetString(1);
                var currentRecipeId = reader.GetInt32(2);

                var instructions = JsonSerializer.Deserialize<RecipeInstructions>(jsonData);

                if (instructions == null)
                {
                    Console.WriteLine("Error deserializing instructions.");
                    return (null, "Error deserializing instructions; instructions are null.");
                }

                var tempInstructionsRecord = new RecipeInstructionRecord { Instructions = instructions };
                tempInstructionsRecord.SetId(id);
                tempInstructionsRecord.SetRecipeId(currentRecipeId);

                instructionsRecord = tempInstructionsRecord;
            }
            else
            {
                Console.WriteLine("Recipe instructions not found.");
                return (null, "Recipe instructions not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting recipe instructions by id ({recipeId}): " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            return (null, $"Error getting recipe instructions by id ({recipeId}): {ex.Message}.");
        }

        return (instructionsRecord, "Recipe instructions successfully retrieved");
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

    /*public async Task<(List<RecipeInstructionRecord>? Instructions, string Message)> GetAllRecipeInstructions()
    {
        Console.WriteLine("Getting all recipe instructions...");
        var recipeInstructionsRecords = new List<RecipeInstructionRecord>();
        string statusMessage = "Recipe instructions successfully retrieved.";

        const string query = "SELECT id, instructions, recipe_id FROM recipe_instructions";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var jsonData = reader.GetString(1);
                var recipeId = reader.GetInt32(2);

                Console.WriteLine("JsonData: " + jsonData);

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
    }*/

    public async Task<(List<Ingredient>? DbIngredients, string Message)> GetAllDbIngredients(
        CancellationToken ct = new())
    {
        Console.WriteLine("Getting all database ingredients...");

        List<Ingredient> ingredients = [];
        string statusMessage = "Ingredients successfully retrieved.";

        const string query =
            "SELECT id, name, cals, fats, carbs, protein, image, cost_per_100g FROM ingredients ORDER BY id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                Ingredient tempIngredient = MakeIngredient(reader);
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

    private Ingredient MakeIngredient(NpgsqlDataReader reader)
    {
        Ingredient tempIngredient = new()
        {
            Name = reader.GetString(1),
            CaloriesPer100g = reader.GetFloat(2),
            FatPer100g = reader.GetFloat(3),
            CarbsPer100g = reader.GetFloat(4),
            ProteinPer100g = reader.GetFloat(5),
            Base64Image = reader.GetString(6),
            CostPer100g = reader.GetFloat(7)
        };
        var uintValue = unchecked((uint)reader.GetInt32(0));
        tempIngredient.SetId(uintValue);

        return tempIngredient;
    }

    public async Task<(Ingredient? Ingredient, string Message)> GetDbIngredientById(int id)
    {
        Console.WriteLine("Getting ingredient by id...");

        Ingredient ingredient;
        string statusMessage = "Ingredient successfully retrieved.";

        const string query = "SELECT * FROM ingredients WHERE id = @id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                ingredient = MakeIngredient(reader);
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

    /*public async Task<(Ingredient? Ingredient, string Message)> GetDbIngredientByName(string name)
    {
        Console.WriteLine("Getting ingredient by name...");

        await using var conn = await GetConnectionAsync();
        Ingredient ingredient = new();
        string statusMessage = "Ingredient successfully retrieved.";

        const string query = "SELECT * FROM ingredients WHERE name = @name";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", name);

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
                ingredient.CostPer100g = reader.GetFloat(7);
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
            Console.WriteLine($"Error getting ingredient by name ({name}): " + ex.Message);
            Console.WriteLine("Stacktrace: " + ex.StackTrace);
            return (null, $"Error getting ingredient by id: {ex.Message}.");
        }

        return (ingredient, statusMessage);
    }*/

    public async Task<(List<CommonItem>? CommonItems, string Message)> GetAllCommonItems()
    {
        Console.WriteLine("Getting all common items...");

        List<CommonItem> commonItems = [];
        string statusMessage = "Ingredients successfully retrieved.";

        const string query = "SELECT * FROM sought_after_items";

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                CommonItem tempIngredient = new()
                {
                    Name = reader.GetString(1),
                    Base64Image = reader.GetString(2),
                    Type = reader.GetString(3),
                    Cost = reader.GetDecimal(4),
                };
                var uintValue = unchecked((uint)reader.GetInt32(0));
                tempIngredient.SetId(uintValue);
                commonItems.Add(tempIngredient);
            }

            if (commonItems.Count == 0)
                return (null, "No common items added.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            return (null, $"Error getting all common items: {ex.Message}");
        }

        return (commonItems, statusMessage);
    }

    public async Task<(bool Status, string Message)> EditFullRecipe(Recipe updatedRecipe)
    {
        try
        {
            await UpdateRecipeNameByRecipeId(updatedRecipe.Name, updatedRecipe.RecipeId);
            await UpdateRecipeMealTypeRecipeIdAsync(updatedRecipe.MealType, updatedRecipe.RecipeId);
            await UpdateRecipeMacrosAndIngredientsByRecipeIdAsync(updatedRecipe, updatedRecipe.RecipeId);
            await UpdateRecipeImageByRecipeId(updatedRecipe.Base64Image, updatedRecipe.RecipeId);

            await EmptyRecipeIngredientsByRecipeId(updatedRecipe.RecipeId);
            if (updatedRecipe.Ingredients != null && updatedRecipe.Ingredients.Count > 0)
                await AddIngredientsToRowAsync(updatedRecipe.Ingredients);

            return (true, "Recipe updated successfully.");
        }
        catch (Exception ex)
        {
            return (false, $"Error editing recipe: {ex.Message}");
        }
    }

    /// Fucked
    public async Task<(bool Status, string Message)> AddRecipeToDb(Recipe recipe)
    {
        Console.WriteLine("Adding recipe to database...");

        string statusMessage = "Recipe successfully added.";
        const string query = "INSERT INTO recipes " +
                             "(meal_type, name, image, macros, cost) " +
                             "VALUES (" +
                             "@type," +
                             "@name," +
                             "@image," +
                             "ROW(" +
                             "@calories," +
                             "@fats," +
                             "@carbs," +
                             "@protein)::recipe_macros, " +
                             "@cost)";
        // Console.WriteLine("Query: " + query);
        // recipe.PrintRecipe();
        try
        {
            await using var conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@type", recipe.MealType);
            cmd.Parameters.AddWithValue("@name", recipe.Name);
            cmd.Parameters.AddWithValue("@image", recipe.Base64Image);
            cmd.Parameters.AddWithValue("@calories", recipe.TotalMacros.Calories);
            cmd.Parameters.AddWithValue("@fats", recipe.TotalMacros.Fat);
            cmd.Parameters.AddWithValue("@carbs", recipe.TotalMacros.Carbs);
            cmd.Parameters.AddWithValue("@protein", recipe.TotalMacros.Protein);
            cmd.Parameters.AddWithValue("@cost", recipe.TotalCost);
            await RunAsyncQuery(cmd);
            var result = await AddIngredientsToRowAsync(recipe.Ingredients);
            if (result.Status == false)
                return (false, "Error adding ingredients to row; " + result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding recipe to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, $"Error adding recipe to database: {ex.Message}.");
        }

        return (true, statusMessage);
    }

    public async Task<(bool Status, string Message)> AddInstructionToDbAsync(RecipeInstructionRecord instructions)
    {
        Console.WriteLine("Adding instructions to database...");

        var statusMessage = $"Instructions successfully added to recipe {instructions.GetRecipeId()}.";
        var serializedJsonData = JsonSerializer.Serialize(instructions.Instructions);
        const string query = "INSERT INTO recipe_instructions " +
                             "(instructions, recipe_id) " +
                             "VALUES (" +
                             "@json_data," +
                             "@recipe_id)";

        try
        {
            await using var conn = await GetConnectionAsync();
            NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@json_data", NpgsqlTypes.NpgsqlDbType.Json, serializedJsonData);
            cmd.Parameters.AddWithValue("@recipe_id", instructions.GetRecipeId());
            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                return (false, "Instructions did not get added to database; recipe ID was not found in database.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding instructions ({instructions}) to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, $"Error adding instructions to database: {ex.Message}.");
        }

        return (true, statusMessage);
    }

    /// Appends a new row of type 'ingredient' to the array of the recipe, found by counting all the recipes in the database.
    /// The query sent to the database actually gets sent as many times as the length of the ingredient array.
    private async Task<(bool Status, string Message)> AddIngredientsToRowAsync(List<Ingredient> ingredients)
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
                             "@multiplier, " +
                             "@cost_per_100g" +
                             ")::ingredient) " +
                             "WHERE id IN (SELECT COUNT(*) FROM recipes)"; // Since every new recipe is actually the last
        // one in the table, we can just add all the occurrences of id into one, and that will be our recipe id.

        try
        {
            await using var conn = await GetConnectionAsync();
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
                cmd.Parameters.AddWithValue("@cost_per_100g", ingredient.CostPer100g);
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

    public async Task<(bool Status, string Message)> AddIngredientToDbAsync(Ingredient ingredient)
    {
        Console.WriteLine("Adding ingredient to database...");

        string statusMessage = "Ingredient successfully added.";

        const string query =
            "INSERT INTO ingredients " +
            "(name, cals, fats, carbs, protein, image, cost_per_100g) " +
            "VALUES(@name, @cals, @fats, @carbs, @protein, @image, @costper100g)";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            cmd.Parameters.AddWithValue("@costper100g", ingredient.GetCostPer100g());
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

    public async Task<string> UpdateRecipeMealTypeRecipeIdAsync(string mealType, int recipeId)
    {
        Console.WriteLine("Updating recipe meal type by name...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Meal type did not get updated; recipe ID is less than 1";
        }

        const string query = "UPDATE recipes SET meal_type = @type WHERE id = @recipe_id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@type", mealType);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);

            var result = await RunAsyncQuery(cmd);

            if (result == 0)
            {
                Console.WriteLine("Recipe ID is not found in database.");
                return "Meal type did not get updated; recipe ID was not found in database.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating recipe meal type ({mealType}) by recipe id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return "Meal type did not get updated; an error occurred: " + ex.Message;
        }

        return "Meal type got updated.";
    }

    public async Task<string> UpdateRecipeMacrosAndIngredientsByRecipeIdAsync(Recipe recipe, int recipeId)
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
                             "@protein), " +
                             "cost = @cost " +
                             "WHERE id = @recipe_id";
        Console.WriteLine("Cost: " + recipe.TotalCost);

        var statusMessage = "Recipe ingredients got updated.";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@calories", recipe.TotalMacros.Calories);
            cmd.Parameters.AddWithValue("@fat", recipe.TotalMacros.Fat);
            cmd.Parameters.AddWithValue("@carbs", recipe.TotalMacros.Carbs);
            cmd.Parameters.AddWithValue("@protein", recipe.TotalMacros.Protein);
            cmd.Parameters.AddWithValue("@cost", recipe.TotalCost);
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
            await using var conn = await GetConnectionAsync();
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
                             "@multiplier, " +
                             "@cost_per_100g " +
                             ")::ingredient) " +
                             "WHERE id = @recipe_id";

        try
        {
            await using var conn = await GetConnectionAsync();
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
                cmd.Parameters.AddWithValue("@cost_per_100g", ingredient.CostPer100g);
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
            await using var conn = await GetConnectionAsync();
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
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@updatedName", updatedName);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);

            var result = await RunAsyncQuery(cmd);

            if (result == 0)
            {
                Console.WriteLine("Recipe ID is not found in database.");
                statusMessage = "Recipe name did not get updated; recipe ID was not found in database.";
            }

            var recipeHasInstructions = await DoesRecipeHaveInstructions(recipeId);
            if (recipeHasInstructions)
            {
                var instructionsRecipeNameResult = await UpdateInstructionsRecipeNameByRecipeId(recipeId, updatedName);
                if (!instructionsRecipeNameResult.status)
                    return $"instructions recipe name did not get updated; {instructionsRecipeNameResult.message}";
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

    private async Task<bool> DoesRecipeHaveInstructions(int recipeId)
    {
        Console.WriteLine("does recipe have instructions...");
        const string query = "SELECT EXISTS (" +
                             "    SELECT 1 " +
                             "    FROM recipe_instructions " +
                             "    WHERE recipe_id = @recipe_id);";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);
            var result = await cmd.ExecuteReaderAsync();
            if (result.Read())
            {
                var exists = result.GetBoolean(0);
                if (exists)
                    return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error checking if recipe has instructions: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            throw;
        }

        return false;
    }

    /// Asynchronously updates the ID sequence values for a specified database table to ensure sequential numbering.
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
            await using var conn = await GetConnectionAsync();
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
                             "image = @image, " +
                             "cost_per_100g = @cost " +
                             "WHERE id = @id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            cmd.Parameters.AddWithValue("@cost", ingredient.CostPer100g);
            cmd.Parameters.AddWithValue("@id", ingredient.GetIntId());

            if (IsIngredientIdZero(ingredient))
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

    public async Task<(List<Ingredient>? Ingredients, string Message)> GetSearchIngredients(string searchParameter)
    {
        List<Ingredient> ingredients = [];

        string query =
            "SELECT " +
            "id," +
            "name," +
            "cals," +
            "fats," +
            "carbs," +
            "protein," +
            "image," +
            "cost_per_100g " +
            "FROM ingredients " +
            "WHERE name ILIKE @searchParam " +
            "ORDER BY name LIKE '%sm%' ";


        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@searchParam", $"%{searchParameter}%");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Ingredient tempIngredient = MakeIngredient(reader);
                ingredients.Add(tempIngredient);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting search ingredients: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting search ingredients: {ex.Message}.");
        }

        return (ingredients, "Ok");
    }

    public async Task<(List<Ingredient>? Ingredients, string Message)> GetIngredientsPaginationAsync(int paginationPage)
    {
        List<Ingredient> ingredients = [];

        int offset = ITEMS_PER_PAGE * (paginationPage - 1);

        string query =
            "SELECT " +
            "id," +
            "name," +
            "cals," +
            "fats," +
            "carbs," +
            "protein," +
            "image," +
            "cost_per_100g " +
            "FROM ingredients " +
            "ORDER BY id " +
            $"LIMIT 20 OFFSET {offset} ";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Ingredient tempIngredient = MakeIngredient(reader);
                ingredients.Add(tempIngredient);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting paginated database ingredients: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting paginated database ingredients: {ex.Message}.");
        }

        return (ingredients, "No ingredients found.");
    }

    public async Task<(List<Recipe>? Recipes, string Message)> GetRecipesPaginationAsync(int paginationPage)
    {
        List<Recipe> recipes = [];

        if (paginationPage < 1)
        {
            return (null, "Pagination page is less than 1.");
        }

        int offset = ITEMS_PER_PAGE * (paginationPage - 1);

        string query = "SELECT r.id, " +
                       "r.name, " +
                       "r.meal_type, " +
                       "r.image, " +
                       "r.cost, " +
                       "(r.macros).total_calories, " +
                       "(r.macros).total_carbs, " +
                       "(r.macros).total_fats, " +
                       "(r.macros).total_protein, " +
                       "json_agg(" +
                       "    json_build_object(" +
                       "         'name', i.name," +
                       "         'grams', i.grams," +
                       "         'calories_pr_hectogram', i.calories_pr_hectogram," +
                       "         'fats_pr_hectogram', i.fats_pr_hectogram," +
                       "         'carbs_pr_hectogram', i.carbs_pr_hectogram," +
                       "         'protein_pr_hectogram', i.protein_pr_hectogram," +
                       "         'cost_per_hectogram', COALESCE(i.cost_per_100g, 0)," +
                       "         'multiplier', i.multiplier" +
                       "     )" +
                       ") AS ingredients " +
                       "FROM recipes AS r, unnest(r.ingredients) AS i " +
                       "GROUP BY r.id " +
                       "ORDER BY r.id " +
                       $"LIMIT 20 OFFSET {offset} ";

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Recipe tempRecipe = MakeRecipe(reader);
                recipes.Add(tempRecipe);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error getting recipes: " + e.Message);
            return (null, "Error getting recipes: " + e.Message);
        }

        return (recipes, "Recipes found");
    }

    public async Task<string> DeleteDbIngredientById(int id)
    {
        Console.WriteLine("Deleting database ingredient by name...");
        var statusMessage = $"Ingredient {id} has been deleted.";

        try
        {
            await using var conn = await GetConnectionAsync();
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

    public async Task<string> DeleteRecipeById(int recipeId)
    {
        Console.WriteLine("Deleting recipe by name...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1.");
            return "Recipe was not deleted; recipe ID is less than 1.";
        }

        var statusMessage = $"Ingredient {recipeId} has been deleted.";

        await using var conn = await GetConnectionAsync();

        const string query = "DELETE FROM recipes WHERE id = @recipe_id";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@recipe_id", recipeId);

        try
        {
            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                statusMessage = $"Recipe {recipeId} was not found.";
            else
            {
                await UpdateTableIds("recipes");
                await UpdateTableIds("recipe_instructions");
                var instructionsResult = await UpdateInstructionsRecipeId();
                if (!instructionsResult.status)
                    return instructionsResult.message;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting recipe by id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return statusMessage;
        }

        return statusMessage;
    }

    public async Task<string> DeleteCommonItemByIdAndType(uint itemId, string itemType)
    {
        Console.WriteLine("Deleting common item by id and type...");
        string statusMessage = "Common item got deleted.";

        try
        {
            await using var conn = await GetConnectionAsync();
            const string query = "DELETE FROM sought_after_items WHERE item_id = @id AND type = @type";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", Convert.ToInt32(itemId));
            cmd.Parameters.AddWithValue("@type", itemType);
            int result = await RunAsyncQuery(cmd);
            if (result < 1)
                return "Common item was not found.";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error deleting common item by id and type: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return "Error deleting common item by id and type: " + ex.Message;
        }

        return statusMessage;
    }

    private async Task<(bool status, string message)> UpdateInstructionsRecipeNameByRecipeId(int recipeId,
        string recipeName)
    {
        Console.WriteLine("Updating instructions recipe name by name...");
        var statusMessage = $"Instructions recipe name {recipeId} has been updated.";
        var query = "UPDATE recipe_instructions " +
                    "SET instructions = jsonb_set(instructions::jsonb, '{name}', '\"" +
                    $"{recipeName}" +
                    "\"'::jsonb, false) " +
                    "WHERE recipe_id = @recipe_id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@recipe_id", recipeId);

            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                return (false, $"Instructions recipe name {recipeId} was not found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating instructions recipe name by recipe id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, $"Error updating instructions recipe name by recipe id ({recipeId})");
        }

        return (true, statusMessage);
    }

    public async Task<string> UpdateInstructionsByInstructionsId(RecipeInstructionRecord instructions,
        int instructionsId)
    {
        Console.WriteLine($"Updating instructions by instructions id ({instructionsId})...");

        var statusMessage = $"Recipe ({instructions.GetRecipeId()}) instructions has been updated.";
        var serializedJsonData = JsonSerializer.Serialize(instructions.Instructions);
        const string query = "UPDATE recipe_instructions " +
                             "SET instructions = @json_data " +
                             "WHERE id = @instructions_id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@json_data", NpgsqlTypes.NpgsqlDbType.Json, serializedJsonData);
            cmd.Parameters.AddWithValue("@instructions_id", instructionsId);
            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                statusMessage = $"Instructions ({instructionsId}) was not found.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating instructions by instructions id ({instructionsId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating instructions by instructions id ({instructionsId})" + ex.Message;
        }

        return statusMessage;
    }

    /*public async Task<(bool status, string message)> RecalibrateRecipes()
    {
        Console.WriteLine("Recalibrating recipes...");
        var statusMessage = "Recipes got recalibrated.";

        var result = await GetAllRecipes();
        if (result.Recipes?.Count < 1 || result.Recipes == null)
            return (false, "No recipes found.");

        List<Recipe> recipes = result.Recipes;
        recipes = await RecalibrateRecipesCost(recipes);

        try
        {
            await AddRecipeToDb(recipes[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error recalibrating recipes: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, "Error recalibrating recipes: " + ex.Message);
        }

        return (true, statusMessage);
    }*/

    /*private async Task<List<Recipe>> RecalibrateRecipesCost(List<Recipe> recipes)
    {
        Console.WriteLine("Recalibrating recipes cost...");

        foreach (var recipe in recipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                var result = await GetDbIngredientByName(ingredient.Name);
                if (result.Ingredient == null)
                    throw new Exception("Ingredient not found.");

                ingredient.CostPer100g = result.Ingredient.CostPer100g;
            }

            recipe.SetTotalCost();
        }

        return recipes;
    }*/

    public async Task AddHashedPassword()
    {
        string query = "UPDATE users SET password = @pass WHERE username = 'admin'";
        var passwordHasher = new PasswordHasher<object>();
        var hashedPassword = passwordHasher.HashPassword(null, "Qwerty123");

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@pass", hashedPassword);

            var result = await RunAsyncQuery(cmd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error fucking:" + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
        }
    }

    public async Task<(bool status, string message)> SignUserIn(string username, string password)
    {
        string query = "SELECT * " +
                       "FROM users " +
                       "WHERE username = @user";

        UserAccount userAccount = new();

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@user", username);

            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Get the hashed password from the database
                string hashedPassword = reader.GetString(reader.GetOrdinal("password"));

                // Verify the plain password against the hashed password
                if (!VerifyHashedpassword(hashedPassword, password))
                    return (false, "Password is incorrect.");

                // Authentication passed, set up the user account
                userAccount.Id = reader.GetInt32(reader.GetOrdinal("id"));
                userAccount.Username = reader.GetString(reader.GetOrdinal("username"));
                userAccount.Password = hashedPassword; // (May not be necessary to pass it back)
                userAccount.Role = reader.GetString(reader.GetOrdinal("role"));

                return (true, "User signed in successfully.");
            }
            else
            {
                return (false, "Username does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error signing the user in:" + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, "An error occurred during sign-in.");
        }
    }

    private bool VerifyHashedpassword(string hashedPassword, string plainPassword)
    {
        var passwordHasher = new PasswordHasher<object>();
        PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(null, hashedPassword, plainPassword);
        return result == PasswordVerificationResult.Success;
    }

    /// Updates the recipe_id field in the recipe_instructions table by linking it to the corresponding
    /// entry in the recipes table based on the name field in the instructions JSON object.
    /// <returns>A tuple containing a boolean indicating the success status and a string message detailing the outcome.</returns>
    private async Task<(bool status, string message)> UpdateInstructionsRecipeId()
    {
        Console.WriteLine("Updating instructions recipe id...");
        var statusMessage = "Instructions got updated.";

        // The "->>" operator looks in a JSON file for a specific variable name. In this case, "name".
        string query = "UPDATE recipe_instructions " +
                       "SET recipe_id =" +
                       "(SELECT id FROM recipes WHERE name = instructions ->> 'name')";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);

            var result = await RunAsyncQuery(cmd);
            if (result < 1)
                return (false, "Instructions did not get updated");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating instructions recipe IDs:" + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (false, "Error updating instructions recipe IDs:" + ex.Message);
        }

        return (true, statusMessage);
    }

    /// Determines whether the ingredient's ID is zero.
    /// <param name="ingredient">The ingredient to check.</param>
    /// <returns>True if the ingredient's ID is zero; otherwise, false.</returns>
    private bool IsIngredientIdZero(Ingredient ingredient)
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