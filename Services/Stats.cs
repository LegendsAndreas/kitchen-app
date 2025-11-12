namespace WebKitchen.Services;

public class Stats
{
    public int TotalRecipes;
    public int TotalIngredients;
    public int TotalInstructions;
    public int TotalDinners;
    public int TotalLunches;
    public int TotalBreakfasts;
    public int TotalSides;
    public int TotalSnacks;
    public int TotalRecipesMissingInstructions;
    public int TotalRecipePlaceHolders;
    public Dictionary<int, string> IngredientImagePlaceHolders;
    public Dictionary<int, string> RecipeImagePlaceHolders;
    public Dictionary<int, string> RecipesMissingInstructions;
}