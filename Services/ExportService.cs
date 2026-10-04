using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Ripple.Models;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace Ripple.Services;

public sealed class ExportService : IDocumentExporter
{
    public string BuildMarkdown(Lecture lecture, bool includeTranscript = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {lecture.Title}");
        sb.AppendLine();
        sb.AppendLine($"_Дата: {lecture.CreatedAt:dd.MM.yyyy HH:mm} · Длительность: {lecture.DurationText}_");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(lecture.Summary))
        {
            sb.AppendLine("## Конспект");
            sb.AppendLine();
            sb.AppendLine(lecture.Summary.Trim());
            sb.AppendLine();
        }

        if (includeTranscript && lecture.Segments.Count > 0)
        {
            sb.AppendLine("## Дословное содержание");
            sb.AppendLine();
            foreach (var seg in lecture.Segments)
                sb.AppendLine($"**[{seg.Timestamp}]** {seg.Text.Trim()}");
        }

        return sb.ToString();
    }

    public void ExportMarkdown(Lecture lecture, string path, bool includeTranscript = true)
    {
        return File.WriteAllText(path, BuildMarkdown(lecture, includeTranscript));
    }

    public void ExportDocx(Lecture lecture, string path, bool includeTranscript = true)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        body.AppendChild(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Title" }),
            new Run(new Text(lecture.Title) { Space = SpaceProcessingModeValues.Preserve })));

        body.AppendChild(new Paragraph(new Run(new Text(
            $"Дата: {lecture.CreatedAt:dd.MM.yyyy HH:mm} · Длительность: {lecture.DurationText}"))));

        if (!string.IsNullOrWhiteSpace(lecture.Summary))
        {
            body.AppendChild(Heading("Конспект"));

            foreach (var line in lecture.Summary.Split('\n'))
            {
                var text = line.TrimEnd('\r');

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                AppendMdLine(body, text);
            }
        }

        if (includeTranscript && lecture.Segments.Count > 0)
        {
            body.AppendChild(Heading("Дословное содержание"));

            foreach (var seg in lecture.Segments)
            {
                var p = new Paragraph();
                p.AppendChild(new Run(
                    new RunProperties(new Bold()),
                    new Text($"[{seg.Timestamp}]") { Space = SpaceProcessingModeValues.Preserve }));
                p.AppendChild(new Run(new Text(" " + seg.Text.Trim())
                { Space = SpaceProcessingModeValues.Preserve }));
                body.AppendChild(p);
            }
        }
    }

    private static Paragraph Heading(string text)
    {
        return new(
        new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
        new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static void AppendMdLine(Body body, string line)
    {
        if (Regex.IsMatch(line, @"^[-*] "))
        {
            var p = new Paragraph(
                new ParagraphProperties(new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 1 })));

            foreach (var el in MdRuns(line[2..]))
            {
                p.AppendChild(el);
            }

            body.AppendChild(p);

            return;
        }
        if (Regex.IsMatch(line, @"^\d+\. "))
        {
            var p = new Paragraph(
                new ParagraphProperties(new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 2 })));

            foreach (var el in MdRuns(line[Regex.Match(line, @"^\d+\. ").Length..]))
            {
                p.AppendChild(el);
            }

            body.AppendChild(p);

            return;
        }

        var h = Regex.Match(line, @"^#{2,4}\s+(.*)$");
        if (h.Success)
        {
            body.AppendChild(Heading(h.Groups[1].Value));

            return;
        }
        var para = new Paragraph();

        foreach (var el in MdRuns(line))
        {
            para.AppendChild(el);
        }

        body.AppendChild(para);
    }

    private static OpenXmlElement[] MdRuns(string text)
    {
        var runs = new List<OpenXmlElement>();
        var pattern = new Regex(@"(\*\*.+?\*\*|`.+?`)");

        foreach (var part in pattern.Split(text))
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
            {
                runs.Add(new Run(
                    new RunProperties(new Bold()),
                    new Text(part[2..^2]) { Space = SpaceProcessingModeValues.Preserve }));
            }

            else if (part.StartsWith('`') && part.EndsWith('`') && part.Length > 2)
            {
                runs.Add(new Run(
                    new RunProperties(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }),
                    new Text(part[1..^1]) { Space = SpaceProcessingModeValues.Preserve }));
            }

            else
            {
                runs.Add(new Run(new Text(part) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }

        return runs.ToArray();
    }

    internal static string StripMd(string text)
    {
        var t = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        t = Regex.Replace(t, @"`(.+?)`", "$1");
        t = Regex.Replace(t, @"\*(.+?)\*", "$1");

        return t.Trim();
    }
}