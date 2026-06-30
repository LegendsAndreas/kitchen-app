using Npgsql;

namespace WebKitchen.Services;

public partial class DbService
{
    public async Task<(Stats? stats, string msg)> GetBaseStats()
    {
        Stats? stats;

        string query = "SELECT " +
                       "(SELECT count(*) FROM recipes) AS total_recipes," +
                       "(SELECT count(*) FROM ingredients) AS total_ingreidents," +
                       "(SELECT count(*) FROM recipe_instructions) AS total_instructions," +
                       "(SELECT count(*) FROM recipes WHERE meal_type = 'B') AS total_breakfast_recipes," +
                       "(SELECT count(*) FROM recipes WHERE meal_type = 'L') AS total_lunch_recipes," +
                       "(SELECT count(*) FROM recipes WHERE meal_type = 'D') AS total_dinner_recipes," +
                       "(SELECT count(*) FROM recipes WHERE meal_type = 'S') AS total_side_recipes," +
                       "(SELECT count(*) FROM recipes WHERE meal_type = 'K') AS total_snack_recipes";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                stats = new()
                {
                    TotalRecipes = reader.GetInt32(0),
                    TotalIngredients = reader.GetInt32(1),
                    TotalInstructions = reader.GetInt32(2),
                    TotalBreakfasts = reader.GetInt32(3),
                    TotalLunches = reader.GetInt32(4),
                    TotalDinners = reader.GetInt32(5),
                    TotalSides = reader.GetInt32(6),
                    TotalSnacks = reader.GetInt32(7),
                };
            }
            else
            {
                return (null, "No stats found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return (null, "Error getting stats.");
        }

        return (stats, "Ok");
    }

    public async Task<(Dictionary<int, string>? stats, string msg)> GetInstructionsStats()
    {
        Dictionary<int, string> stats = new();

        string instructionsQuery = "SELECT r.id, r.name " +
                                   "FROM recipes AS r " +
                                   "LEFT JOIN recipe_instructions ri ON r.id = ri.recipe_id " +
                                   "WHERE ri.recipe_id IS NULL " +
                                   "ORDER BY r.id ";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(instructionsQuery, conn);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                stats.Add(reader.GetInt32(0), reader.GetString(1));
            }

            if (stats.Count == 0)
                return (null, "No instructions found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return (null, "Error getting stats.");
        }

        return (stats, "Ok");
    }

    public async Task<(Dictionary<int, string>? stats, string msg)> GetPlaceholderStats(string base64Image,
        bool recipes)
    {
        Dictionary<int, string> stats = new();

        string instructionsQuery;

        if (recipes)
            instructionsQuery = "SELECT id, name " +
                                "FROM recipes " +
                                "WHERE image = @image";
        else
            instructionsQuery = "SELECT id, name " +
                                "FROM ingredients " +
                                "WHERE image = @image";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(instructionsQuery, conn);
            cmd.Parameters.AddWithValue("@image", base64Image);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                stats.Add(reader.GetInt32(0), reader.GetString(1));
            }

            if (stats.Count == 0)
                return ([], "No instructions found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return (null, "Error getting stats.");
        }

        return (stats, "Ok");
    }
}