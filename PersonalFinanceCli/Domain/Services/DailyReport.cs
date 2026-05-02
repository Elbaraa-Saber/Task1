using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Domain.Services;
public sealed record DailyReport(
    DateOnly Date,
    Currency Currency,
    decimal Income,
    decimal Expense,
    IReadOnlyDictionary<string, decimal> CategoryExpenses,
    IReadOnlyList<CardBalanceLine> Cards,
    DailyLimit? Limit);
