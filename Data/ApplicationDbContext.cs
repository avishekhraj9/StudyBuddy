using Microsoft.EntityFrameworkCore;
using AIStudyBuddy.Models;

namespace AIStudyBuddy.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PlannerTask> PlannerTasks => Set<PlannerTask>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<CareerGoal> CareerGoals => Set<CareerGoal>();
    public DbSet<RoadmapItem> RoadmapItems => Set<RoadmapItem>();
    public DbSet<LearningResource> LearningResources => Set<LearningResource>();
    public DbSet<UserResourceProgress> UserResourceProgresses => Set<UserResourceProgress>();
}
