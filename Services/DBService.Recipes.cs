using System.Text.Json;
using Npgsql;

namespace WebKitchen.Services;

public partial class DbService
{
    public async Task<string?> AddRecipeToDb(Recipe recipe)
    {
        Console.WriteLine("Adding recipe to database...");

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
                             "@cost) " +
                             "RETURNING id";

        await using NpgsqlConnection conn = await GetConnectionAsync();
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@type", recipe.MealType);
            cmd.Parameters.AddWithValue("@name", recipe.Name);
            cmd.Parameters.AddWithValue("@image", recipe.Base64Image);
            cmd.Parameters.AddWithValue("@calories", recipe.TotalMacros.Calories);
            cmd.Parameters.AddWithValue("@fats", recipe.TotalMacros.Fat);
            cmd.Parameters.AddWithValue("@carbs", recipe.TotalMacros.Carbs);
            cmd.Parameters.AddWithValue("@protein", recipe.TotalMacros.Protein);
            cmd.Parameters.AddWithValue("@cost", recipe.TotalCost);

            var newRecipeId = await cmd.ExecuteScalarAsync();
            if (newRecipeId == null)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("Error adding recipe to database; could not get ID of new recipe");
                return "Error adding recipe to database; could not get ID of new recipe";
            }

            if (!int.TryParse(newRecipeId.ToString(), out var recipeId))
            {
                await transaction.RollbackAsync();
                Console.WriteLine("Error adding recipe to database; could not parse ID of new recipe");
                return "Error retrieving recipe ID.";
            }

            string? ingredientsMsg = await AddIngredientsToRowAsync(recipe.Ingredients, recipeId, conn, transaction);
            if (!string.IsNullOrEmpty(ingredientsMsg))
            {
                await transaction.RollbackAsync();
                return "Error adding ingredients to row; " + ingredientsMsg;
            }

            string thumbnail = await recipe.GetThumbnailBase64Image(recipe.Base64Image);
            string? thumbnailMsg = await AddThumbnail(thumbnail, "recipe", recipeId, conn, transaction);
            if (!string.IsNullOrEmpty(thumbnailMsg))
            {
                await transaction.RollbackAsync();
                return "Error adding thumbnail; " + thumbnailMsg;
            }

