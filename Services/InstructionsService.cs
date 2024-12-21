using System.Text.Json.Serialization;

namespace WebKitchen.Services;

public class RecipeInstructionRecord
{
    private int Id { get; set; }

    [JsonPropertyName("instructions")]
    public RecipeInstructions Instructions { get; set; } = new(); // Deserialized JSON data

    private int RecipeId { get; set; }

    public void SetId(int id) => Id = id;
    public void SetRecipeId(int recipeId) => RecipeId = recipeId;

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