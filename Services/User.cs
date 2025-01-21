namespace WebKitchen.Services;

public class User
{
    public event Action? OnChange;
    private uint Id { get; set; }
    public string Username { get; set; }
    private string Email { get; set; }
    private string Password { get; set; }
    public List<Recipe> Recipes { get; set; } = [];

    public uint GetUserId()
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

    public void SetId(uint id)
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

    public void SetUser(uint id, string name, string email, string password, List<Recipe> recipes)
    {
        SetId(id);
        SetUsername(name);
        SetEmail(email);
        SetPassword(password);
        SetRecipes(recipes);
    }

    /// <summary>
    /// Converts a signed integer to an unsigned integer.
    /// </summary>
    /// <param name="i">The signed integer to convert.</param>
    /// <returns>The unsigned integer equivalent of the input value. Returns 0 if the input is less than 1.</returns>
    public uint IntToUint(int i)
    {
        Console.WriteLine("Converting int to uint...");

        if (i < 1)
            return 0;
        
        return (uint)i;
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