using System;

namespace AIStudyBuddy.Models;

public class Flashcard
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string Front { get; set; } = string.Empty;
    public string Back { get; set; } = string.Empty;
    public int SpacedRepetitionLevel { get; set; } = 0;
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;
}
