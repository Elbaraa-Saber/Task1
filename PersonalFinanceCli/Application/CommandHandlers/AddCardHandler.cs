using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Application.CommandHandlers;

public sealed class AddCardHandler
{
    private readonly ICardRepository _cardRepository;

    public AddCardHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public Card Handle(string newCardName, string currencyRaw, decimal? initialBalance)
    {
        if (string.IsNullOrWhiteSpace(newCardName))
        {
            throw new InvalidOperationException("Card name cannot be empty.");
        }
        if (!Enum.TryParse<Currency>(currencyRaw, true, out var currency))
        {
            throw new InvalidOperationException("Unknown currency. Allowed: RUB, EUR.");
        }
        var isFirstCard = _cardRepository.GetAll().Count == 0;

        var newCard = new Card
        {
            Name = newCardName,
            Currency = currency,
            InitialBalance = initialBalance ?? 0m,
            IsDefault = isFirstCard
        };

        return _cardRepository.Add(newCard);
    }
}
