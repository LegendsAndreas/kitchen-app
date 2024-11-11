namespace WebKitchen;

public class Macros
{
    public float TotalCalories;
    public float TotalFats;
    public float TotalCarbs;
    public float TotalProtein;
}

public class Ingredient
{
    public string Name;
    public float Kg;
    public float Calories;
    public float Carbs;
    public float Protein;
    public float Fats;
    private float _multiplier;
    
    public void ClearIngredient()
    {
        Name = string.Empty;
        Kg = 0f;
        Calories = 0f;
        Carbs = 0f;
        Protein = 0f;
        Fats = 0f;
        _multiplier = 0f;
    }

    public void SetMultiplier()
    {
        _multiplier = Kg / 100;
    }
}

public class Recipe
{
    private int Number;
    public string? MealType;
    public string? Name;
    public string? Image;
    public List<Ingredient>? Ingredients;
    public Macros? TotalMacros;

    public void GetRecipesFromDatabase()
    {
        List<Recipe> recipes = new List<Recipe>();
    }

    private void ConnectToDatabase()
    {
        
    }
}