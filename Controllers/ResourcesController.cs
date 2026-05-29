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
public class ResourcesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ResourceHubService _resourceService;

    public ResourcesController(ApplicationDbContext context, ResourceHubService resourceService)
    {
        _context = context;
        _resourceService = resourceService;
    }

    private int GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    private string? GetClientApiKey()
    {
        if (Request.Headers.TryGetValue("X-YouTube-Key", out var headerValues))
        {
            var key = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }
        return null;
    }

    [HttpGet("note/{noteId}")]
    public async Task<IActionResult> GetResourcesForNote(int noteId)
    {
        var userId = GetUserId();
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note == null)
        {
            return NotFound(new { message = "Note not found." });
        }

        var queryTopic = !string.IsNullOrWhiteSpace(note.Subject) && note.Subject != "General"
            ? note.Subject
            : note.Title;

        var apiKey = GetClientApiKey();
        var resources = await _resourceService.GetResourcesForTopicAsync(queryTopic, apiKey);

        return Ok(resources);
    }

    [HttpGet("topic")]
    public async Task<IActionResult> GetResourcesForTopic([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { message = "Query parameter is required." });
        }

        var apiKey = GetClientApiKey();
        var resources = await _resourceService.GetResourcesForTopicAsync(query, apiKey);
        return Ok(resources);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        var userId = GetUserId();
        var completed = await _context.UserResourceProgresses
            .Where(p => p.UserId == userId && p.IsCompleted)
            .Select(p => p.ResourceId)
            .ToListAsync();

        return Ok(completed);
    }

    [HttpPost("{resourceId}/progress")]
    public async Task<IActionResult> ToggleProgress(string resourceId, [FromBody] ToggleProgressRequest request)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return BadRequest(new { message = "Resource ID is required." });
        }

        var userId = GetUserId();
        var progress = await _context.UserResourceProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ResourceId == resourceId);

        if (progress == null)
        {
            progress = new UserResourceProgress
            {
                UserId = userId,
                ResourceId = resourceId,
                IsCompleted = request.IsCompleted
            };
            _context.UserResourceProgresses.Add(progress);
        }
        else
        {
            progress.IsCompleted = request.IsCompleted;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Progress updated.", isCompleted = progress.IsCompleted });
    }
}

public class ToggleProgressRequest
{
    public bool IsCompleted { get; set; }
}
