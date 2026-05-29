using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AIStudyBuddy.Data;
using AIStudyBuddy.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=studybuddy.db"));

// Register Custom Services
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<ResourceHubService>();

// Register JWT Authentication
var secret = builder.Configuration["Jwt:Key"] ?? "AIStudyBuddySuperSecretKeyWhichMustBeAtLeast32BytesLong!";
var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AIStudyBuddy",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AIStudyBuddyUsers",
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

// Add Controllers
builder.Services.AddControllers();

// Add Swagger / OpenAPI (Optional but helpful)
builder.Services.AddOpenApi();

var app = builder.Build();

// Setup/Migrate the SQLite Database on Startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    // Dynamically check and create missing tables (SQLite fallback migration)
    var conn = context.Database.GetDbConnection();
    var wasClosed = conn.State == System.Data.ConnectionState.Closed;
    if (wasClosed) conn.Open();
    try
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='LearningResources';";
            var exists = cmd.ExecuteScalar();
            if (exists == null)
            {
                using (var createCmd = conn.CreateCommand())
                {
                    createCmd.CommandText = @"
                        CREATE TABLE ""LearningResources"" (
                            ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                            ""Topic"" TEXT NOT NULL,
                            ""ResourceId"" TEXT NOT NULL,
                            ""Title"" TEXT NOT NULL,
                            ""Type"" TEXT NOT NULL,
                            ""ChannelOrPublisher"" TEXT NOT NULL,
                            ""DurationOrDetails"" TEXT NOT NULL,
                            ""Rating"" REAL NOT NULL
                        );";
                    createCmd.ExecuteNonQuery();
                }
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='UserResourceProgresses';";
            var exists = cmd.ExecuteScalar();
            if (exists == null)
            {
                using (var createCmd = conn.CreateCommand())
                {
                    createCmd.CommandText = @"
                        CREATE TABLE ""UserResourceProgresses"" (
                            ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                            ""UserId"" INTEGER NOT NULL,
                            ""ResourceId"" TEXT NOT NULL,
                            ""IsCompleted"" INTEGER NOT NULL
                        );";
                    createCmd.ExecuteNonQuery();
                }
            }
        }
    }
    finally
    {
        if (wasClosed) conn.Close();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Enable serving static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();