            await transaction.CommitAsync();
        }
        catch (TimeoutException ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine("Timeout error adding recipe to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Timeout error adding recipe to database: {ex.Message}.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine("Error adding recipe to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error adding recipe to database: {ex.Message}.";
        }

        return null;
    }

    /// Appends a new row of type 'ingredient' to the array of the recipe, found by counting all the recipes in the database.
    /// The query sent to the database actually gets sent as many times as the length of the ingredient array.
    private async Task<string?> AddIngredientsToRowAsync(List<Ingredient> ingredients, int recipeId,
        NpgsqlConnection conn, NpgsqlTransaction transaction)
    {
        Console.WriteLine("Adding ingredients to row...");

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
                             "@cost_per_100g," +
                             "@is_recipe" +
                             ")::ingredient) " +
                             "WHERE id = @recipe_id"; // Since every new recipe is actually the last
        // one in the table, we can just add all the occurrences of id into one, and that will be our recipe id.

        try
        {
            foreach (var ingredient in ingredients)
            {
                await using var cmd = new NpgsqlCommand(query, conn, transaction);
                cmd.Parameters.AddWithValue("@name", ingredient.Name);
                cmd.Parameters.AddWithValue("@grams", ingredient.Grams);
                cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
                cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                cmd.Parameters.AddWithValue("@cost_per_100g", ingredient.CostPer100g);
                cmd.Parameters.AddWithValue("@is_recipe", ingredient.IsRecipe);
                cmd.Parameters.AddWithValue("@recipe_id", recipeId);
                await RunAsyncQuery(cmd);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding ingredients ({ingredients}) to row: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return "Error adding ingredients to row: " + ex.Message;
        }

        return null;
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
            await AddIngredientsToRowById(updatedRecipe.Ingredients, updatedRecipe.RecipeId);

            return (true, "Recipe updated successfully.");
        }
        catch (Exception ex)
        {
            return (false, $"Error editing recipe: {ex.Message}");
        }
    }

    private async Task<string> UpdateRecipeNameByRecipeId(string updatedName, int recipeId)
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

    private async Task<string> UpdateRecipeMealTypeRecipeIdAsync(string mealType, int recipeId)
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

    private async Task<string> UpdateRecipeMacrosAndIngredientsByRecipeIdAsync(Recipe recipe, int recipeId)
    {
        Console.WriteLine($"Updating recipe macros and ingredients by id ({recipeId})...");

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe ID is less than 1");
            return "Recipe ingredients did not get updated; recipe ID is less than 1";
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!ingredient.IsRecipe)
            {
                ingredient.CostPer100g = await GetIngredientCost(ingredient.Name);
            }
            else
            {
                ingredient.CostPer100g = await GetIngredientRecipesCost(ingredient.Name);
            }
            ingredient.PrintIngredient();
        }

        recipe.TotalCost = recipe.Ingredients.Select(i => i.CostPer100g * i.Multiplier).Sum();

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
                             "@cost_per_100g, " +
                             "@is_recipe" +
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
                cmd.Parameters.AddWithValue("@is_recipe", ingredient.IsRecipe);
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

    private async Task<string> UpdateRecipeImageByRecipeId(string base64Image, int recipeId)
    {
        Console.WriteLine("Updating recipe image by id...");
        Recipe recipeHelper = new();

        if (recipeId < 1)
        {
            Console.WriteLine("Recipe id is below 1.");
            return "Recipe image did not get updated; recipe ID is less than 1.";
        }

        var statusMessage = "Recipe image got updated.";
        const string query = "UPDATE recipes SET image = @base64_image WHERE id = @recipe_id";
        const string thumbnailQuery =
            "UPDATE thumbnails SET image = @base64_image WHERE relation_id = @recipe_id AND relation_type = 'recipe'";
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

            string thumbnailImage = await recipeHelper.GetThumbnailBase64Image(base64Image);
            await using var thumbnailCmd = new NpgsqlCommand(thumbnailQuery, conn);
            thumbnailCmd.Parameters.AddWithValue("@base64_image", thumbnailImage);
            thumbnailCmd.Parameters.AddWithValue("@recipe_id", recipeId);
            result = await RunAsyncQuery(thumbnailCmd);

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

    public async Task<(bool status, string msg)> AddIngredientsToRowById(List<Ingredient> ingredients, int recipeId)
    {
        Console.WriteLine("Adding ingredients to row by id...");

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
                             "@cost_per_100g," +
                             "@is_recipe" +
                             ")::ingredient) " +
                             "WHERE id = @recipeId"; // Since every new recipe is actually the last
        // one in the table, we can just add all the occurrences of id into one, and that will be our recipe id.

        try
        {
            await using var conn = await GetConnectionAsync();
            foreach (var ingredient in ingredients)
            {
                await using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@recipeId", recipeId);
                cmd.Parameters.AddWithValue("@name", ingredient.Name);
                cmd.Parameters.AddWithValue("@grams", ingredient.Grams);
                cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
                cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
                cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
                cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
                cmd.Parameters.AddWithValue("@multiplier", ingredient.GetMultiplier());
                cmd.Parameters.AddWithValue("@cost_per_100g", ingredient.CostPer100g);
                cmd.Parameters.AddWithValue("@is_recipe", ingredient.IsRecipe);
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
                             "WHERE id = @id " +
                             "RETURNING id";

        await using var conn = await GetConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand(query, conn, transaction);

        try
        {
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

            var ingredientId = await cmd.ExecuteScalarAsync();
            if (ingredientId == null)
            {
                Console.WriteLine("Error adding recipe to database; could not get ID of new recipe");
                return "Error adding recipe to database; could not get ID of new recipe";
            }

            if (!int.TryParse(ingredientId.ToString(), out var recipeId))
            {
                Console.WriteLine("Error adding recipe to database; could not parse ID of new recipe");
                return "Error retrieving recipe ID.";
            }

            Recipe thumbnailHelper = new();
            string thumbnail = await thumbnailHelper.GetThumbnailBase64Image(ingredient.Base64Image);
            string? thumbnailMsg = await UpdateThumbnail(thumbnail, "ingredient", recipeId, conn, transaction);
            if (!string.IsNullOrEmpty(thumbnailMsg))
            {
                return "Error adding thumbnail; " + thumbnailMsg;
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating database ingredient ({ingredient}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating database ingredient ({ingredient}): " + ex.Message;
        }

        return statusMessage;
    }

    public async Task<(Ingredient? ingredient, string message)> GetIngredientByNameSearch(string name)
    {
        Ingredient? ingredient;

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
            "WHERE name = @ingredientName";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ingredientName", name);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Ingredient tempIngredient = MakeIngredient(reader);
                ingredient = tempIngredient;
            }
            else
            {
                return (null, "Ingredient not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting search ingredients: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting search ingredients: {ex.Message}.");
        }

        return (ingredient, "Ok");
    }

    public async Task<(List<string>? recipeNames, string message)> GetRecipesByNameSearch(string name)
    {
        List<string> recipeNames = [];

        string query =
            "SELECT " +
            "name " +
            "FROM recipes " +
            "WHERE name ILIKE @searchParam " +
            "ORDER BY name ILIKE @searchParamPiority DESC, " +
            "name ILIKE @searchParam DESC, " +
            "name";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@searchParam", $"%{name}%");
            cmd.Parameters.AddWithValue("@searchParamPiority", $"{name}%");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string tempRecipe = reader.GetString(0);
                recipeNames.Add(tempRecipe);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting search recipes: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting search recipes: {ex.Message}.");
        }

        return (recipeNames, "Ok");
    }

    public async Task<(Recipe? Recipe, string Message)> GetRecipeByName(string recipeName)
    {
        Console.WriteLine("Getting recipe by name...");

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
                             "         'multiplier', i.multiplier," +
                             "         'is_recipe', COALESCE(i.is_recipe, false)" +
                             "     )" +
                             ") AS ingredients " +
                             "FROM recipes AS r, unnest(r.ingredients) AS i " +
                             "WHERE r.name = @recipeName " +
                             "GROUP BY r.id " +
                             "ORDER BY r.id ";
        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@recipeName", recipeName);
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
            Console.WriteLine($"Error getting recipe by name ({recipeName}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting recipe by name ({recipeName}): {ex.Message}.");
        }

        return (recipe, "Recipe successfully retrieved");
    }

    public async Task<(List<Recipe>? Recipes, string Message)> GetRecipesPaginatedSearchAsync(string search,
        List<string> mealTypes, int paginationPage)
    {
        List<Recipe> recipes = [];

        if (paginationPage < 1)
        {
            return (null, "Pagination page is less than 1.");
        }

        int offset = ITEMS_PER_PAGE * (paginationPage - 1);

        string baseQuery = "SELECT r.id, " +
                           "r.name, " +
                           "r.meal_type, " +
                           "t.image, " +
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
                           "         'multiplier', i.multiplier," +
                           "         'is_recipe', COALESCE(i.is_recipe, false)" +
                           "     )" +
                           ") AS ingredients " +
                           "FROM recipes AS r " +
                           "LEFT JOIN LATERAL unnest(r.ingredients) AS i ON TRUE " +
                           "LEFT JOIN thumbnails AS t ON t.relation_id = r.id AND t.relation_type = 'recipe' ";

        List<string> whereConditions = [];
        string orderByClause = "GROUP BY r.id, t.image ORDER BY r.id ";
        if (search != "")
        {
            whereConditions.Add("r.name ILIKE @searchParam");
            orderByClause =
                "GROUP BY r.id, t.image ORDER BY r.name ILIKE @searchParamPriority DESC, r.name ILIKE @searchParam DESC ";
        }

        if (mealTypes.Count != 0)
        {
            whereConditions.Add("r.meal_type = ANY(@mealTypesParam)");
        }

        string whereClause = whereConditions.Any()
            ? $"WHERE {string.Join(" AND ", whereConditions)} "
            : "";

        string query = baseQuery + whereClause + orderByClause + $"LIMIT {ITEMS_PER_PAGE} OFFSET {offset}";
        MaxRecipesPages =
            (int)Math.Ceiling((double)await GetRecipeQueryCount(whereClause, search, mealTypes) /
                              ITEMS_PER_PAGE);
        Console.WriteLine($"Max recipes pages: {MaxRecipesPages}");

        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            if (mealTypes.Count > 0)
            {
                cmd.Parameters.AddWithValue("@mealTypesParam", mealTypes.ToArray());
            }

            if (search != "")
            {
                cmd.Parameters.AddWithValue("@searchParam", $"%{search}%");
                cmd.Parameters.AddWithValue("@searchParamPriority", $"{search}%");
            }

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

    private async Task<int> GetRecipeQueryCount(string whereClause, string search, List<string> mealTypes)
    {
        try
        {
            string query = "SELECT COUNT(*) FROM recipes AS r " + whereClause;
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            if (mealTypes.Count > 0)
            {
                cmd.Parameters.AddWithValue("@mealTypesParam", mealTypes.ToArray());
            }

            if (search != "")
            {
                cmd.Parameters.AddWithValue("@searchParam", $"%{search}%");
                cmd.Parameters.AddWithValue("@searchParamPriority", $"{search}%");
            }

            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine("Amount of rows for search: " + reader.GetInt32(0));
                return reader.GetInt32(0);
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<(Recipe? Recipe, string Message)> GetRecipeByIdAsync(int recipeId, CancellationToken ct = new())
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
                             "COALESCE(" +
                             "json_agg(" +
                             "    json_build_object(" +
                             "         'name', i.name," +
                             "         'grams', i.grams," +
                             "         'calories_pr_hectogram', i.calories_pr_hectogram," +
                             "         'fats_pr_hectogram', i.fats_pr_hectogram," +
                             "         'carbs_pr_hectogram', i.carbs_pr_hectogram," +
                             "         'protein_pr_hectogram', i.protein_pr_hectogram," +
                             "         'cost_per_hectogram', COALESCE(i.cost_per_100g, 0)," +
                             "         'multiplier', i.multiplier," +
                             "         'is_recipe', COALESCE(i.is_recipe, false)" +
                             "     )" +
                             ") FILTER (WHERE i.name IS NOT NULL)," +
                             "'[]'" +
                             ") AS ingredients " +
                             "FROM recipes AS r LEFT JOIN LATERAL unnest(r.ingredients) AS i ON true " +
                             "WHERE r.id = @id " +
                             "GROUP BY r.id " +
                             "ORDER BY r.id ";
        try
        {
            await using NpgsqlConnection conn = await GetConnectionAsync();
            await using NpgsqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@id", recipeId);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
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

    /// Asynchronously retrieves a random recipe from the collection of dinner recipes available in the database.
    /// This method fetches all dinner recipes, selects one randomly, and returns it as a single recipe object.
    /// <returns>An instance of Recipe that represents a random dinner recipe.</returns>
    public async Task<Recipe> GetRandomRecipe()
    {
        Console.WriteLine("Getting random recipe...");
        var result = await GetDinnerRecipes();
        if (result.Recipe == null)
        {
            Console.WriteLine("No dinner recipes found.");
            throw new Exception("No dinner recipes found.");
        }

        return result.Recipe;
    }

    /// Retrieves a list of dinner recipes from the database, including their details and nutritional information.
    /// If a recipe does not have a specific image, a placeholder image is substituted.
    /// <returns>A tuple containing a list of Recipe objects and a status message indicating the result of the operation.</returns>
    private async Task<(Recipe? Recipe, string Message)> GetDinnerRecipes()
    {
        Console.WriteLine("Getting dinner recipes...");

        Recipe? recipe = null;
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
                             "         'multiplier', i.multiplier," +
                             "         'is_recipe', COALESCE(i.is_recipe, false)" +
                             "     )" +
                             ") AS ingredients " +
                             "FROM recipes AS r, unnest(r.ingredients) AS i " +
                             "WHERE r.meal_type = 'D' " +
                             "GROUP BY r.id " +
                             "ORDER BY RANDOM() " +
                             "LIMIT 1";

        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            recipe = MakeRecipe(reader);
        }

        return (recipe, "Recipes successfully retrieved");
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
                // await UpdateTableIds("recipes");
                // await UpdateTableIds("recipe_instructions");
                // var instructionsResult = await UpdateInstructionsRecipeId();
                // if (!instructionsResult.status)
                // return instructionsResult.message;
            }

            string? deleteResults = await DeleteRecipeCleanUp(recipeId);
            if (!string.IsNullOrEmpty(deleteResults))
                return deleteResults;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting recipe by id ({recipeId}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return statusMessage;
        }

        return statusMessage;
    }

    private async Task<string?> DeleteRecipeCleanUp(int id)
    {
        Console.WriteLine("Deleting recipe clean up...");

        // const string recipeInstructionsQuery = "DELETE FROM recipe_ingredients WHERE recipe_id = @recipe_id";
        const string thumbnailQuery = "DELETE FROM thumbnails WHERE relation_id = @recipe_id";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(thumbnailQuery, conn);
            cmd.Parameters.AddWithValue("@recipe_id", id);
            await RunAsyncQuery(cmd);

            /*cmd.Parameters.AddWithValue("@recipe_id", id);
            if (await RunAsyncQuery(cmd) < 1)
                return "Recipe clean up failed.";*/
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting recipe clean up ({id}): " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return "Error deleting recipe clean up: " + ex.Message;
        }

        return null;
    }

    /// <param name="ingredient">The ingredient to check.</param>
    /// <returns>True if the ingredient's ID is zero; otherwise, false.</returns>
    private bool IsIngredientIdZero(Ingredient ingredient)
    {
        Console.WriteLine("Checking if ingredient ID is 0...");
        if (ingredient.GetId() != 0) return false;
        Console.WriteLine("Ingredient ID is 0");
        return true;
    }

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
}