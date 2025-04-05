using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using Npgsql;

namespace WebKitchen.Services;

public class Macros
{
    public float Calories { get; set; }
    public float Fat { get; set; }
    public float Carbs { get; set; }
    public float Protein { get; set; }

    public void PrintMacros()
    {
        Console.WriteLine("__Printing Macros__");
        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine("Total calories: " + Calories);
        Console.WriteLine("Total fats: " + Fat);
        Console.WriteLine("Total carbs: " + Carbs);
        Console.WriteLine("Total protein: " + Protein);
    }

    public void SetMacros(List<Ingredient> ingredients)
    {
        Console.WriteLine("Setting macros...");
        if (ingredients.Count == 0)
        {
            Console.WriteLine("No ingredients available.");
        }
        else
        {
            Calories = 0f;
            Fat = 0f;
            Carbs = 0f;
            Protein = 0f;
            foreach (var ingredient in ingredients)
            {
                Console.WriteLine("Printing ingredient at Macros");
                ingredient.PrintIngredient();
                Calories += ingredient.CaloriesPer100g * ingredient.GetMultiplier();
                Fat += ingredient.FatPer100g * ingredient.GetMultiplier();
                Carbs += ingredient.CarbsPer100g * ingredient.GetMultiplier();
                Protein += ingredient.ProteinPer100g * ingredient.GetMultiplier();
            }
        }

        PrintMacros();
    }

    public Macros TransferMacros(Macros macros)
    {
        Macros tempMacros = new()
        {
            Calories = macros.Calories,
            Fat = macros.Fat,
            Carbs = macros.Carbs,
            Protein = macros.Protein
        };
        return tempMacros;
    }
}

public class Ingredient
{
    private uint Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public float Grams { get; set; }
    public float CaloriesPer100g { get; set; }
    public float CarbsPer100g { get; set; }
    public float ProteinPer100g { get; set; }
    public float FatPer100g { get; set; }
    private float Multiplier { get; set; }
    public string Base64Image { get; set; } = string.Empty;

    public uint GetId()
    {
        Console.WriteLine("Getting ID...");
        return Id;
    }

    public int GetIntId()
    {
        Console.WriteLine("Getting converted ID...");
        return (int)Id;
    }

    public float GetMultiplier()
    {
        Console.WriteLine("Getting multiplier...");
        return Multiplier;
    }

    public string GetIngredientImage()
    {
        Console.WriteLine("Getting ingredient image...");
        return Base64Image;
    }

    public void SetId(uint id)
    {
        Console.WriteLine("Setting ID...");
        Id = id;
    }


    public void SetMultiplier()
    {
        Console.WriteLine("Setting multiplier...");
        Multiplier = Grams / 100;
    }

    public async Task SetIngredientImage(IBrowserFile image, long allowedFileSize = 10)
    {
        SharedStuff helperStuff = new();
        Base64Image = await helperStuff.SetImage(image, allowedFileSize);
    }

    public void ClearIngredient()
    {
        Console.WriteLine("Clearing ingredient...");
        Id = 0;
        Name = string.Empty;
        Grams = 0f;
        CaloriesPer100g = 0f;
        CarbsPer100g = 0f;
        ProteinPer100g = 0f;
        FatPer100g = 0f;
        Multiplier = 0f;
        Base64Image = string.Empty;
    }

    public void PrintIngredient()
    {
        Console.WriteLine("__Printing Ingredient__");
        Console.WriteLine("ID: " + Id);
        Console.WriteLine("Ingredient: " + Name);
        Console.WriteLine("Grams: " + Grams);
        Console.WriteLine("Calories: " + CaloriesPer100g);
        Console.WriteLine("Carbs: " + CarbsPer100g);
        Console.WriteLine("Protein: " + ProteinPer100g);
        Console.WriteLine("Fats: " + FatPer100g);
        Console.WriteLine("Multiplier: " + Multiplier);
    }

