namespace AIStudyBuddy.Models;

public class UserResourceProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ResourceId { get; set; } = string.Empty; // links to LearningResource.ResourceId
    public bool IsCompleted { get; set; }
}
