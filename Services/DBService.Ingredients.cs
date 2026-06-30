using Npgsql;

namespace WebKitchen.Services;

public partial class DbService
{
    
    public async Task<string> AddIngredientToDbAsync(Ingredient ingredient)
    {
        Console.WriteLine("Adding ingredient to database...");

        string statusMessage = "Ingredient successfully added.";

        const string query =
            "INSERT INTO ingredients " +
            "(name, cals, fats, carbs, protein, image, cost_per_100g) " +
            "VALUES(@name, @cals, @fats, @carbs, @protein, @image, @costper100g) " +
            "RETURNING id";

        await using var conn = await GetConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@name", ingredient.Name);
            cmd.Parameters.AddWithValue("@cals", ingredient.CaloriesPer100g);
            cmd.Parameters.AddWithValue("@fats", ingredient.FatPer100g);
            cmd.Parameters.AddWithValue("@carbs", ingredient.CarbsPer100g);
            cmd.Parameters.AddWithValue("@protein", ingredient.ProteinPer100g);
            cmd.Parameters.AddWithValue("@image", ingredient.Base64Image);
            cmd.Parameters.AddWithValue("@costper100g", ingredient.GetCostPer100g());

            var newIngredientId = await cmd.ExecuteScalarAsync();
            if (newIngredientId == null)
            {
                Console.WriteLine("Error adding ingredient to database; could not get ID of new ingredient");
                return "Error adding ingredient to database; could not get ID of new ingredient";
            }

            if (!int.TryParse(newIngredientId.ToString(), out var ingredientId))
            {
                Console.WriteLine("Error adding recipe to database; could not parse ID of new ingredient");
                return "Error retrieving ingredient ID.";
            }

            Recipe thumbnailHelper = new();
            string thumbnail = await thumbnailHelper.GetThumbnailBase64Image(ingredient.Base64Image);

            string? thumbnailMsg = await AddThumbnail(thumbnail, "ingredient", ingredientId, conn, transaction);
            if (!string.IsNullOrEmpty(thumbnailMsg))
            {
                return "Error adding thumbnail; " + thumbnailMsg;
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error adding ingredients to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return "Error adding ingredients to database: " + ex.Message;
        }

        return statusMessage;
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

    private async Task<float> GetIngredientCost(string name)
    {
        const string query = "SELECT cost_per_100g FROM ingredients WHERE name = @name";
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", name);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetFloat(0);
        }
        else
        {
            return 0;
        }
    }
    public async Task<(List<string>? Ingredients, string Message)> GetIngredientsByNameSearch(string ingredientName,
        CancellationToken ct = default)
    {
        List<string> ingredientsName = [];

        string query =
            "SELECT " +
            "name " +
            "FROM ingredients " +
            "WHERE name ILIKE @searchParam " +
            "ORDER BY name ILIKE @searchParamPiority DESC, " +
            "name ILIKE @searchParam DESC, " +
            "name";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@searchParam", $"%{ingredientName}%");
            cmd.Parameters.AddWithValue("@searchParamPiority", $"{ingredientName}%");
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync())
            {
                string tempIngredientName = reader.GetString(0);
                ingredientsName.Add(tempIngredientName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting search ingredients: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return (null, $"Error getting search ingredients: {ex.Message}.");
        }

        return (ingredientsName, "Ok");
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
            "ORDER BY name ILIKE @searchParamPiority DESC, " +
            "name ILIKE @searchParam DESC, " +
            "name";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@searchParam", $"%{searchParameter}%");
            cmd.Parameters.AddWithValue("@searchParamPiority", $"{searchParameter}%");
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
            "i.id," +
            "i.name," +
            "i.cals," +
            "i.fats," +
            "i.carbs," +
            "i.protein," +
            "t.image," +
            "i.cost_per_100g " +
            "FROM ingredients i " +
            "LEFT JOIN thumbnails t ON t.relation_id = i.id AND t.relation_type = 'ingredient' " +
            "ORDER BY i.id, t.image " +
            $"LIMIT {ITEMS_PER_PAGE} OFFSET {offset} ";

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
    
    public async Task<string?> DeleteDbIngredientById(int id)
    {
        Console.WriteLine("Deleting database ingredient by name...");

        await using var conn = await GetConnectionAsync();
        /*Adding the "using" keyword also automatically rolls back a failed transaction, so you dont need to manually
        use "transaction.Rollback();"*/
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            const string query = "DELETE FROM ingredients WHERE id = @id";
            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@id", id);

            int result = await RunAsyncQuery(cmd);
            if (result < 1)
            {
                Console.WriteLine($"Ingredient ID ({id}) is not found in database.");
                return $"Ingredient ID ({id}) is not found in database.";
            }

            string? deleteCleanUpResults = await DeleteIngredientCleanUp(id, conn, transaction);
            if (!string.IsNullOrEmpty(deleteCleanUpResults))
                return deleteCleanUpResults;

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            // Also not a good idea to put the Rollback inside a catch statement, because Rollback can cause an exception
            // await transaction.RollbackAsync();
            Console.WriteLine($"Error deleting database ingredient by id ({id})" + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error deleting database ingredient by id ({id}): " + ex.Message;
        }

        return null;
    }
    
    private async Task<string?> DeleteIngredientCleanUp(int id, NpgsqlConnection conn, NpgsqlTransaction transaction)
    {
        // throw new Exception("Piss");
        Console.WriteLine("Cleaning up delete database ingredient...");

        const string query = "DELETE FROM thumbnails WHERE relation_id = @id AND relation_type = 'ingredient'";

        await using var cmd = new NpgsqlCommand(query, conn, transaction);
        cmd.Parameters.AddWithValue("@id", id);
        int result = await RunAsyncQuery(cmd);
        if (result < 1)
        {
            Console.WriteLine($"Error deleting database ingredient by id ({id}); could not delete thumbnails");
            return $"Error deleting database ingredient by id ({id}); could not delete thumbnails";
        }

        return null;
    }
}