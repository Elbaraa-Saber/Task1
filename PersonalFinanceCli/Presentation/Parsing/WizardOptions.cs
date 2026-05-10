namespace PersonalFinanceCli.Presentation.Parsing;

public readonly record struct WizardOptions(
    string? CardRaw,
    DateOnly? Date,
    string? Note,
    string? Error);