    public Ingredient TransferIngredient(Ingredient ing)
    {
        Console.WriteLine("Transferring ingredient...");
        // We must create a new instance of Ingredient, where we then SPECIFICALLY assign its values to be equal TO THE VALUE
        // of the variables of CurrentIngredient. Otherwise, we will just be setting tempIngredient to point at CurrentIngredient,
        // which means that if CurrentIngredient changes, so does the ingredient that we insert into the list.
        Ingredient transferIngredient = new()
        {
            Name = ing.Name,
            Grams = ing.Grams,
            CaloriesPer100g = ing.CaloriesPer100g,
            CarbsPer100g = ing.CarbsPer100g,
            FatPer100g = ing.FatPer100g,
            ProteinPer100g = ing.ProteinPer100g,
            Base64Image = ing.Base64Image
        };
        transferIngredient.SetId(ing.GetId());
        transferIngredient.SetMultiplier();

        return transferIngredient;
    }
}

public class Recipe
{
    public int RecipeId;

    [Required]
    [StringLength(1, ErrorMessage = "Meal type must be one character long.")]
    public string MealType { get; set; } = string.Empty;

    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Base64Image { get; set; } = string.Empty;
    public List<Ingredient> Ingredients { get; set; } = new();
    public Macros TotalMacros { get; set; } = new();

    public void PrintRecipe()
    {
        Console.WriteLine("Printing recipe...");
        try
        {
            Console.WriteLine("Recipe Number: " + RecipeId);
            Console.WriteLine("MealType: " + MealType);
            Console.WriteLine("Name: " + Name);
            // Console.WriteLine("Image: " + Base64Image);

            if (Ingredients.Count != 0)
            {
                foreach (var ingredient in Ingredients)
                {
                    ingredient.PrintIngredient();
                }
            }
            else
            {
                Console.WriteLine("No ingredients available.");
            }

            TotalMacros.PrintMacros();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error printing recipe: " + ex.Message);
            Console.WriteLine("StackTrace: " + ex.StackTrace);
            return;
        }
    }

    public Recipe TransferRecipe(Recipe recipe)
    {
        Recipe tempRecipe = new()
        {
            RecipeId = recipe.RecipeId,
            MealType = recipe.MealType,
            Name = recipe.Name,
            Base64Image = recipe.Base64Image,
        };

        foreach (var ingredient in recipe.Ingredients)
        {
            Ingredient tempIngredient = ingredient.TransferIngredient(ingredient);
            tempRecipe.Ingredients.Add(tempIngredient);
        }

        tempRecipe.TotalMacros = tempRecipe.TotalMacros.TransferMacros(recipe.TotalMacros);

        return tempRecipe;
    }

    public void PrintImage()
    {
        Console.WriteLine("Printing image...");
        Console.WriteLine("Image: " + Base64Image);
    }

    public async Task SetRecipeImage(IBrowserFile image, long allowedFileSize = 10)
    {
        var helperStuff = new SharedStuff();
        Base64Image = await helperStuff.SetImage(image, allowedFileSize);
    }

    public void SetTotalMacros()
    {
        Console.WriteLine("Setting total macros...");
        TotalMacros.SetMacros(Ingredients);
    }

    public void ClearRecipe()
    {
        Console.WriteLine("Clearing recipe...");
        RecipeId = 0;
        MealType = string.Empty;
        Name = string.Empty;
        Base64Image = string.Empty;
        Ingredients = [];
        TotalMacros = new Macros();
    }
}

public class SharedStuff
{
    public async Task<string> SetImage(IBrowserFile image, long allowedFileSize)
    {
        Console.WriteLine("Setting recipe image...");
        long maxFileSize = allowedFileSize * 1024 * 1024;

        if (image.Size > maxFileSize)
        {
            Console.WriteLine("Image too large.");
            return "";
        }

        using var memoryStream = new MemoryStream();
        await image.OpenReadStream(maxFileSize).CopyToAsync(memoryStream);

        byte[] imageBytes = memoryStream.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        return base64Image;
    }
}

public class SharedRecipe
{
    public Recipe SelectedRecipe { get; set; }

    public void SetSelectedRecipe(Recipe recipe) => SelectedRecipe = recipe;
}