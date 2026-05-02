using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Domain.Services;

public sealed record CardBalanceLine(
    int CardId,
    string CardName,
    bool IsDefault,
    decimal Balance,
    Currency Currency);
