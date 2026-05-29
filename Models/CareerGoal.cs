using System;

namespace AIStudyBuddy.Models;

public class CareerGoal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string GoalName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
