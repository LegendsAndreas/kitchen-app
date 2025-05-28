using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebKitchen.Components;
using WebKitchen.Services;

namespace WebKitchen;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        //https://www.youtube.com/watch?v=GKvEuA80FAE
        /*builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).
            AddCookie(options =>
            {
                options.Cookie.Name = "auth_token";
                options.LoginPath = "/login";
                options.Cookie.MaxAge = TimeSpan.FromMinutes(30);
                options.AccessDeniedPath = "/access-denied";
            });
        
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));*/

        builder.Services.AddScoped<Recipe>();
        builder.Services.AddScoped<Ingredient>();
        builder.Services.AddScoped<SharedRecipe>();
        builder.Services.AddScoped<SharedRecipeList>();
        builder.Services.AddScoped<SharedIngredientList>();
        builder.Services.AddHttpContextAccessor();


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

        /*app.UseAuthentication();
        app.UseAuthorization();*/

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