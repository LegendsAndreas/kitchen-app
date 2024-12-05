using System.ComponentModel.DataAnnotations;
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
        Console.WriteLine("Printing Macros:");
        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine("Total calories: " + Calories);
        Console.WriteLine("Total fats: " + Fat);
        Console.WriteLine("Total carbs: " + Carbs);
        Console.WriteLine("Total protein: " + Protein);
    }
}

public class Ingredient
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public float Grams { get; set; }
    public float Calories { get; set; }
    public float Carbs { get; set; }
    public float Protein { get; set; }
    public float Fats { get; set; }
    public float Multiplier { get; set; }
    public string Image { get; set; } = string.Empty;

    public void Clear()
    {
        Name = string.Empty;
        Grams = 0f;
        Calories = 0f;
        Carbs = 0f;
        Protein = 0f;
        Fats = 0f;
        Multiplier = 0f;
        Image = string.Empty;
    }

    public void SetMultiplier()
    {
        Multiplier = Grams / 100;
    }

    public void PrintIngredient()
    {
        Console.WriteLine("Printing Ingredient___");
        Console.WriteLine("Ingredient: " + Name);
        Console.WriteLine("Grams: " + Grams);
        Console.WriteLine("Calories: " + Calories);
        Console.WriteLine("Carbs: " + Carbs);
        Console.WriteLine("Protein: " + Protein);
        Console.WriteLine("Fats: " + Fats);
        Console.WriteLine("Multiplier: " + Multiplier);
    }

    public Ingredient TransferIngredient(Ingredient ing)
    {
        // We must create a new instance of Ingredient, where we then SPECIFICALLY assign its values to be equal TO THE VALUE
        // of the variables of CurrentIngredient. Otherwise, we will just be setting tempIngredient to point at CurrentIngredient,
        // which means that if CurrentIngredient changes, so does the ingredient that we insert into the list.

        Ingredient transferIngredient = new()
        {
            Name = ing.Name,
            Grams = ing.Grams,
            Calories = ing.Calories,
            Carbs = ing.Carbs,
            Fats = ing.Fats,
            Protein = ing.Protein,
            Image = ing.Image
        };
        transferIngredient.SetMultiplier();

        return transferIngredient;
    }
}

public class Recipe
{
    public int Number;
    [Required] public string MealType { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Image { get; set; } = string.Empty;
    public List<Ingredient> Ingredients { get; set; } = new();
    public Macros TotalMacros { get; set; } = new();

    public void PrintRecipe()
    {
        Console.WriteLine("Reciping recipe...");
        Console.WriteLine("Recipe Number: " + Number);
        Console.WriteLine("MealType: " + MealType);
        Console.WriteLine("Name: " + Name);
        Console.WriteLine("Image: " + Image);

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

    public void SetTotalMacros()
    {
        if (Ingredients.Count == 0)
        {
            Console.WriteLine("No ingredients available.");
        }
        else
        {
            foreach (var ingredient in Ingredients)
            {
                Console.WriteLine("Printing ingredient at Macrows");
                ingredient.PrintIngredient();
                TotalMacros.Calories += ingredient.Calories * ingredient.Multiplier;
                TotalMacros.Fat += ingredient.Fats * ingredient.Multiplier;
                TotalMacros.Carbs += ingredient.Carbs * ingredient.Multiplier;
                TotalMacros.Protein += ingredient.Protein * ingredient.Multiplier;
            }
        }

        Console.WriteLine("At SetTotalMacros()");
        TotalMacros.PrintMacros();
    }

    public void Clear()
    {
        Number = 0;
        MealType = String.Empty;
        Name = String.Empty;
        Image = String.Empty;
        Ingredients = new();
        TotalMacros = new();
    }
}