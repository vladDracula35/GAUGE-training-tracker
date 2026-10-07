using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using GAUGE.Models;

namespace GAUGE.Services;

public static class MarkdownExportService
{
    public static string GenerateWorkoutMarkdown(string title, IEnumerable<WorkoutSetModel> sets)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);

        int setNumber = 1;
        foreach (var set in sets)
        {
            sb.AppendLine($"Підхід {setNumber++}");

            foreach (var ex in set.Exercises)
            {
                var line = $"{ex.Name}: {ex.Reps} повт. (Відпочинок: {ex.RestSeconds}s)";
                if (!string.IsNullOrWhiteSpace(ex.Notes))
                {
                    line += $"*Відчуття: {ex.Notes.Trim()}";
                }
                sb.AppendLine(line);
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static async Task<bool> ExportToMarkdownAsync(string title, IEnumerable<WorkoutSetModel> sets, IStorageProvider storageProvider)
    {
        try
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Зберегти тренування в Obsidian",
                DefaultExtension = "md",
                SuggestedFileName = $"{title.Replace(":", "-").Replace("/", "-")}.md",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Markdown Note") { Patterns = new[] { "*.md" } }
                }
            });

            if (file == null) return false;

            var content = GenerateWorkoutMarkdown(title, sets);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(content);

            return true;
        }
        catch
        {
            return false;
        }
    }
}