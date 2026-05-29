namespace AIStudyBuddy.Models;

public class LearningResource
{
    public int Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty; // YouTube video ID or URL
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "Video"; // "Video", "Article", "Documentation", "Practice"
    public string ChannelOrPublisher { get; set; } = string.Empty;
    public string DurationOrDetails { get; set; } = string.Empty;
    public double Rating { get; set; } = 4.8;
}
