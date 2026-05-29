using System;

namespace AIStudyBuddy.Models;

public class PlannerTask
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TaskText { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string Category { get; set; } = "Daily Goal"; // "Daily Goal", "Weekly Schedule", "Exam Countdown"
}
