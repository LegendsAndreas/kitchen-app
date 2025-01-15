namespace WebKitchen.Services;

public class User
{
    public event Action? OnChange;
    private int Id { get; set; }
    public string Username { get; set; }
    private string Email { get; set; }
    private string Password { get; set; }
    public List<Recipe> Recipes { get; set; } = [];

    public int GetUserId()
    {
        return Id;
    }

    public string GetEmail()
    {
        return Email;
    }

    public string GetPassword()
    {
        return Password;
    }

    public void SetId(int id)
    {
        Id = id;
        NotifyStateChanged();
    }

    public void SetUsername(string username)
    {
        Username = username;
        NotifyStateChanged();
    }

    public void SetEmail(string email)
    {
        Email = email;
        NotifyStateChanged();
    }

    public void SetPassword(string password)
    {
        Password = password;
        NotifyStateChanged();
    }

    public void SetRecipes(List<Recipe> recipes)
    {
        Recipes = recipes;
        NotifyStateChanged();
    }

    public void SetUser(int id, string name, string email, string password, List<Recipe> recipes)
    {
        SetId(id);
        SetUsername(name);
        SetEmail(email);
        SetPassword(password);
        SetRecipes(recipes);
    }

    public User TransferUser(User user)
    {
        Console.WriteLine("Transferring user...");
        User transUser = new()
        {
            Username = user.Username,
        };
        transUser.SetId(user.GetUserId());
        transUser.SetEmail(user.GetEmail());
        transUser.SetPassword(user.GetPassword());
        transUser.Recipes = user.Recipes;
        
        return transUser;
    }
    
    private void NotifyStateChanged() => OnChange?.Invoke();

    public bool IsOwner()
    {
        if (Id == 1)
            return true;
        else
            return false;
    }
}