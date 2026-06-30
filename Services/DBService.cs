using System.Text.Json;
using Npgsql;

// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]
// jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&user=[USERNAME]
// Remember that a connection string largely opperates on regex logic, so it does not matter too much where you put your variables.
// So, if you don't need sslmode, you can just delete it entirely.
// jdbc:postgresql://ep-steep-rice-a2ieai9c.eu-central-1.aws.neon.tech/neondb?sslmode=require&user=neondb_owner&password=<PASSTA>

namespace WebKitchen.Services;

public partial class DbService
{
    private int _totalRecipes;
    public int MaxRecipesPages;
    private int _totalIngredients;
    public int MaxIngredientsPages;
    private readonly string _connectionString;
    private const int ITEMS_PER_PAGE = 20;

    public DbService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SetTotalVariables()
    {
        _totalRecipes = await GetTotalRecipes();
        _totalIngredients = await GetTotalIngredients();
        Console.WriteLine($"Total Recipes: {_totalRecipes}");
        Console.WriteLine($"Total Ingredients: {_totalIngredients}");
        MaxRecipesPages = (int)Math.Ceiling((double)_totalRecipes / ITEMS_PER_PAGE);
        MaxIngredientsPages = (int)Math.Ceiling((double)_totalIngredients / ITEMS_PER_PAGE);
        Console.WriteLine($"Max Recipes Pages: {MaxRecipesPages}");
        Console.WriteLine($"Max Ingredients Pages: {MaxIngredientsPages}");
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

    private async Task<string?> AddThumbnail(string base64Image, string relation, int id, NpgsqlConnection conn,
        NpgsqlTransaction transaction)
    {
        Console.WriteLine("Adding thumbnail to database...");

        const string query =
            "INSERT INTO thumbnails (image, relation_id, relation_type) VALUES (@image, @relation_id,@relation_type)";

        try
        {
            NpgsqlCommand cmd = new(query, conn, transaction);
            cmd.Parameters.AddWithValue("@image", base64Image);
            cmd.Parameters.AddWithValue("@relation_id", id);
            cmd.Parameters.AddWithValue("@relation_type", relation);
            if (await RunAsyncQuery(cmd) < 1)
                return "Error adding thumbnail to database.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding thumbnail to database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error adding thumbnail to database: {ex.Message}.";
        }

        return null;
    }

    private async Task<string?> UpdateThumbnail(string base64Image, string relation, int id, NpgsqlConnection conn,
        NpgsqlTransaction transaction)
    {
        Console.WriteLine("Updating thumbnail in database...");

        const string query =
            "UPDATE thumbnails SET image = @image WHERE relation_id = @relation_id AND relation_type = @relation_type";

        try
        {
            NpgsqlCommand cmd = new(query, conn, transaction);
            cmd.Parameters.AddWithValue("@image", base64Image);
            cmd.Parameters.AddWithValue("@relation_id", id);
            cmd.Parameters.AddWithValue("@relation_type", relation);
            if (await RunAsyncQuery(cmd) < 1)
                return "Error updating thumbnail in database.";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error updating thumbnail in database: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return $"Error updating thumbnail in database: {ex.Message}.";
        }

        return null;
    }

    public async Task<IdImageRecipe?> InitThumbnailsRecipes()
    {
        const string query = "SELECT id, image FROM recipes ORDER BY id OFFSET 0";

        List<IdImageRecipe> idImageRecipes = new();
        Recipe helper = new();

        await using NpgsqlConnection conn = await GetConnectionAsync();
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn, transaction);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                idImageRecipes.Add(new IdImageRecipe
                {
                    Id = reader.GetInt32(0),
                    Base64Image = reader.GetString(1)
                });
            }

            await reader.CloseAsync();

            int counter = 1;
            foreach (var idImageRecipe in idImageRecipes)
            {
                Console.WriteLine($"Adding thumbnail for recipe {counter++} out of {idImageRecipes.Count}...");
                string thumbnail = await helper.GetThumbnailBase64Image(idImageRecipe.Base64Image);
                await AddThumbnail(thumbnail, "recipe", idImageRecipe.Id, conn, transaction);
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine(ex);
            throw;
        }

        return null;
    }

    public async Task<IdImageRecipe?> InitThumbnailsIngredients()
    {
        const string query = "SELECT id, image FROM ingredients ORDER BY id";

        List<IdImageRecipe> idImageRecipes = new();
        Recipe helper = new();

        await using NpgsqlConnection conn = await GetConnectionAsync();
        await using NpgsqlTransaction transaction = await conn.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(query, conn, transaction);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                idImageRecipes.Add(new IdImageRecipe
                {
                    Id = reader.GetInt32(0),
                    Base64Image = reader.GetString(1)
                });
            }

            await reader.CloseAsync();

            int counter = 1;
            foreach (var idImageRecipe in idImageRecipes)
            {
                Console.WriteLine($"Adding thumbnail for recipe {counter++} out of {idImageRecipes.Count}...");
                string thumbnail = await helper.GetThumbnailBase64Image(idImageRecipe.Base64Image);
                await AddThumbnail(thumbnail, "ingredient", idImageRecipe.Id, conn, transaction);
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine(ex);
            throw;
        }

        return null;
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

    /// Executes the provided SQL query asynchronously and processes the number of affected rows.
    /// <param name="cmd">An instance of NpgsqlCommand representing the query to be executed.</param>
    /// <return>An integer representing the number of rows affected by the query.</return>
    private async Task<int> RunAsyncQuery(NpgsqlCommand cmd)
    {
        int result = await cmd.ExecuteNonQueryAsync();
        if (result <= 0)
            Console.WriteLine("No records affected.");
        else
            Console.WriteLine("Records affected: " + result);
        return result;
    }
}