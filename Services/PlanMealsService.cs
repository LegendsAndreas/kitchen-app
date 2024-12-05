
namespace WebKitchen.Services;

public class DailyMeal
{
    public List<Recipe> DailyMeals { get; set; } = new();
    public Macros TotalDailyMacros { get; set; } = new();

    public void AddMacrosToTotalMacros(Macros total)
    {
        TotalDailyMacros.Calories += total.Calories;
        TotalDailyMacros.Fat += total.Fat;
        TotalDailyMacros.Carbs += total.Carbs;
        TotalDailyMacros.Protein += total.Protein;
    }

    public void RemoveMacrosFromTotalMacros(Macros total)
    {
        TotalDailyMacros.Calories -= total.Calories;
        TotalDailyMacros.Fat -= total.Fat;
        TotalDailyMacros.Carbs -= total.Carbs;
        TotalDailyMacros.Protein -= total.Protein;
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
        Console.WriteLine($"Adding macros and recipe to Day: {recipe.TotalMacros.Calories} {recipe.TotalMacros.Carbs} {recipe.TotalMacros.Fat} {recipe.TotalMacros.Protein}");
        
        DailyMealsList[day].DailyMeals.Add(recipe);
        
        TotalWeeklyMacros.Calories += recipe.TotalMacros.Calories;
        TotalWeeklyMacros.Fat += recipe.TotalMacros.Fat;
        TotalWeeklyMacros.Carbs += recipe.TotalMacros.Carbs;
        TotalWeeklyMacros.Protein += recipe.TotalMacros.Protein;
        
        DailyMealsList[day].AddMacrosToTotalMacros(recipe.TotalMacros);
    }
    
    public void RemoveMacrosAndRecipeToDay(Recipe recipe, int day)
    {
        DailyMealsList[day].DailyMeals.Remove(recipe);
        
        TotalWeeklyMacros.Calories -= recipe.TotalMacros.Calories;
        TotalWeeklyMacros.Fat -= recipe.TotalMacros.Fat;
        TotalWeeklyMacros.Carbs -= recipe.TotalMacros.Carbs;
        TotalWeeklyMacros.Protein -= recipe.TotalMacros.Protein;
        
        DailyMealsList[day].RemoveMacrosFromTotalMacros(recipe.TotalMacros);
    }
}