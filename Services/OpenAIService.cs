using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AIStudyBuddy.Models;

namespace AIStudyBuddy.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string? _defaultApiKey;

    public OpenAIService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _defaultApiKey = configuration["OpenAI:ApiKey"];
    }

    private string? GetApiKey(string? clientKey)
    {
        return !string.IsNullOrWhiteSpace(clientKey) ? clientKey : _defaultApiKey;
    }

    public async Task<string> GenerateSummaryAsync(string text, string? clientKey = null)
    {
        var apiKey = GetApiKey(clientKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateFallbackSummary(text);
        }

        try
        {
            var prompt = $"Analyze the following study materials and provide a summary with key points, major concepts, and chapter-wise breakdowns. Return the result in clean, beautiful Markdown:\n\n{TruncateText(text, 12000)}";
            return await CallOpenAiChatAsync(apiKey, prompt, "You are a specialized text summarizer for academic notes. Highlight key terms in bold.");
        }
        catch (Exception ex)
        {
            return $"*Error calling OpenAI API ({ex.Message}). Falling back to local summary:*\n\n{GenerateFallbackSummary(text)}";
        }
    }

    public async Task<List<Question>> GenerateQuizAsync(int quizId, string text, string? clientKey = null)
    {
        var apiKey = GetApiKey(clientKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateFallbackQuiz(quizId, text);
        }

        try
        {
            var prompt = "Based on the following study materials, generate a JSON array of 5 to 10 quiz questions. " +
                         "The questions should consist of MCQs, True/False, and Fill in the Blanks. " +
                         "You MUST return ONLY the raw JSON array matching this exact C# schema: " +
                         "[ { \"questionText\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", \"optionD\": \"...\", \"correctAnswer\": \"A\"/\"B\"/\"C\"/\"D\", \"questionType\": \"MCQ\"/\"TrueFalse\"/\"FillInTheBlank\", \"explanation\": \"...\" } ]. " +
                         "For True/False questions, OptionA should be 'True', OptionB 'False', OptionC and OptionD empty, and correctAnswer either 'A' or 'B'. " +
                         "For Fill in the Blanks, OptionA should be the correct term, and Options B, C, D should be plausible distractors. " +
                         "Here is the study material:\n\n" + TruncateText(text, 10000);

            var systemPrompt = "You are a professional quiz generator. You only respond with valid JSON arrays containing questions. Do not include markdown code block formatting (like ```json) in your response, just the raw JSON text.";

            var responseJson = await CallOpenAiChatAsync(apiKey, prompt, systemPrompt);
            
            // Strip out ```json if the model accidentally included it
            responseJson = CleanJsonString(responseJson);

            var questions = JsonSerializer.Deserialize<List<RawQuestion>>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (questions == null || questions.Count == 0)
            {
                return GenerateFallbackQuiz(quizId, text);
            }

            return questions.Select(q => new Question
            {
                QuizId = quizId,
                QuestionText = q.QuestionText ?? "Identify the correct statement.",
                OptionA = q.OptionA ?? string.Empty,
                OptionB = q.OptionB ?? string.Empty,
                OptionC = q.OptionC ?? string.Empty,
                OptionD = q.OptionD ?? string.Empty,
                CorrectAnswer = q.CorrectAnswer ?? "A",
                QuestionType = q.QuestionType ?? "MCQ",
                Explanation = q.Explanation ?? "Correct answer selected based on text."
            }).ToList();
        }
        catch (Exception)
        {
            return GenerateFallbackQuiz(quizId, text);
        }
    }

    public async Task<List<Flashcard>> GenerateFlashcardsAsync(int noteId, string text, string? clientKey = null)
    {
        var apiKey = GetApiKey(clientKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateFallbackFlashcards(noteId, text);
        }

        try
        {
            var prompt = "Based on the following study materials, generate a JSON array of 6 to 12 flashcards for spaced repetition study. " +
                         "Each flashcard has a Front (the question or term) and a Back (the answer or definition). " +
                         "You MUST return ONLY the raw JSON array matching this exact schema: " +
                         "[ { \"front\": \"...\", \"back\": \"...\" } ]. " +
                         "Here is the study material:\n\n" + TruncateText(text, 10000);

            var systemPrompt = "You are a flashcard generator. You only output valid JSON arrays. Do not include markdown code block formatting in your response.";

            var responseJson = await CallOpenAiChatAsync(apiKey, prompt, systemPrompt);
            responseJson = CleanJsonString(responseJson);

            var rawCards = JsonSerializer.Deserialize<List<RawFlashcard>>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rawCards == null || rawCards.Count == 0)
            {
                return GenerateFallbackFlashcards(noteId, text);
            }

            return rawCards.Select(c => new Flashcard
            {
                NoteId = noteId,
                Front = c.Front ?? "Study Term",
                Back = c.Back ?? "Study Definition",
                SpacedRepetitionLevel = 0,
                NextReviewDate = DateTime.UtcNow
            }).ToList();
        }
        catch (Exception)
        {
            return GenerateFallbackFlashcards(noteId, text);
        }
    }

    public async Task<string> AskQuestionAsync(string noteText, string questionText, string? clientKey = null)
    {
        var apiKey = GetApiKey(clientKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateFallbackAnswer(noteText, questionText);
        }

        try
        {
            var prompt = $"Study Materials:\n---\n{TruncateText(noteText, 10000)}\n---\n\n" +
                         $"Question: {questionText}\n\n" +
                         "Answer the student's question based on the study materials provided above. " +
                         "Keep it clear, academic, and direct. If the answer cannot be inferred from the notes, " +
                         "provide general knowledge but explicitly state that this information wasn't in the uploaded notes.";

            return await CallOpenAiChatAsync(apiKey, prompt, "You are AI Study Buddy, an intelligent chatbot helping a student study their notes.");
        }
        catch (Exception ex)
        {
            return $"*Error calling OpenAI API ({ex.Message}). Falling back to local response:*\n\n{GenerateFallbackAnswer(noteText, questionText)}";
        }
    }

    private async Task<string> CallOpenAiChatAsync(string apiKey, string prompt, string systemMessage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = prompt }
            },
            temperature = 0.5
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return messageContent ?? string.Empty;
    }

    private string CleanJsonString(string input)
    {
        var json = input.Trim();
        if (json.StartsWith("```"))
        {
            // Remove starting marker like ```json or ```
            int firstLineEnd = json.IndexOf('\n');
            if (firstLineEnd != -1)
            {
                json = json.Substring(firstLineEnd).Trim();
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3).Trim();
            }
        }
        return json;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "... [Truncated]";
    }

    #region Smart Fallback Algorithms

    private string GenerateFallbackSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "No content available to summarize.";
        }

        var sentences = SplitIntoSentences(text);
        if (sentences.Count == 0)
        {
            return "Could not extract readable text from the document.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("# 📖 Document Study Summary");
        sb.AppendLine("*(Note: This is a locally generated summary since no OpenAI API Key is configured.)*");
        sb.AppendLine();
        sb.AppendLine("## 📌 Executive Summary");
        // Use first 4 sentences as overview
        var overviewSentences = sentences.Take(Math.Min(4, sentences.Count));
        sb.AppendLine(string.Join(" ", overviewSentences));
        sb.AppendLine();

        sb.AppendLine("## 💡 Key Conceptual Points");
        
        // Find sentences with definitions or key markers
        var keySentences = sentences
            .Where(s => s.Contains(" is ", StringComparison.OrdinalIgnoreCase) || 
                        s.Contains(" are ", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains(" important ", StringComparison.OrdinalIgnoreCase) || 
                        s.Contains(" key ", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains(" defined ", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(6)
            .ToList();

        if (keySentences.Count < 3)
        {
            // Fallback to taking every Nth sentence
            keySentences = sentences.Skip(2).Where((_, index) => index % 4 == 0).Take(6).ToList();
        }

        foreach (var keySentence in keySentences)
        {
            // Clean sentence and add bullet
            var clean = keySentence.Trim();
            if (clean.Length > 15)
            {
                sb.AppendLine($"- **Concept:** {clean}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## 🎯 Recommended Next Steps");
        sb.AppendLine("1. Use the **Quiz Generator** tab to test your recall on this text.");
        sb.AppendLine("2. Open the **AI Chat** interface to ask custom questions about these materials.");
        sb.AppendLine("3. Flip through the **Flashcard Deck** for vocabulary practice.");

        return sb.ToString();
    }

    private List<Question> GenerateFallbackQuiz(int quizId, string text)
    {
        var sentences = SplitIntoSentences(text);
        var list = new List<Question>();

        // Let's search for definition-like sentences: "X is Y" or "X refers to Y"
        var definitions = new List<(string term, string definition)>();
        var pattern = new Regex(@"\b([A-Z][a-zA-Z\s]{3,25})\b\s+(?:is|refers to|means)\s+([^.]+)", RegexOptions.Compiled);
        
        foreach (var s in sentences)
        {
            var match = pattern.Match(s);
            if (match.Success)
            {
                var term = match.Groups[1].Value.Trim();
                var desc = match.Groups[2].Value.Trim();
                if (term.Length > 2 && desc.Length > 10 && !definitions.Any(d => d.term.Equals(term, StringComparison.OrdinalIgnoreCase)))
                {
                    definitions.Add((term, desc));
                }
            }
        }

        // Generate up to 5 questions from parsed definitions
        int count = 1;
        foreach (var def in definitions.Take(4))
        {
            // Create a multiple choice question
            var question = new Question
            {
                QuizId = quizId,
                QuestionText = $"What is the definition of '{def.term}'?",
                OptionA = CapitalizeFirstLetter(def.definition) + ".",
                OptionB = "A process that occurs under high temperature conditions.",
                OptionC = "The secondary stage of the experimental phase.",
                OptionD = "An obsolete methodology used in previous decades.",
                CorrectAnswer = "A",
                QuestionType = "MCQ",
                Explanation = $"According to the text, {def.term} is defined as: {def.definition}."
            };
            list.Add(question);
            count++;
        }

        // Add some generic fill in the blanks / True False if needed
        if (list.Count < 5 && sentences.Count > 5)
        {
            var factSentence = sentences.FirstOrDefault(s => s.Length > 40 && s.Length < 100);
            if (factSentence != null)
            {
                list.Add(new Question
                {
                    QuizId = quizId,
                    QuestionText = $"True or False: According to the notes, \"{factSentence.Trim()}\"",
                    OptionA = "True",
                    OptionB = "False",
                    OptionC = "",
                    OptionD = "",
                    CorrectAnswer = "A",
                    QuestionType = "TrueFalse",
                    Explanation = "This statement is directly supported by the text in the notes."
                });
            }
        }

        // Hard fallback if the text was too short or parse yielded nothing
        if (list.Count == 0)
        {
            list.Add(new Question
            {
                QuizId = quizId,
                QuestionText = "What is the primary objective of studying these materials?",
                OptionA = "To master the core terms, processes, and principles explained.",
                OptionB = "To memorize the pages line-by-line without understanding.",
                OptionC = "To rewrite the notes into an external database program.",
                OptionD = "To upload files without reviewing the content.",
                CorrectAnswer = "A",
                QuestionType = "MCQ",
                Explanation = "Reviewing and mastering concepts is the core purpose of the Study Buddy dashboard."
            });
            list.Add(new Question
            {
                QuizId = quizId,
                QuestionText = "True or False: Consistent study streaks and active recall (quizzes/flashcards) improve retention.",
                OptionA = "True",
                OptionB = "False",
                OptionC = "",
                OptionD = "",
                CorrectAnswer = "A",
                QuestionType = "TrueFalse",
                Explanation = "Active recall methods, such as taking self-quizzes and flashcard revision, are scientifically proven study boosters."
            });
            list.Add(new Question
            {
                QuizId = quizId,
                QuestionText = "Complete the blank: Spaced repetition is a learning technique where cards are reviewed at ________ intervals.",
                OptionA = "increasing",
                OptionB = "constant",
                OptionC = "decreasing",
                OptionD = "random",
                CorrectAnswer = "A",
                QuestionType = "FillInTheBlank",
                Explanation = "Spaced repetition relies on reviewing information at increasing intervals to push knowledge to long-term memory."
            });
        }

        // Ensure we number them/assign IDs sequentially
        return list;
    }

    private List<Flashcard> GenerateFallbackFlashcards(int noteId, string text)
    {
        var sentences = SplitIntoSentences(text);
        var cards = new List<Flashcard>();

        // Look for definition matches
        var pattern = new Regex(@"\b([A-Z][a-zA-Z\s]{3,25})\b\s+(?:is|refers to|means)\s+([^.]+)", RegexOptions.Compiled);
        foreach (var s in sentences)
        {
            var match = pattern.Match(s);
            if (match.Success)
            {
                var term = match.Groups[1].Value.Trim();
                var desc = match.Groups[2].Value.Trim();
                if (term.Length > 2 && desc.Length > 10 && !cards.Any(c => c.Front.Equals(term, StringComparison.OrdinalIgnoreCase)))
                {
                    cards.Add(new Flashcard
                    {
                        NoteId = noteId,
                        Front = term,
                        Back = CapitalizeFirstLetter(desc) + ".",
                        SpacedRepetitionLevel = 0,
                        NextReviewDate = DateTime.UtcNow
                    });
                }
            }
        }

        // Add default cards if none found
        if (cards.Count < 4)
        {
            cards.Add(new Flashcard { NoteId = noteId, Front = "Active Recall", Back = "Testing your memory by questioning yourself rather than passively reading notes.", SpacedRepetitionLevel = 0, NextReviewDate = DateTime.UtcNow });
            cards.Add(new Flashcard { NoteId = noteId, Front = "Spaced Repetition", Back = "Reviewing cards at increasing intervals (e.g., 1 day, 3 days, 7 days) to lock them in long-term memory.", SpacedRepetitionLevel = 0, NextReviewDate = DateTime.UtcNow });
            cards.Add(new Flashcard { NoteId = noteId, Front = "Feynman Technique", Back = "Explaining a concept in simple terms to a beginner to identify gaps in your own understanding.", SpacedRepetitionLevel = 0, NextReviewDate = DateTime.UtcNow });
            cards.Add(new Flashcard { NoteId = noteId, Front = "Pomodoro Method", Back = "A study system where you work for 25 minutes, then take a 5-minute break.", SpacedRepetitionLevel = 0, NextReviewDate = DateTime.UtcNow });
        }

        return cards;
    }

    private string GenerateFallbackAnswer(string noteText, string questionText)
    {
        if (string.IsNullOrWhiteSpace(noteText)) return "Note has no content.";
        
        var sentences = SplitIntoSentences(noteText);
        var queryWords = questionText
            .Split(new[] { ' ', '?', '.', ',', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Select(w => w.ToLowerInvariant())
            .ToList();

        if (queryWords.Count == 0)
        {
            return "Please ask a more specific question with actual terms from your notes.";
        }

        // Score sentences by word matches
        var scoredSentences = new List<(string sentence, int score)>();
        foreach (var s in sentences)
        {
            int score = 0;
            var sentenceLower = s.ToLowerInvariant();
            foreach (var word in queryWords)
            {
                if (sentenceLower.Contains(word))
                {
                    score++;
                }
            }
            if (score > 0)
            {
                scoredSentences.Add((s.Trim(), score));
            }
        }

        if (scoredSentences.Count > 0)
        {
            var bestMatches = scoredSentences
                .OrderByDescending(x => x.score)
                .Take(3)
                .Select(x => x.sentence)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("🤖 *Here is what I found in your notes regarding your query:*");
            sb.AppendLine();
            foreach (var match in bestMatches)
            {
                sb.AppendLine($"> ... {match} ...");
                sb.AppendLine();
            }
            sb.AppendLine("*(Note: To receive a full generative answer using AI models, configure your OpenAI API Key in Settings.)*");
            return sb.ToString();
        }

        return "🤖 I searched through your note but could not find a direct reference matching your question keywords.\n\n" +
               "*Tip: Ensure your question uses terms that are actually present in the note, or set your OpenAI API Key in settings to enable intelligent cross-document analysis.*";
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitter
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;

        // Replace carriage returns
        var cleanText = text.Replace("\r", " ").Replace("\n", " ");
        var sentenceBoundaries = new Regex(@"(?<=[\.!\?])\s+(?=[A-Z])", RegexOptions.Compiled);
        var parsed = sentenceBoundaries.Split(cleanText);

        foreach (var p in parsed)
        {
            var trimmed = p.Trim();
            if (trimmed.Length > 5)
            {
                list.Add(trimmed);
            }
        }

        return list;
    }

    private string CapitalizeFirstLetter(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        if (str.Length == 1) return str.ToUpper();
        return char.ToUpper(str[0]) + str.Substring(1);
    }

    #endregion

    // Raw classes for deserialization
    private class RawQuestion
    {
        public string? QuestionText { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? QuestionType { get; set; }
        public string? Explanation { get; set; }
    }

    private class RawFlashcard
    {
        public string? Front { get; set; }
        public string? Back { get; set; }
    }

    public async Task<List<RoadmapItem>> GenerateRoadmapAsync(int userId, string careerGoal, string? clientKey = null)
    {
        var apiKey = GetApiKey(clientKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateFallbackRoadmap(userId, careerGoal);
        }

        try
        {
            var prompt = $"Generate a detailed 8-week career roadmap to prepare a student for the role: {careerGoal}. " +
                         "The roadmap should contain 6 to 10 specific skill nodes. For each node, specify: " +
                         "1. SkillName (name of the skill/concept)\n" +
                         "2. WeekRange (e.g. 'Week 1-2', 'Week 3-4')\n" +
                         "3. Difficulty ('Beginner', 'Intermediate', or 'Advanced')\n" +
                         "4. Projects (a short practical study project idea to build)\n" +
                         "5. InterviewQuestions (a mock interview question related to this skill)\n" +
                         "6. ParentSkill (optional parent category like 'Backend Dev', 'Frontend Dev', 'Databases', etc.)\n" +
                         "You MUST return ONLY the raw JSON array matching this exact C# schema: " +
                         "[ { \"skillName\": \"...\", \"weekRange\": \"...\", \"difficulty\": \"...\", \"projects\": \"...\", \"interviewQuestions\": \"...\", \"parentSkill\": \"...\" } ]. " +
                         "Keep order logical. Do not include markdown code block formatting (like ```json) in your response, just the raw JSON text.";

            var systemPrompt = "You are a professional IT career counselor and tech curriculum architect. You only respond with valid JSON arrays matching the requested schema.";

            var responseJson = await CallOpenAiChatAsync(apiKey, prompt, systemPrompt);
            responseJson = CleanJsonString(responseJson);

            var items = JsonSerializer.Deserialize<List<RawRoadmapItem>>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items == null || items.Count == 0)
            {
                return GenerateFallbackRoadmap(userId, careerGoal);
            }

            int order = 1;
            return items.Select(item => new RoadmapItem
            {
                UserId = userId,
                SkillName = item.SkillName ?? "General Skill",
                WeekRange = item.WeekRange ?? "Week 1-2",
                Difficulty = item.Difficulty ?? "Beginner",
                Status = "NotStarted",
                Projects = item.Projects ?? "Build a basic proof-of-concept.",
                InterviewQuestions = item.InterviewQuestions ?? "Explain this concept.",
                ParentSkill = item.ParentSkill ?? string.Empty,
                Order = order++
            }).ToList();
        }
        catch (Exception)
        {
            return GenerateFallbackRoadmap(userId, careerGoal);
        }
    }

    private List<RoadmapItem> GenerateFallbackRoadmap(int userId, string careerGoal)
    {
        var list = new List<RoadmapItem>();
        var goalLower = careerGoal.ToLowerInvariant();

        if (goalLower.Contains("scientist"))
        {
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Python & Data Wrangling", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "Programming", Order = 1,
                Projects = "Clean and merge datasets of housing trends using Pandas and NumPy.",
                InterviewQuestions = "What is the difference between a Python list and a tuple? Why are tuples memory-efficient?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Exploratory Data Analysis", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "Data Wrangling", Order = 2,
                Projects = "Analyze and plot correlations on customer churn metrics using Seaborn.",
                InterviewQuestions = "Explain the difference between covariance and correlation."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Statistics & Probability", WeekRange = "Week 3-4", Difficulty = "Intermediate", ParentSkill = "Math", Order = 3,
                Projects = "Run hypothesis tests and A/B test analysis on landing page conversions.",
                InterviewQuestions = "What is a p-value? How do you explain a Type I versus Type II error?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Supervised Machine Learning", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Modeling", Order = 4,
                Projects = "Train a Scikit-Learn linear regression and decision tree on employee salaries.",
                InterviewQuestions = "What is overfitting, and what are three common techniques to prevent it?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Unsupervised Machine Learning", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Modeling", Order = 5,
                Projects = "Build a K-Means customer segmentation clustering model.",
                InterviewQuestions = "How does the K-Means algorithm work, and how do you choose the optimal number of clusters?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Deep Learning Basics", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Neural Networks", Order = 6,
                Projects = "Train a multi-layer perceptron in PyTorch to classify handwritten digits.",
                InterviewQuestions = "What is the role of activation functions (like ReLU) in neural networks?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "ML Deployment & MLOps", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Deployment", Order = 7,
                Projects = "Deploy the trained model as a FastAPI endpoint dockerized container.",
                InterviewQuestions = "Explain the core pipeline differences between training a model and serving predictions."
            });
        }
        else if (goalLower.Contains("analyst"))
        {
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Advanced Excel Fundamentals", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "Spreadsheets", Order = 1,
                Projects = "Build a sales report dashboard using Pivot Tables, VLOOKUP, and INDEX/MATCH.",
                InterviewQuestions = "How do you handle duplicate values or error cells like #N/A in Excel tables?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "SQL Query Basics", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "Databases", Order = 2,
                Projects = "Query retail database tables to extract total revenue per product line.",
                InterviewQuestions = "What is the difference between WHERE and HAVING in SQL?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Advanced SQL Aggregations", WeekRange = "Week 3-4", Difficulty = "Intermediate", ParentSkill = "Databases", Order = 3,
                Projects = "Calculate year-over-year sales growths using SQL window functions (LAG/LEAD).",
                InterviewQuestions = "Explain what a SQL window function is and give an example."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Tableau / Power BI dashboards", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Visualization", Order = 4,
                Projects = "Create an executive KPI dashboard detailing regional performance trends.",
                InterviewQuestions = "What is the difference between a dimension and a measure in BI systems?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Descriptive Statistics", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Math", Order = 5,
                Projects = "Calculate variance, standard deviation, and IQR outlines for customer orders.",
                InterviewQuestions = "Why is standard deviation preferred over variance for reporting volatility?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Data Reporting & Presenting", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Business", Order = 6,
                Projects = "Write a comprehensive slide deck highlighting retention recommendations.",
                InterviewQuestions = "How do you explain a complex cohort analysis to a non-technical executive?"
            });
        }
        else if (goalLower.Contains("cloud"))
        {
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Cloud Basics & AWS Services", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "AWS", Order = 1,
                Projects = "Set up a static website using S3 bucket, CloudFront HTTPS caching, and Route53.",
                InterviewQuestions = "What is the difference between S3 (Object Storage) and EBS (Block Storage)?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Virtual Networks & VPCs", WeekRange = "Week 3-4", Difficulty = "Intermediate", ParentSkill = "Networking", Order = 2,
                Projects = "Launch EC2 instances within private and public subnets behind an Elastic Load Balancer.",
                InterviewQuestions = "Explain the difference between a security group and a network ACL."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Infrastructure as Code (Terraform)", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "IaC", Order = 3,
                Projects = "Write Terraform configurations deploying a scale-out web application architecture.",
                InterviewQuestions = "What is Terraform state file? Why is it crucial to keep it synchronized?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Docker Containers", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Containers", Order = 4,
                Projects = "Create Dockerfiles wrapping frontend & API layers into loadable images.",
                InterviewQuestions = "What is the difference between a Docker container and a Virtual Machine?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Cloud Security & IAM roles", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Security", Order = 5,
                Projects = "Configure least-privilege IAM policies restricts access to database keys.",
                InterviewQuestions = "What is the principal of least privilege? How do you enforce it in AWS IAM?"
            });
        }
        else if (goalLower.Contains("devops"))
        {
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Linux & Shell Scripting", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "OS", Order = 1,
                Projects = "Write Bash scripts automating server health checks, archiving logs to backups.",
                InterviewQuestions = "How do you check current memory, CPU usage, and network ports in Linux?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Docker & Multi-Stage Builds", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "Containers", Order = 2,
                Projects = "Optimize app deployment footprint using multi-stage Docker builds.",
                InterviewQuestions = "Explain why multi-stage builds are critical in production setups."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "CI/CD Pipeline Design", WeekRange = "Week 3-4", Difficulty = "Intermediate", ParentSkill = "Pipelines", Order = 3,
                Projects = "Set up GitHub Actions to run tests and push verified containers to registry.",
                InterviewQuestions = "What is the difference between continuous delivery and continuous deployment?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Kubernetes Basics", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Orchestration", Order = 4,
                Projects = "Write manifest files defining Pods, Deployments, and LoadBalancer services.",
                InterviewQuestions = "What is a Pod? Why doesn't Kubernetes run containers directly?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Infrastructure Provisioning", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "IaC", Order = 5,
                Projects = "Use Terraform to spawn a cluster across multiple server zones.",
                InterviewQuestions = "Explain the advantages of declaring infrastructure code rather than manual UI setup."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Monitoring & Logging", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Operations", Order = 6,
                Projects = "Configure Prometheus metrics scraper and visual dashboards on Grafana.",
                InterviewQuestions = "What are the four golden signals of service monitoring?"
            });
        }
        else // default .NET Developer
        {
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "C# OOP & Clean Syntax", WeekRange = "Week 1-2", Difficulty = "Beginner", ParentSkill = "C# Core", Order = 1,
                Projects = "Build a console study logger app with user inputs and file storage.",
                InterviewQuestions = "Explain interface inheritance. What is the difference between an abstract class and an interface?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "ASP.NET Core Web API", WeekRange = "Week 3-4", Difficulty = "Intermediate", ParentSkill = "Web APIs", Order = 2,
                Projects = "Create REST API endpoints managing study planner items with validations.",
                InterviewQuestions = "How does middleware work in ASP.NET Core? Describe the request pipeline execution."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Entity Framework Core", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Databases", Order = 3,
                Projects = "Integrate SQLite tables with relational joins and query logging.",
                InterviewQuestions = "Explain lazy loading vs eager loading in EF Core. When would you use AsNoTracking()?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Security & JWT Auth", WeekRange = "Week 5-6", Difficulty = "Intermediate", ParentSkill = "Security", Order = 4,
                Projects = "Implement token authentication and user role protection constraints.",
                InterviewQuestions = "How do you securely store passwords? Describe the concept of password hashing and salting."
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Advanced C# Task Patterns", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "C# Core", Order = 5,
                Projects = "Write asynchronous background processing services executing note parsers.",
                InterviewQuestions = "What is the difference between Task.WhenAll and Task.WaitAll? How do you prevent thread locks?"
            });
            list.Add(new RoadmapItem
            {
                UserId = userId, SkillName = "Docker & Deployment Hosting", WeekRange = "Week 7-8", Difficulty = "Advanced", ParentSkill = "Deployment", Order = 6,
                Projects = "Dockerize the web app and publish it to local environment containers.",
                InterviewQuestions = "What is a multi-stage Dockerfile? How does it benefit ASP.NET Core deployments?"
            });
        }

        return list;
    }

    private class RawRoadmapItem
    {
        public string? SkillName { get; set; }
        public string? WeekRange { get; set; }
        public string? Difficulty { get; set; }
        public string? Projects { get; set; }
        public string? InterviewQuestions { get; set; }
        public string? ParentSkill { get; set; }
    }
}
