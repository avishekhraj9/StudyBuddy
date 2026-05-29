namespace AIStudyBuddy.Models;

public class Question
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty; // "A", "B", "C", "D"
    public string QuestionType { get; set; } = "MCQ"; // MCQ, TrueFalse, FillInTheBlank
    public string Explanation { get; set; } = string.Empty;
}
