using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIStudyBuddy.Data;
using AIStudyBuddy.Models;
using AIStudyBuddy.Services;

namespace AIStudyBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OpenAIService _openAiService;

    public ChatController(ApplicationDbContext context, OpenAIService openAiService)
    {
        _context = context;
        _openAiService = openAiService;
    }

    private int GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetClientApiKey()
    {
        if (Request.Headers.TryGetValue("X-OpenAI-Key", out var headerValues))
        {
            var key = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }
        return null;
    }

    [HttpGet("{noteId}/history")]
    public async Task<IActionResult> GetChatHistory(int noteId)
    {
        var userId = GetUserId();
        var isAuthorized = await _context.Notes.AnyAsync(n => n.Id == noteId && n.UserId == userId);
        if (!isAuthorized)
        {
            return Unauthorized(new { message = "Unauthorized access to note." });
        }

        var history = await _context.ChatMessages
            .Where(c => c.NoteId == noteId)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost("{noteId}/send")]
    public async Task<IActionResult> SendMessage(int noteId, [FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            return BadRequest(new { message = "Message content is required." });
        }

        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var userMsg = new ChatMessage
        {
            NoteId = noteId,
            Sender = "User",
            MessageText = request.MessageText.Trim(),
            Timestamp = DateTime.UtcNow
        };
        _context.ChatMessages.Add(userMsg);
        await _context.SaveChangesAsync();

        var apiKey = GetClientApiKey();
        var aiResponse = await _openAiService.AskQuestionAsync(note.FullText, userMsg.MessageText, apiKey);

        var aiMsg = new ChatMessage
        {
            NoteId = noteId,
            Sender = "AI",
            MessageText = aiResponse,
            Timestamp = DateTime.UtcNow
        };
        _context.ChatMessages.Add(aiMsg);
        await _context.SaveChangesAsync();

        return Ok(aiMsg);
    }
}

public class ChatMessageRequest
{
    public string MessageText { get; set; } = string.Empty;
}
