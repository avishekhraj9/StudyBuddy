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
public class RoadmapController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OpenAIService _openAiService;

    public RoadmapController(ApplicationDbContext context, OpenAIService openAiService)
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

    [HttpGet]
    public async Task<IActionResult> GetRoadmap()
    {
        var userId = GetUserId();
        var goal = await _context.CareerGoals
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        var items = await _context.RoadmapItems
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Order)
            .ToListAsync();

        return Ok(new
        {
            goal = goal?.GoalName,
            items
        });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateRoadmap([FromBody] GenerateRoadmapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CareerGoal))
        {
            return BadRequest(new { message = "Career goal track is required." });
        }

        var userId = GetUserId();

        var oldGoals = await _context.CareerGoals.Where(g => g.UserId == userId).ToListAsync();
        _context.CareerGoals.RemoveRange(oldGoals);

        var oldItems = await _context.RoadmapItems.Where(r => r.UserId == userId).ToListAsync();
        _context.RoadmapItems.RemoveRange(oldItems);

        var newGoal = new CareerGoal
        {
            UserId = userId,
            GoalName = request.CareerGoal.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.CareerGoals.Add(newGoal);
        await _context.SaveChangesAsync();

        try
        {
            var apiKey = GetClientApiKey();
            var items = await _openAiService.GenerateRoadmapAsync(userId, newGoal.GoalName, apiKey);

            _context.RoadmapItems.AddRange(items);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Roadmap generated successfully.", items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error generating career roadmap: {ex.Message}" });
        }
    }

    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> ToggleRoadmapItem(int id)
    {
        var userId = GetUserId();
        var item = await _context.RoadmapItems.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
        if (item == null)
        {
            return NotFound(new { message = "Roadmap item not found." });
        }

        item.Status = item.Status switch
        {
            "NotStarted" => "InProgress",
            "InProgress" => "Completed",
            "Completed" => "NotStarted",
            _ => "NotStarted"
        };

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations()
    {
        var userId = GetUserId();
        
        var notes = await _context.Notes.Where(n => n.UserId == userId).ToListAsync();
        var noteIds = notes.Select(n => n.Id).ToList();

        var lowScoreQuizzes = await _context.Quizzes
            .Where(q => noteIds.Contains(q.NoteId) && q.Score.HasValue && q.Score.Value < 70)
            .ToListAsync();

        if (lowScoreQuizzes.Count == 0)
        {
            return Ok(new { recommendations = Array.Empty<object>() });
        }

        var recommendations = lowScoreQuizzes.Select(q => {
            var note = notes.FirstOrDefault(n => n.Id == q.NoteId);
            return new
            {
                quizId = q.Id,
                noteTitle = note?.Title ?? "Notes",
                subject = note?.Subject ?? "General",
                score = q.Score,
                advice = $"Your score in \"{note?.Title}\" (${note?.Subject}) is {q.Score}%. We recommend re-reading your summary notes before continuing other roadmap items."
            };
        }).ToList();

        return Ok(new { recommendations });
    }
}

public class GenerateRoadmapRequest
{
    public string CareerGoal { get; set; } = string.Empty;
}
