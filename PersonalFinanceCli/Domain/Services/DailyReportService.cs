using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Domain.Services;

public sealed class DailyReportService
{
    private readonly ICardRepository _cardRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILimitRepository _limitRepository;

    public DailyReportService(
        ICardRepository cardRepository,
        ITransactionRepository transactionRepository,
        ILimitRepository limitRepository)
    {
        _cardRepository = cardRepository;
        _transactionRepository = transactionRepository;
        _limitRepository = limitRepository;
    }

    public DailyReport Generate(DateOnly date)
    {
        var cards = _cardRepository.GetAll();
        var reportCurrency = cards.FirstOrDefault(card => card.IsDefault)?.Currency
            ?? cards.FirstOrDefault()?.Currency
            ?? Currency.RUB;

        var cardIdsInReportCurrency = cards
            .Where(card => card.Currency == reportCurrency).Select(card => card.Id).ToHashSet();
        var allTransactions = _transactionRepository.GetAll();

        decimal totalIncome = 0m;
        decimal totalExpense = 0m;
        var categoryExpenseTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var transaction in allTransactions)
        {
            if (!cardIdsInReportCurrency.Contains(transaction.CardId) || transaction.Date != date)
            {
                continue;
            }

            if (transaction.Type == TransactionType.Income)
            {
                totalIncome += transaction.Amount;
            }
            else
            {
                totalExpense += transaction.Amount;
                if (categoryExpenseTotals.ContainsKey(transaction.Category))
                {
                    categoryExpenseTotals[transaction.Category] += transaction.Amount;
                }
                else
                {
                    categoryExpenseTotals[transaction.Category] = transaction.Amount;
                }
            }
        }

        var dailyLimit = _limitRepository.GetByDate(date);
        var limitUsagePercentage = 0;
        if (dailyLimit is { Amount: > 0 })
        {
            limitUsagePercentage = (int)((totalExpense / dailyLimit.Amount) * 100m);
        }

        if (limitUsagePercentage < 0)
        {
            limitUsagePercentage = 0;
        }

        var cardBalanceLines = new List<CardBalanceLine>();
        foreach (var card in cards)
        {
            decimal balance = card.InitialBalance;
            foreach (var transaction in allTransactions.Where(x => x.CardId == card.Id))
            {
                if (transaction.Type == TransactionType.Income)
                {
                    balance += transaction.Amount;
                }
                else
                {
                    balance -= transaction.Amount;
                }
            }

            cardBalanceLines.Add(new CardBalanceLine(
                card.Id,
                card.Name,
                card.IsDefault,
                balance,
                card.Currency));
        }

        return new DailyReport(
            date,
            reportCurrency,
            totalIncome,
            totalExpense,
            categoryExpenseTotals,
            cardBalanceLines,
            dailyLimit);
    }
}

