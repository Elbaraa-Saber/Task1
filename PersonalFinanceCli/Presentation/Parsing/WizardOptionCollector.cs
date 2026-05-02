using System.Text.RegularExpressions;

namespace PersonalFinanceCli.Presentation.Parsing;

public sealed class WizardOptionCollector
{
    private static readonly Regex StrictDateRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public WizardOptions Collect(IReadOnlyList<string> tokens, int startIndex)
    {
        string? rawCard = null;
        DateOnly? date = null;
        string? note = null;

        var optionIndex = startIndex;
        while (optionIndex < tokens.Count)
        {
            var option = tokens[optionIndex];
            if (option == "--card")
            {
                optionIndex++;
                rawCard = optionIndex < tokens.Count ? tokens[optionIndex] : null;
                if (string.IsNullOrWhiteSpace(rawCard))
                {
                    return new WizardOptions(null, null, null, "Invalid --card value.");
                }
            }
            else if (option == "--date")
            {
                optionIndex++;
                var dateText = optionIndex < tokens.Count ? tokens[optionIndex] : null;
                if (string.IsNullOrWhiteSpace(dateText) 
                    || !StrictDateRegex.IsMatch(dateText) 
                    || !DateOnly.TryParse(dateText, out var parsedDate))
                {
                    return new WizardOptions(null, null, null, "Invalid --date value. Use strict YYYY-MM-DD.");
                }

                date = parsedDate;
            }
            else if (option == "--note")
            {
                optionIndex++;
                if (optionIndex >= tokens.Count)
                {
                    return new WizardOptions(null, null, null, "Invalid --note value.");
                }

                var noteText = tokens[optionIndex];
                if (!noteText.Contains(' '))
                {
                    return new WizardOptions(null, null, null, "Wizard requires quoted note for --note.");
                }

                note = noteText;
            }
            else
            {
                return new WizardOptions(null, null, null, $"Unknown option {option}.");
            }

            optionIndex++;
        }

        return new WizardOptions(rawCard, date, note, null);
    }
}

