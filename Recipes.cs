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
    public float Proteins;
    public float Fats;
    public float Multiplier;
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