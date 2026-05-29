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
public class QuizController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OpenAIService _openAiService;

    public QuizController(ApplicationDbContext context, OpenAIService openAiService)
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuizById(int id)
    {
        var userId = GetUserId();
        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == id && _context.Notes.Any(n => n.Id == q.NoteId && n.UserId == userId));

        if (quiz == null)
        {
            return NotFound(new { message = "Quiz not found or unauthorized." });
        }

        return Ok(quiz);
    }

    [HttpGet("note/{noteId}")]
    public async Task<IActionResult> GetQuizzesByNote(int noteId)
    {
        var userId = GetUserId();
        var isAuthorized = await _context.Notes.AnyAsync(n => n.Id == noteId && n.UserId == userId);
        if (!isAuthorized)
        {
            return Unauthorized(new { message = "Unauthorized access to note." });
        }

        var quizzes = await _context.Quizzes
            .Where(q => q.NoteId == noteId)
            .OrderByDescending(q => q.CreatedDate)
            .ToListAsync();

        return Ok(quizzes);
    }

    [HttpPost("generate/{noteId}")]
    public async Task<IActionResult> GenerateQuiz(int noteId)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var quiz = new Quiz
        {
            NoteId = noteId,
            Title = $"Quiz for {note.Title}",
            CreatedDate = DateTime.UtcNow
        };

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        try
        {
            var apiKey = GetClientApiKey();
            var questions = await _openAiService.GenerateQuizAsync(quiz.Id, note.FullText, apiKey);
            
            _context.Questions.AddRange(questions);
            await _context.SaveChangesAsync();

            return Ok(new { quizId = quiz.Id, title = quiz.Title });
        }
        catch (Exception ex)
        {
            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();
            return StatusCode(500, new { message = $"Error generating quiz: {ex.Message}" });
        }
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitQuiz(int id, [FromBody] SubmitQuizRequest request)
    {
        var userId = GetUserId();
        var quiz = await _context.Quizzes
            .FirstOrDefaultAsync(q => q.Id == id && _context.Notes.Any(n => n.Id == q.NoteId && n.UserId == userId));

        if (quiz == null)
        {
            return NotFound(new { message = "Quiz not found." });
        }

        quiz.Score = request.Score;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Quiz score recorded.", score = quiz.Score });
    }

    #region Flashcards API

    [HttpGet("note/{noteId}/flashcards")]
    public async Task<IActionResult> GetFlashcards(int noteId)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var flashcards = await _context.Flashcards
            .Where(f => f.NoteId == noteId)
            .OrderBy(f => f.NextReviewDate)
            .ToListAsync();

        return Ok(flashcards);
    }

    [HttpPost("note/{noteId}/flashcards/generate")]
    public async Task<IActionResult> GenerateFlashcards(int noteId)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var existing = await _context.Flashcards.Where(f => f.NoteId == noteId).ToListAsync();
        _context.Flashcards.RemoveRange(existing);

        var apiKey = GetClientApiKey();
        var cards = await _openAiService.GenerateFlashcardsAsync(noteId, note.FullText, apiKey);
        _context.Flashcards.AddRange(cards);
        await _context.SaveChangesAsync();

        return Ok(cards);
    }

    [HttpPost("flashcards/{id}/review")]
    public async Task<IActionResult> ReviewFlashcard(int id, [FromBody] FlashcardReviewRequest request)
    {
        var userId = GetUserId();
        var card = await _context.Flashcards
            .FirstOrDefaultAsync(c => c.Id == id && _context.Notes.Any(n => n.Id == c.NoteId && n.UserId == userId));

        if (card == null)
        {
            return NotFound(new { message = "Flashcard not found." });
        }

        if (request.IsCorrect)
        {
            card.SpacedRepetitionLevel = Math.Min(5, card.SpacedRepetitionLevel + 1);
            int days = card.SpacedRepetitionLevel switch
            {
                1 => 1,
                2 => 3,
                3 => 7,
                4 => 14,
                5 => 30,
                _ => 1
            };
            card.NextReviewDate = DateTime.UtcNow.AddDays(days);
        }
        else
        {
            card.SpacedRepetitionLevel = 0;
            card.NextReviewDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(card);
    }

    #endregion
}

public class SubmitQuizRequest
{
    public int Score { get; set; }
}

public class FlashcardReviewRequest
{
    public bool IsCorrect { get; set; }
}
