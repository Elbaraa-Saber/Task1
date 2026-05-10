using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;
using PersonalFinanceCli.Infrastructure.Time;

namespace PersonalFinanceCli.Application.Services;

public sealed class CushionService
{
    public const string TransferToCushionCategory = "Transfer to cushion";
    public const string TransferFromIncomeCategory = "Transfer from income";
    private const string CushionCardName = "Financial cushion";

    private readonly ICardRepository _cardRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IClock _clock;

    public CushionService(
        ICardRepository cardRepository,
        ITransactionRepository transactionRepository,
        IClock clock)
    {
        _cardRepository = cardRepository;
        _transactionRepository = transactionRepository;
        _clock = clock;
    }

    public Card? FindCushionByExactName()
    {
        var cards = _cardRepository.GetAll();

        return cards.FirstOrDefault(card => card.Name == CushionCardName);
    }

    public Card? FindCushionByNameContainingCushion()
    {
        var cards = _cardRepository.GetAll();

        return cards.FirstOrDefault(card => 
            card.Name.Contains("cushion", StringComparison.OrdinalIgnoreCase));
    }

    public Card CreateCushion(Currency currency)
    {
        var existingCushionCard = FindCushionByExactName();

        if (existingCushionCard != null)
        {
            return existingCushionCard;
        }

        return _cardRepository.Add(new Card
        {
            Name= CushionCardName,
            Currency=currency,
            InitialBalance=0m,
            IsDefault=false,
            IsCushion=true
        });
    }

    public Card? FindCushionCardLoose()
    {
        var cards = _cardRepository.GetAll();

        return cards.FirstOrDefault(card => card.IsCushion)
            ?? cards.FirstOrDefault(card => card.Name == CushionCardName)
            ?? cards.FirstOrDefault(card =>
                card.Name.Contains("cushion", StringComparison.OrdinalIgnoreCase));
    }

    public void AddTransferPair(int fromCardId, int cushionCardId, decimal amount, DateOnly? date)
    {
        var transferDate = date ?? _clock.Today;

        _transactionRepository.Add(new Transaction
        {
            CardId = fromCardId,
            Amount = amount,
            Category = TransferToCushionCategory,
            Date = transferDate,
            Note = "auto",
            Type = TransactionType.Expense
        });

        _transactionRepository.Add(new Transaction
        {
            CardId = cushionCardId,
            Amount = amount,
            Category = TransferFromIncomeCategory,
            Date = transferDate,
            Note = "auto",
            Type = TransactionType.Income
        });
    }

    public decimal CalculateDefaultTransferAmount(decimal incomeAmount, string category)
    {
        var isSalaryCategory = category.Contains("Salary", StringComparison.OrdinalIgnoreCase);

        if(incomeAmount < 10m)
        {
            if (isSalaryCategory)
            {
                return 1m;
            }

            return 1m;
        }
        else
        {
            if (isSalaryCategory)
            {
                return FloorToTwoDecimalPlaces(incomeAmount * 0.20m);
            }

            return FloorToTwoDecimalPlaces(incomeAmount * 0.10m);
        }
    }

    public static decimal FloorToTwoDecimalPlaces(decimal value)
    {
        return Math.Floor(value * 100m) / 100m;
    }
}
