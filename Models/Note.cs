using System;

namespace AIStudyBuddy.Models;

public class Note
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public string? Summary { get; set; }
    public string FullText { get; set; } = string.Empty;
    public DateTime? LastViewed { get; set; }
}
