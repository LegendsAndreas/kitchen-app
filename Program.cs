using WebKitchen.Components;
using WebKitchen.Services;

namespace WebKitchen;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddScoped<Recipe>();
        builder.Services.AddScoped<Ingredient>();
        builder.Services.AddScoped<SharedRecipe>();
        builder.Services.AddScoped<SharedRecipeList>();
        builder.Services.AddScoped<SharedIngredientList>();

        builder.Services.AddSingleton(_ =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (connectionString == null)
            {
                Console.WriteLine("No connection string found.");
                return null;
            }

            return new DBService(connectionString);
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }


        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}

/*"Kestrel": {
    "Endpoints": {
        "Http": {
            "Url": "http://192.168.112.169:5001"
        }
    }
},*/