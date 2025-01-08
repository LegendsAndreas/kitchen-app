using System.Text.Json.Serialization;

namespace WebKitchen.Services;

public class RecipeInstructionRecord
{
    private int Id { get; set; }

    private int RecipeId { get; set; }

    [JsonPropertyName("instructions")]
    public RecipeInstructions Instructions { get; set; } = new(); // Deserialized JSON data


    public void SetId(int id) => Id = id;
    public void SetRecipeId(int recipeId) => RecipeId = recipeId;

    public void PrintRecipeInstructionsRecord()
    {
        Console.WriteLine("Printing recipe instructions record...");
        
        Console.WriteLine($"Record ID: {GetId()}");
        Console.WriteLine($"Recipe Name: {Instructions.Name}");
        Console.WriteLine($"Recipe ID: {GetRecipeId()}");
        Console.WriteLine("Steps:");
        foreach (var step in Instructions.Steps)
        {
            Console.WriteLine($"  Step {step.StepNumber}: {step.StepText}");
        }

        Console.WriteLine("Notes:");
        foreach (var note in Instructions.Notes)
        {
            Console.WriteLine($"  Note {note.NoteNumber}: {note.NoteText}");
        }
    }

    public int GetId()
    {
        Console.WriteLine("Getting recipe instructions record ID...");
        return Id;
    }

    public int GetRecipeId()
    {
        Console.WriteLine("Getting recipe instructions record recipe ID...");
        return RecipeId;
    }

    public void Clear()
    {
        Id = 0;
        RecipeId = 0;
        Instructions = new RecipeInstructions();
    }
}

public class RecipeInstructions
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("steps")] public List<Step> Steps { get; set; } = [];

    [JsonPropertyName("notes")] public List<Note> Notes { get; set; } = [];
    
}

public class Step
{
    [JsonPropertyName("step_number")] public int StepNumber { get; set; }

    [JsonPropertyName("instruction")] public string StepText { get; set; } = "";

    public void Clear()
    {
        StepNumber = 0;
        StepText = "";
    }

    public Step TransferStepValues(Step step)
    {
        Console.WriteLine("Transferring step values...");

        Step transferStep = new()
        {
            StepNumber = step.StepNumber,
            StepText = step.StepText
        };

        return transferStep;
    }
}

public class Note
{
    [JsonPropertyName("note_number")] public int NoteNumber { get; set; }
    [JsonPropertyName("note")] public string NoteText { get; set; } = "";

    public void Clear()
    {
        NoteNumber = 0;
        NoteText = "";
    }

    public Note TransferNoteValues(Note note)
    {
        Console.WriteLine("Transferring note values...");

        Note transferNote = new()
        {
            NoteNumber = note.NoteNumber,
            NoteText = note.NoteText
        };

        return transferNote;
    }
}