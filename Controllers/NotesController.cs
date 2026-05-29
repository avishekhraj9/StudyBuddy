using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIStudyBuddy.Data;
using AIStudyBuddy.Models;
using AIStudyBuddy.Services;

namespace AIStudyBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PdfService _pdfService;
    private readonly OpenAIService _openAiService;
    private readonly string _uploadsFolder;

    public NotesController(ApplicationDbContext context, PdfService pdfService, OpenAIService openAiService)
    {
        _context = context;
        _pdfService = pdfService;
        _openAiService = openAiService;
        _uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        
        if (!Directory.Exists(_uploadsFolder))
        {
            Directory.CreateDirectory(_uploadsFolder);
        }
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

    [HttpGet]
    public async Task<IActionResult> GetNotes([FromQuery] string? search, [FromQuery] string? subject)
    {
        var userId = GetUserId();
        var query = _context.Notes.Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(searchLower) || n.FullText.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            var subjectLower = subject.ToLower();
            query = query.Where(n => n.Subject.ToLower() == subjectLower);
        }

        var notes = await query.OrderByDescending(n => n.UploadDate).ToListAsync();
        return Ok(notes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNoteById(int id)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        note.LastViewed = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(note);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadNote([FromForm] IFormFile file, [FromForm] string? subject)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please upload a valid file." });
        }

        var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only PDF, DOCX, and TXT files are allowed." });
        }

        var userId = GetUserId();

        // Save file to disk
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(_uploadsFolder, uniqueFileName);
        
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            // Extract text
            var extractedText = await _pdfService.ExtractTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                System.IO.File.Delete(filePath);
                return BadRequest(new { message = "Could not extract any text content from the file." });
            }

            // Create Note record
            var note = new Note
            {
                UserId = userId,
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                FileName = file.FileName,
                FilePath = filePath,
                Subject = string.IsNullOrWhiteSpace(subject) ? "General" : subject.Trim(),
                UploadDate = DateTime.UtcNow,
                FullText = extractedText,
                LastViewed = DateTime.UtcNow
            };

            _context.Notes.Add(note);
            await _context.SaveChangesAsync();

            // Generate summary
            var apiKey = GetClientApiKey();
            note.Summary = await _openAiService.GenerateSummaryAsync(extractedText, apiKey);
            await _context.SaveChangesAsync();

            return Ok(note);
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            return StatusCode(500, new { message = $"Error processing file: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNote(int id)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        if (System.IO.File.Exists(note.FilePath))
        {
            try
            {
                System.IO.File.Delete(note.FilePath);
            }
            catch (Exception) { }
        }

        _context.Notes.Remove(note);
        
        var quizzes = await _context.Quizzes.Where(q => q.NoteId == id).ToListAsync();
        _context.Quizzes.RemoveRange(quizzes);

        var flashcards = await _context.Flashcards.Where(f => f.NoteId == id).ToListAsync();
        _context.Flashcards.RemoveRange(flashcards);

        var chatMessages = await _context.ChatMessages.Where(c => c.NoteId == id).ToListAsync();
        _context.ChatMessages.RemoveRange(chatMessages);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Note and all associated content deleted." });
    }

    [HttpPost("{id}/summary")]
    public async Task<IActionResult> RegenerateSummary(int id)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var apiKey = GetClientApiKey();
        note.Summary = await _openAiService.GenerateSummaryAsync(note.FullText, apiKey);
        await _context.SaveChangesAsync();

        return Ok(new { summary = note.Summary });
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var userId = GetUserId();
        var subjects = await _context.Notes
            .Where(n => n.UserId == userId)
            .Select(n => n.Subject)
            .Distinct()
            .ToListAsync();
        return Ok(subjects);
    }
}
