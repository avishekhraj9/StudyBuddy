using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AIStudyBuddy.Models;

namespace AIStudyBuddy.Services;

public class ResourceHubService
{
    private readonly HttpClient _httpClient;
    private readonly string? _defaultYouTubeKey;

    public ResourceHubService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _defaultYouTubeKey = configuration["YouTube:ApiKey"];
    }

    public async Task<List<LearningResource>> GetResourcesForTopicAsync(string topic, string? clientKey = null)
    {
        var apiKey = !string.IsNullOrWhiteSpace(clientKey) ? clientKey : _defaultYouTubeKey;
        var resources = new List<LearningResource>();

        // Load pre-curated local catalog matches
        var catalogResources = GetLocalCuratedCatalog(topic);
        resources.AddRange(catalogResources);

        // If a YouTube API key is active, dynamically fetch additional videos
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var dynamicVideos = await FetchYouTubeVideosAsync(topic, apiKey);
                resources.InsertRange(0, dynamicVideos);
            }
            catch (Exception)
            {
                // Fallback silently to curated catalog
            }
        }

        // Return deduplicated resources by ResourceId
        return resources
            .GroupBy(r => r.ResourceId)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<List<LearningResource>> FetchYouTubeVideosAsync(string query, string apiKey)
    {
        var list = new List<LearningResource>();
        var url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&maxResults=4&q={Uri.EscapeDataString(query)}&type=video&key={apiKey}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return list;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return list;

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idObj) && idObj.TryGetProperty("videoId", out var videoIdProp))
            {
                var videoId = videoIdProp.GetString();
                var snippet = item.GetProperty("snippet");
                var title = snippet.GetProperty("title").GetString() ?? "Recommended Video";
                var channel = snippet.GetProperty("channelTitle").GetString() ?? "YouTube Channel";

                if (!string.IsNullOrEmpty(videoId))
                {
                    list.Add(new LearningResource
                    {
                        Topic = query,
                        ResourceId = videoId,
                        Title = WebUtilityDecode(title),
                        Type = "Video",
                        ChannelOrPublisher = channel,
                        DurationOrDetails = "Video",
                        Rating = 4.8
                    });
                }
            }
        }

        return list;
    }

    private string WebUtilityDecode(string text)
    {
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private List<LearningResource> GetLocalCuratedCatalog(string topic)
    {
        var list = new List<LearningResource>();
        var term = topic.ToLowerInvariant();

        if (term.Contains("c#") || term.Contains(".net") || term.Contains("asp.net") || term.Contains("web api") || term.Contains("oop"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "GHJip5qf9l4", Title = "C# Full Course for Beginners", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "4 hours", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "27k_p-2W42Y", Title = "ASP.NET Core Web API Complete Guide", Type = "Video", ChannelOrPublisher = "Nick Chapsas", DurationOrDetails = "35 min", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "J_Vv9E9r_tA", Title = "Entity Framework Core Database Crash Course", Type = "Video", ChannelOrPublisher = "Teddy Smith", DurationOrDetails = "45 min", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://learn.microsoft.com/en-us/aspnet/core/introduction-to-aspnet-core", Title = "Introduction to ASP.NET Core Framework", Type = "Documentation", ChannelOrPublisher = "Microsoft Learn", DurationOrDetails = "Article Guide", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "NET_PRACTICE_BOOKSTORE", Title = "Practice: Build a bookstore REST API backend with CRUD", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Intermediate", Rating = 4.7 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "NET_PROJECT_JWT", Title = "Project: Implement JWT Auth protection on controller routes", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Advanced", Rating = 4.8 });
        }
        else if (term.Contains("python") || term.Contains("pandas") || term.Contains("numpy") || term.Contains("wrangling") || term.Contains("exploratory"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "LHBEWDH21_o", Title = "Python for Data Science - 12 Hour Full Course", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "12 hours", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "vmEHCJofSLg", Title = "Pandas & NumPy Complete Walkthrough", Type = "Video", ChannelOrPublisher = "Keith Galli", DurationOrDetails = "1 hour", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://pandas.pydata.org/docs/user_guide/index.html", Title = "Official Pandas User Reference Documentation", Type = "Documentation", ChannelOrPublisher = "Pandas Docs", DurationOrDetails = "Doc Link", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "PY_PRACTICE_CLEANING", Title = "Practice: Clean an employee dataset and evaluate statistical ranges", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Beginner", Rating = 4.7 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "PY_PROJECT_HOUSE_PRICES", Title = "Project: Plot correlation matrix heatmaps for housing pricing datasets", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Intermediate", Rating = 4.8 });
        }
        else if (term.Contains("machine learning") || term.Contains("regression") || term.Contains("clustering") || term.Contains("classification") || term.Contains("learning path"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "OwTzWLI7jlo", Title = "Linear Regression Explained Visually", Type = "Video", ChannelOrPublisher = "StatQuest", DurationOrDetails = "15 min", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "yIYKR4sgzI8", Title = "Logistic Regression & Classification Fundamentals", Type = "Video", ChannelOrPublisher = "StatQuest", DurationOrDetails = "20 min", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "9f-GaaQkUro", Title = "Machine Learning Full Course for Beginners", Type = "Video", ChannelOrPublisher = "Simplilearn", DurationOrDetails = "6 hours", Rating = 4.7 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://scikit-learn.org/stable/supervised_learning.html", Title = "Scikit-Learn Supervised Learning Users Reference", Type = "Documentation", ChannelOrPublisher = "Scikit-Learn Docs", DurationOrDetails = "Doc Link", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "ML_PRACTICE_SCIKIT", Title = "Practice: Train a linear regression predicting vehicle values", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Intermediate", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "ML_PROJECT_FASTAPI", Title = "Project: Deploy customer clustering output behind a FastAPI server", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Advanced", Rating = 4.9 });
        }
        else if (term.Contains("sql") || term.Contains("database") || term.Contains("joins") || term.Contains("query"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "HXV3zeypf6Y", Title = "SQL Full Database Course for Beginners", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "4 hours", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "9yeQE6T1T6U", Title = "SQL Joins Explained Visually & Step-by-Step", Type = "Video", ChannelOrPublisher = "Alex The Analyst", DurationOrDetails = "12 min", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://www.postgresql.org/docs/current/tutorial-window.html", Title = "SQL Window Functions Tutorial Guide", Type = "Documentation", ChannelOrPublisher = "PostgreSQL Docs", DurationOrDetails = "Reference Link", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "SQL_PRACTICE_JOINS", Title = "Practice: Write database joins extracting regional retail metrics", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Beginner", Rating = 4.7 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "SQL_PROJECT_INDEXING", Title = "Project: Optimize indexes and analyze search latency times on large DB schemas", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Advanced", Rating = 4.8 });
        }
        else if (term.Contains("aws") || term.Contains("cloud") || term.Contains("terraform") || term.Contains("vpc"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "SOTamWGuq2c", Title = "AWS Certified Cloud Practitioner Full Course", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "13 hours", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "hvnK5v2sUj0", Title = "Terraform Complete Tutorial for Beginners", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "2 hours", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://registry.terraform.io/providers/hashicorp/aws/latest/docs", Title = "Official HashiCorp Terraform AWS Provider Reference", Type = "Documentation", ChannelOrPublisher = "HashiCorp Docs", DurationOrDetails = "Doc Link", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "CLOUD_PRACTICE_IAC", Title = "Practice: Deploy a secure static website using S3 bucket and CloudFront", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Beginner", Rating = 4.7 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "CLOUD_PROJECT_VPC", Title = "Project: Setup private and public subnets inside a virtual network VPC", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Intermediate", Rating = 4.8 });
        }
        else if (term.Contains("docker") || term.Contains("kubernetes") || term.Contains("devops") || term.Contains("ci/cd") || term.Contains("pipeline"))
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "3c-iKgk3Z0M", Title = "Docker Crash Course for Beginners", Type = "Video", ChannelOrPublisher = "freeCodeCamp", DurationOrDetails = "2 hours", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "X48VuDVv0do", Title = "Kubernetes Complete Architecture & Deployment Tutorial", Type = "Video", ChannelOrPublisher = "TechWorld with Nana", DurationOrDetails = "3 hours", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://docs.docker.com/get-started/", Title = "Get Started with Docker Containerization", Type = "Documentation", ChannelOrPublisher = "Docker Docs", DurationOrDetails = "Doc Link", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "DEVOPS_PRACTICE_CONTAINER", Title = "Practice: Multi-stage Dockerfile packaging for database-driven web APIs", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Intermediate", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "DEVOPS_PROJECT_K8S", Title = "Project: Configure horizontal pod autoscale deployments on Kubernetes", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Advanced", Rating = 4.9 });
        }
        else // default generic study resources
        {
            list.Add(new LearningResource { Topic = topic, ResourceId = "fDbxCWt4uOE", Title = "How to Study Effectively using Active Recall", Type = "Video", ChannelOrPublisher = "Ali Abdaal", DurationOrDetails = "15 min", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "cVf38y07Cf0", Title = "Spaced Repetition & Cognitive Memory Science", Type = "Video", ChannelOrPublisher = "Thomas Frank", DurationOrDetails = "10 min", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "https://learninglab.psych.wustl.edu/articles/active-recall/", Title = "Active Recall Practice Testing Science Guide", Type = "Documentation", ChannelOrPublisher = "Harvard Learning Center", DurationOrDetails = "Article", Rating = 4.9 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "STUDY_PRACTICE_QUIZ", Title = "Practice: Write a 5-question mock exam sheet on your uploaded notes", Type = "Practice", ChannelOrPublisher = "Study Buddy Challenges", DurationOrDetails = "Beginner", Rating = 4.8 });
            list.Add(new LearningResource { Topic = topic, ResourceId = "STUDY_PROJECT_CARDS", Title = "Project: Complete flashcard spaced reviews three consecutive days", Type = "Project", ChannelOrPublisher = "Study Buddy Projects", DurationOrDetails = "Intermediate", Rating = 4.8 });
        }

        return list;
    }
}
