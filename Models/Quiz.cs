using System;
using System.Collections.Generic;

namespace AIStudyBuddy.Models;

public class Quiz
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Score { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public List<Question> Questions { get; set; } = new();
}
