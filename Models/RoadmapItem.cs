namespace AIStudyBuddy.Models;

public class RoadmapItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string WeekRange { get; set; } = string.Empty; // e.g. "Week 1-2"
    public string Difficulty { get; set; } = "Beginner"; // "Beginner", "Intermediate", "Advanced"
    public string Status { get; set; } = "NotStarted"; // "NotStarted", "InProgress", "Completed"
    public string Projects { get; set; } = string.Empty;
    public string InterviewQuestions { get; set; } = string.Empty;
    public string ParentSkill { get; set; } = string.Empty;
    public int Order { get; set; }
}
