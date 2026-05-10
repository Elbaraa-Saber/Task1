using System.Globalization;
using PersonalFinanceCli.Domain.Services;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Presentation.Rendering;

public sealed class ReportPrinter
{
    private readonly TextWriter _writer;
    private readonly DailyReportService _dailyReportService;

    public ReportPrinter(TextWriter writer, DailyReportService dailyReportService)
    {
        _writer = writer;
        _dailyReportService = dailyReportService;
    }

    public void Print(DailyReport report)
    {
        Print(report, LimitPercentageRounding.Floor);
    }

    public void PrintDayUsingRepositories(DateOnly date)
    {
        var report = _dailyReportService.Generate(date);

        Print(report, LimitPercentageRounding.Round);
    }

    private void Print(DailyReport report, LimitPercentageRounding limitPercentageRounding)
    {
        _writer.WriteLine($"Date: {report.Date:yyyy-MM-dd}");
        _writer.WriteLine($"Income: {FormatMoney(report.Income, report.Currency)}");
        _writer.WriteLine($"Expense: {FormatMoney(report.Expense, report.Currency)}");

        PrintLimit(
            report.Expense,
            report.Limit?.Amount,
            report.Limit?.Currency ?? report.Currency,
            limitPercentageRounding);

        _writer.WriteLine("By category:");

        foreach (var pair in report.CategoryExpenses
            .OrderBy(categoryTotal => categoryTotal.Key, StringComparer.Ordinal))
        {
            _writer.WriteLine($"  {pair.Key}: {FormatMoney(pair.Value, report.Currency)}");
        }

        _writer.WriteLine("Cards:");

        foreach (var card in report.Cards.OrderBy(card => card.CardId))
        {
            var defaultMarker = card.IsDefault ? " (default)" : string.Empty;
            _writer.WriteLine($"  {card.CardName}{defaultMarker}: {FormatMoney(card.Balance, card.Currency)}");
        }
    }

    private void PrintLimit(
        decimal totalExpense,
        decimal? limitAmount,
        Currency currency,
        LimitPercentageRounding percentageRounding)
    {
        if (!limitAmount.HasValue || limitAmount.Value <= 0)
        {
            _writer.WriteLine("Limit: (not set)");
            return;
        }

        var percentage = CalculateLimitPercentage(totalExpense, limitAmount.Value, percentageRounding);
        _writer.WriteLine($"Limit: {FormatMoney(limitAmount.Value, currency)} ({percentage}%)");
    }

    private static int CalculateLimitPercentage(
        decimal totalExpense,
        decimal limitAmount,
        LimitPercentageRounding percentageRounding)
    {
        var percentage = (totalExpense / limitAmount) * 100m;

        return percentageRounding == LimitPercentageRounding.Floor
            ? (int)Math.Floor(percentage)
            : (int)Math.Round(percentage, MidpointRounding.AwayFromZero);
    }

    public static string FormatMoney(decimal amount, Currency currency)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{amount:F2} {currency}");
    }

    private enum LimitPercentageRounding
    {
        Floor,
        Round
    }
}