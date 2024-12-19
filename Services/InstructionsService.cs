using System.Text.Json.Serialization;

namespace WebKitchen.Services;

public class RecipeInstructionRecord
{
    public int Id { get; set; }
    [JsonPropertyName("instructions")]
    public RecipeInstructions Instructions { get; set; } // Deserialized JSON data
    public int RecipeId { get; set; }
}

public class RecipeInstructions
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("steps")]
    public List<Step> Steps { get; set; } = [];
    
    [JsonPropertyName("notes")]
    public List<Note> Notes { get; set; } = [];
}

public class Step
{
    [JsonPropertyName("step_number")]
    public int StepNumber { get; set; }
    
    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = "";
}

public class Note
{
    [JsonPropertyName("note_number")]
    public int NoteNumber { get; set; }
    [JsonPropertyName("note")]
    public string NoteText { get; set; } = "";
}