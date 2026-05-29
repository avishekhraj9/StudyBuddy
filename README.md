# 🎓 AI Study Buddy

**AI Study Buddy** is a modern, responsive web application designed to help students accelerate their learning. It combines document parsing, artificial intelligence, active recall quizzes, spaced repetition vocabulary reviews, personalized study planners, and structured career roadmaps with educational resource recommendation hubs.

---

## ✨ Features

### 1. 📂 Study Materials & Document Parser
* **Drag-and-Drop Uploader:** Supports uploading PDF, DOCX, and TXT files up to 10MB.
* **Text Extraction:** Uses `UglyToad.PdfPig` to extract text page-by-page from PDFs and OpenXML for Word files.
* **Instant AI Summarization:** Generates clean, chapter-by-chapter summaries and key concept lists on upload.

### 2. 🤖 Interactive Chat with Notes
* Select any document and ask contextual questions.
* The system utilizes vector search concept queries or targeted document text contexts to deliver grounded answers.

### 3. 🎯 Active Recall Quizzes
* Generate mock examinations directly from uploaded notes.
* Supports **MCQs, True/False, and Fill-in-the-Blanks**.
* Renders real-time grading, immediate answers feedback, and detailed AI explanations.

### 4. 🧠 Spaced Repetition Flashcards
* Smart flashcard generator extracting key terms and definitions.
* Integrates a spaced repetition scoring scheduler (review boxes 0–5) to optimize vocabulary memorization.

### 5. 📅 Study Planner & Calendar Countdown
* Create customized goals (Daily Goals, Weekly Schedules, Exam Countdowns).
* Integrated dashboard countdown calendar tracking your midterms/exams.

### 6. 🗺️ AI Career Roadmap
* Pick standard career tracks (e.g. *.NET Developer, Data Scientist, Data Analyst, Cloud Engineer, DevOps Engineer*).
* Generates structured 8-week timelines detailing week-by-week skill requirements, hands-on practice projects, and target mock interview prep questions.
* Real-time **Job Readiness Score** tracking milestones.
* **Study Recommendations Alert:** The system automatically analyzes your quiz histories and flags weak scores (below 70%) to recommend areas on your roadmap that need review.

### 7. 📺 YouTube recommendations & Resource Hub
* Dynamically fetches matching tutorials using YouTube Data API v3 or falls back to a pre-curated catalog.
* Displays related official documentation links, practice coding challenges, and mini-projects.
* **Progress Tracking:** Toggle completion checkmarks on tutorials, synced dynamically across both Note Summary and Career Roadmap views.
* **Planner Integration:** Click "Start" on any practice challenge to automatically schedule it as a goal on your Study Planner!

---

## 🛠️ Tech Stack

* **Backend:** ASP.NET Core Web API (C# .NET 10)
* **Database:** Entity Framework Core with SQLite
* **AI Integration:** OpenAI API (GPT models)
* **Frontend:** Responsive vanilla HTML5, CSS3 Variables (sleek deep-slate theme with glowing violet glassmorphic panels), and vanilla JavaScript.
* **Dependencies:** `UglyToad.PdfPig` (PDF), `DocumentFormat.OpenXml` (Word), `Microsoft.EntityFrameworkCore.Sqlite`

---

## 🚀 Getting Started

### Prerequisites
* Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Setup & Run
1. Clone the repository to your local system:
   ```bash
   git clone <your-repo-url>
   cd AIStudyBuddy
   ```
2. Build and run the project:
   ```bash
   dotnet run
   ```
3. Open your browser and navigate to:
   ```text
   http://localhost:5013
   ```
4. **Configure custom API Keys (Optional):**
   * Register a new account and head to the **Settings** view.
   * Enter your custom OpenAI API Key. (If no key is configured, the application runs on built-in mock heuristic fallbacks).

---

## 💻 Git commands to push on GitHub

If you haven't linked the project yet:
```bash
# Link your repository
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO_NAME.git

# Commit README
git add README.md
git commit -m "docs: Add project README description"

# Push to GitHub
git push -u origin main
```
