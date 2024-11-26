
namespace WebKitchen.Services;

public class MealPlan
{
    // Lister -> Listen
    // DailyRecipes -> DailyRecipes[n]
    public List<List<Recipe>> DailyRecipes  {get; set;}
    public Macros TotalMacros { get; set; }
    
    public void AddMacrosAndRecipeToDay(Recipe recipe, int day, int index)
    {
        DailyRecipes[day].Add(recipe);
        
        TotalMacros.TotalCalories += DailyRecipes[day][index].TotalMacros.TotalCalories;
        TotalMacros.TotalFats += DailyRecipes[day][index].TotalMacros.TotalFats;
        TotalMacros.TotalCarbs += DailyRecipes[day][index].TotalMacros.TotalCarbs;
        TotalMacros.TotalProtein += DailyRecipes[day][index].TotalMacros.TotalProtein;
    }
}