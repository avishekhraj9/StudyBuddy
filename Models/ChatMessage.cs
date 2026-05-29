using System;

namespace AIStudyBuddy.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string Sender { get; set; } = string.Empty; // "User" or "AI"
    public string MessageText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
