using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIStudyBuddy.Data;
using AIStudyBuddy.Models;

namespace AIStudyBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlannerController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PlannerController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out int id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks()
    {
        var userId = GetUserId();
        var tasks = await _context.PlannerTasks
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
        return Ok(tasks);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddTask([FromBody] PlannerTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskText))
        {
            return BadRequest(new { message = "Task description is required." });
        }

        var userId = GetUserId();
        var task = new PlannerTask
        {
            UserId = userId,
            TaskText = request.TaskText.Trim(),
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(1),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Daily Goal" : request.Category.Trim(),
            IsCompleted = false
        };

        _context.PlannerTasks.Add(task);
        await _context.SaveChangesAsync();

        return Ok(task);
    }

    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> ToggleTask(int id)
    {
        var userId = GetUserId();
        var task = await _context.PlannerTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task == null)
        {
            return NotFound(new { message = "Task not found." });
        }

        task.IsCompleted = !task.IsCompleted;
        await _context.SaveChangesAsync();

        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = GetUserId();
        var task = await _context.PlannerTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task == null)
        {
            return NotFound(new { message = "Task not found." });
        }

        _context.PlannerTasks.Remove(task);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Task deleted successfully." });
    }
}

public class PlannerTaskRequest
{
    public string TaskText { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Category { get; set; } = "Daily Goal";
}
