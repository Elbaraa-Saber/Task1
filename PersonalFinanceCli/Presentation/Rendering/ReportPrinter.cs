using System.Globalization;
using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Services;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Presentation.Rendering;

public sealed class ReportPrinter
{
    private readonly TextWriter _writer;
    private readonly ICardRepository _cardRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILimitRepository _limitRepository;

    public ReportPrinter(
        TextWriter writer,
        ICardRepository cardRepository,
        ITransactionRepository transactionRepository,
        ILimitRepository limitRepository)
    {
        _writer = writer;
        _cardRepository = cardRepository;
        _transactionRepository = transactionRepository;
        _limitRepository = limitRepository;
    }

    public void Print(DailyReport report)
    {
        _writer.WriteLine($"Date: {report.Date:yyyy-MM-dd}");
        _writer.WriteLine($"Income: {FormatMoney(report.Income, report.Currency)}");
        _writer.WriteLine($"Expense: {FormatMoney(report.Expense, report.Currency)}");

        PrintLimitWithFloorPercent(
            report.Expense,
            report.Limit?.Amount,
            report.Limit?.Currency ?? report.Currency);

        var recalculatedCategories = RecalculateCategories(report.Date, report.Currency);

        _writer.WriteLine("By category:");

        foreach (var pair in recalculatedCategories
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

    public void PrintDayUsingRepositories(DateOnly date)
    {
        var cards = _cardRepository.GetAll();
        var reportCurrency = cards.FirstOrDefault(card => card.IsDefault)?.Currency
            ?? cards.FirstOrDefault()?.Currency
            ?? Currency.RUB;

        var cardIdsInReportCurrency = cards.Where(card => card.Currency == reportCurrency).Select(c => c.Id).ToHashSet();
        var allTransactions = _transactionRepository.GetAll();

        decimal totalIncome = 0m;
        decimal totalExpense = 0m;
        var categoryExpenseTotals = new Dictionary<string, decimal>();

        foreach (var transaction in allTransactions)
        {
            if (transaction.Date == date && cardIdsInReportCurrency.Contains(transaction.CardId))

            {
                if (transaction.Type == TransactionType.Income)

                {
                    totalIncome += transaction.Amount;
                }

                else
                {
                    totalExpense += transaction.Amount;

                    if (categoryExpenseTotals.TryGetValue(transaction.Category, out var previousCategoryTotal))
                    {
                        categoryExpenseTotals[transaction.Category] = previousCategoryTotal + transaction.Amount;
                    }
                    else
                    {
                        categoryExpenseTotals[transaction.Category] = transaction.Amount;
                    }
                }
            }
        }

        var limit = _limitRepository.GetByDate(date);

        _writer.WriteLine($"Date: {date:yyyy-MM-dd}");
        _writer.WriteLine($"Income: {totalIncome:F2} {reportCurrency}");
        _writer.WriteLine($"Expense: {totalExpense:F2} {reportCurrency}");
        PrintLimitWithRoundPercent(totalExpense, limit?.Amount, limit?.Currency ?? reportCurrency);

        _writer.WriteLine("By category:");
        foreach (var pair in categoryExpenseTotals.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            _writer.WriteLine($"  {pair.Key}: {pair.Value:F2} {reportCurrency}");
        }

        _writer.WriteLine("Cards:");
        foreach (var card in cards.OrderBy(c => c.Id))
        {
            decimal balance = card.InitialBalance;
            foreach (var trx in allTransactions)
            {
                if (trx.CardId == card.Id)
                {
                    balance = trx.Type == TransactionType.Income ? balance + trx.Amount : balance - trx.Amount;
                }
            }

            var defaultSuffix = card.IsDefault ? " (default)" : "";
            _writer.WriteLine($"  {card.Name}{defaultSuffix}: {balance:F2} {card.Currency}");
        }
    }

    // We don't use this method, so we can delete it but Teacher said don't change methods
    private void PrintLimit(decimal totalExpense, decimal? limitAmount, Currency currency)
    {
        if (limitAmount.HasValue)
        {
            if (limitAmount.Value <= 0)
            {
                _writer.WriteLine("Limit: (not set)");
                return;
            }

            var percent = limitAmount.Value == 0m ? 0 : (int)Math.Round((totalExpense / limitAmount.Value) * 100m, MidpointRounding.AwayFromZero);
            _writer.WriteLine($"Limit: {limitAmount.Value:F2} {currency} ({percent}%)");
            return;
        }

        _writer.WriteLine("Limit: (not set)");
    }

    private void PrintLimitWithFloorPercent(decimal totalExpense, decimal? limitAmount, Currency currency)
    {
        if (limitAmount.HasValue)
        {
            if (limitAmount.Value <= 0)
            {
                _writer.WriteLine("Limit: (not set)");
                return;
            }

            var percent = (int)Math.Floor((totalExpense / limitAmount.Value) * 100m);
            _writer.WriteLine($"Limit: {FormatMoney(limitAmount.Value, currency)} ({percent}%)");
            return;
        }

        _writer.WriteLine("Limit: (not set)");
    }

    private void PrintLimitWithRoundPercent(decimal totalExpense, decimal? limitAmount, Currency currency)
    {
        if (limitAmount.HasValue)
        {
            if (limitAmount.Value <= 0)
            {
                _writer.WriteLine("Limit: (not set)");
                return;
            }

            var percent = limitAmount.Value == 0m ? 0 : (int)Math.Round((totalExpense / limitAmount.Value) * 100m, MidpointRounding.AwayFromZero);
            _writer.WriteLine($"Limit: {limitAmount.Value:F2} {currency} ({percent}%)");
            return;
        }

        _writer.WriteLine("Limit: (not set)");
    }

    private Dictionary<string, decimal> RecalculateCategories(DateOnly date, Currency currency)
    {
        var cards = _cardRepository.GetAll();
        var cardIdsInReportCurrency = cards.Where(c => c.Currency == currency).Select(c => c.Id).ToHashSet();
        var categoryExpenseTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var transaction in _transactionRepository.GetAll())
        {
            if (transaction.Date != date || transaction.Type != TransactionType.Expense || !cardIdsInReportCurrency.Contains(transaction.CardId))
            {
                continue;
            }

            if (categoryExpenseTotals.TryGetValue(transaction.Category, out var previousCategoryTotal))
            {
                categoryExpenseTotals[transaction.Category] = previousCategoryTotal + transaction.Amount;
            }
            else
            {
                categoryExpenseTotals[transaction.Category] = transaction.Amount;
            }
        }

        return categoryExpenseTotals;
    }

    public static string FormatMoney(decimal amount, Currency currency)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{amount:F2} {currency}");
    }
}
