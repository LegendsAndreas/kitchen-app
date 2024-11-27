
namespace WebKitchen.Services;

public class DailyMeal
{
    public List<Recipe> DailyMeals { get; set; } = new();
    public Macros TotalDailyMacros { get; set; } = new();

    public void AddMacrosToTotalMacros(Macros total)
    {
        TotalDailyMacros.TotalCalories += total.TotalCalories;
        TotalDailyMacros.TotalFats += total.TotalFats;
        TotalDailyMacros.TotalCarbs += total.TotalCarbs;
        TotalDailyMacros.TotalProtein += total.TotalProtein;
    }

    public void RemoveMacrosFromTotalMacros(Macros total)
    {
        TotalDailyMacros.TotalCalories -= total.TotalCalories;
        TotalDailyMacros.TotalFats -= total.TotalFats;
        TotalDailyMacros.TotalCarbs -= total.TotalCarbs;
        TotalDailyMacros.TotalProtein -= total.TotalProtein;
    }
}

public class MealPlan
{
    public List<DailyMeal> DailyMealsList { get; set; } = new();
    public Macros TotalWeeklyMacros { get; set; } = new();

    public MealPlan()
    {
        DailyMeal dayOneMeals = new();
        DailyMeal dayTwoMeals = new();
        DailyMeal dayThreeMeals = new();
        DailyMeal dayFourMeals = new();
        DailyMeal dayFiveMeals = new();
        DailyMeal daySixMeals = new();
        DailyMeal daySevenMeals = new();
        
        DailyMealsList.Add(dayOneMeals);
        DailyMealsList.Add(dayTwoMeals);
        DailyMealsList.Add(dayThreeMeals);
        DailyMealsList.Add(dayFourMeals);
        DailyMealsList.Add(dayFiveMeals);
        DailyMealsList.Add(daySixMeals);
        DailyMealsList.Add(daySevenMeals);
    }
    
    public void AddMacrosAndRecipeToDay(Recipe recipe, int day)
    {
        Console.WriteLine($"Adding macros and recipe to Day: {recipe.TotalMacros.TotalCalories} {recipe.TotalMacros.TotalCarbs} {recipe.TotalMacros.TotalFats} {recipe.TotalMacros.TotalProtein}");
        
        DailyMealsList[day].DailyMeals.Add(recipe);
        
        TotalWeeklyMacros.TotalCalories += recipe.TotalMacros.TotalCalories;
        TotalWeeklyMacros.TotalFats += recipe.TotalMacros.TotalFats;
        TotalWeeklyMacros.TotalCarbs += recipe.TotalMacros.TotalCarbs;
        TotalWeeklyMacros.TotalProtein += recipe.TotalMacros.TotalProtein;
        
        DailyMealsList[day].AddMacrosToTotalMacros(recipe.TotalMacros);
    }
    
    public void RemoveMacrosAndRecipeToDay(Recipe recipe, int day)
    {
        DailyMealsList[day].DailyMeals.Remove(recipe);
        
        TotalWeeklyMacros.TotalCalories -= recipe.TotalMacros.TotalCalories;
        TotalWeeklyMacros.TotalFats -= recipe.TotalMacros.TotalFats;
        TotalWeeklyMacros.TotalCarbs -= recipe.TotalMacros.TotalCarbs;
        TotalWeeklyMacros.TotalProtein -= recipe.TotalMacros.TotalProtein;
        
        DailyMealsList[day].RemoveMacrosFromTotalMacros(recipe.TotalMacros);
    }
}