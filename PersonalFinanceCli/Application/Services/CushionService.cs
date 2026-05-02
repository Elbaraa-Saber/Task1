using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Application.Services;

public sealed class CushionService
{
    public const string TransferToCushionCategory = "Transfer to cushion";
    public const string TransferFromIncomeCategory = "Transfer from income";

    private readonly ICardRepository _cardRepository;

    public CushionService(ICardRepository cardRepository)
    {
        _cardRepository=cardRepository;
    }

    public Card? FindCushionByName()
    {
        var cards = _cardRepository.GetAll();

        return cards.FirstOrDefault(card => card.Name == "Financial cushion");
    }

    public Card? FindCushionByPartialName()
    {
        var cards = _cardRepository.GetAll();

        return cards.FirstOrDefault(card => 
            card.Name.Contains("cushion", StringComparison.OrdinalIgnoreCase));
    }

    public Card CreateCushion(Currency currency)
    {
        var existingCushionCard=FindCushionByName();

        if (existingCushionCard != null)
        {
            return existingCushionCard;
        }

        return _cardRepository.Add(new Card
        {
            Name="Financial cushion",
            Currency=currency,
            InitialBalance=0m,
            IsDefault=false,
            IsCushion=true
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
