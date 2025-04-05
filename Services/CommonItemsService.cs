namespace WebKitchen.Services;

public class CommonItem
{
    private uint id;
    public string Name = "";
    public string Base64Image = "";
    public string Type = "";
    public decimal Cost;

    public uint GetId()
    {
        return id;
    }

    public void SetId(uint passedId)
    {
        id = passedId;
    }
}