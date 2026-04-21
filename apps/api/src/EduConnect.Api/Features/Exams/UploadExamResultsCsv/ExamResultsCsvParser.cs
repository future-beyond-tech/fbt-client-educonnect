using System.Globalization;
using System.Text;
using EduConnect.Api.Features.Exams.UpsertExamResults;

namespace EduConnect.Api.Features.Exams.UploadExamResultsCsv;

/// <summary>
/// Parses an uploaded results CSV into the same row shape the manual-upsert
/// handler consumes. The expected header row is:
///
///   roll_number, subject, marks_obtained, grade, is_absent, remarks
///
/// Roll number and subject name are used to resolve StudentId + ExamSubjectId
/// at a higher layer (the handler); the parser only structures the text.
/// </summary>
public static class ExamResultsCsvParser
{
    public sealed record ParsedRow(
        int LineNumber,
        string RollNumber,
        string Subject,
        decimal? MarksObtained,
        string? Grade,
        string? Remarks,
        bool IsAbsent);

    public sealed record ParseResult(
        List<ParsedRow> Rows,
        List<string> Errors);

    public static ParseResult Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var rows = new List<ParsedRow>();
        var errors = new List<string>();

        string? headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            errors.Add("CSV is empty.");
            return new ParseResult(rows, errors);
        }

        var headers = SplitCsvLine(headerLine)
            .Select(h => h.Trim().ToLowerInvariant())
            .ToList();

        int rollIdx = headers.IndexOf("roll_number");
        int subjectIdx = headers.IndexOf("subject");
        int marksIdx = headers.IndexOf("marks_obtained");
        int gradeIdx = headers.IndexOf("grade");
        int absentIdx = headers.IndexOf("is_absent");
        int remarksIdx = headers.IndexOf("remarks");

        if (rollIdx < 0 || subjectIdx < 0)
        {
            errors.Add("CSV must include 'roll_number' and 'subject' columns.");
            return new ParseResult(rows, errors);
        }

        int lineNo = 1; // header was line 1
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = SplitCsvLine(line);

            string Safe(int idx)
            {
                if (idx < 0 || idx >= cols.Count) return string.Empty;
                return cols[idx]?.Trim() ?? string.Empty;
            }

            var roll = Safe(rollIdx);
            var subject = Safe(subjectIdx);
            var marksRaw = Safe(marksIdx);
            var grade = Safe(gradeIdx);
            var absentRaw = Safe(absentIdx);
            var remarks = Safe(remarksIdx);

            if (string.IsNullOrWhiteSpace(roll) || string.IsNullOrWhiteSpace(subject))
            {
                errors.Add($"Line {lineNo}: roll_number and subject are required.");
                continue;
            }

            bool isAbsent = false;
            if (!string.IsNullOrWhiteSpace(absentRaw))
            {
                var normalized = absentRaw.Trim().ToLowerInvariant();
                isAbsent = normalized is "1" or "true" or "yes" or "y" or "absent";
            }

            decimal? marks = null;
            if (!string.IsNullOrWhiteSpace(marksRaw))
            {
                if (decimal.TryParse(marksRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMarks))
                {
                    marks = parsedMarks;
                }
                else
                {
                    errors.Add($"Line {lineNo}: marks_obtained '{marksRaw}' is not a valid number.");
                    continue;
                }
            }

            if (isAbsent && marks.HasValue)
            {
                errors.Add($"Line {lineNo}: row is marked absent but also carries marks.");
                continue;
            }

            if (!isAbsent && !marks.HasValue && string.IsNullOrWhiteSpace(grade))
            {
                errors.Add($"Line {lineNo}: row has no marks, grade, or absent flag.");
                continue;
            }

            rows.Add(new ParsedRow(
                LineNumber: lineNo,
                RollNumber: roll,
                Subject: subject,
                MarksObtained: marks,
                Grade: string.IsNullOrWhiteSpace(grade) ? null : grade,
                Remarks: string.IsNullOrWhiteSpace(remarks) ? null : remarks,
                IsAbsent: isAbsent));
        }

        return new ParseResult(rows, errors);
    }

    /// <summary>
    /// Split one CSV line honoring double-quote wrapping and "" escapes.
    /// Intentionally simple — we don't need multi-line field support here
    /// because the expected shape is one row per line.
    /// </summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"' && sb.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
