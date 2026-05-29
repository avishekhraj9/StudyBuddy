using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace AIStudyBuddy.Services;

public class PdfService
{
    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => ExtractFromPdf(filePath),
            ".docx" => ExtractFromDocx(filePath),
            ".txt" => await File.ReadAllTextAsync(filePath, Encoding.UTF8),
            _ => throw new NotSupportedException($"File extension {extension} is not supported.")
        };
    }

    private string ExtractFromPdf(string filePath)
    {
        var text = new StringBuilder();
        try
        {
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    text.AppendLine(page.Text);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error reading PDF: {ex.Message}", ex);
        }
        return text.ToString();
    }

    private string ExtractFromDocx(string filePath)
    {
        try
        {
            using (var document = WordprocessingDocument.Open(filePath, false))
            {
                var body = document.MainDocumentPart?.Document?.Body;
                return body?.InnerText ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error reading Word document: {ex.Message}", ex);
        }
    }
}
