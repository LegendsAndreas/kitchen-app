namespace WebKitchen.Services;

public class User
{
    private int Id { get; set; }
    public string Name { get; set; }
    private string Email { get; set; }
    private string Password { get; set; }

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
    }

    public void SetEmail(string email)
    {
        Email = email;
    }

    public void SetPassword(string password)
    {
        Password = password;
    }

    public void SetUser(int id, string name, string email, string password)
    {
        Id = id;
        Name = name;
        Email = email;
        Password = password;
    }
}