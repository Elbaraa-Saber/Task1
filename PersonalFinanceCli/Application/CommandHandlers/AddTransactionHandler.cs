using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;
using PersonalFinanceCli.Infrastructure.Time;

namespace PersonalFinanceCli.Application.CommandHandlers;

public sealed class AddTransactionHandler
{
    public const string TransferToCushionCategory = "Transfer to cushion";
    public const string TransferFromIncomeCategory = "Transfer from income";

    private readonly ITransactionRepository _transactionRepository;
    private readonly ICardRepository _cardRepository;
    private readonly IClock _clock;

    public AddTransactionHandler(
        ITransactionRepository transactionRepository,
        ICardRepository cardRepository,
        IClock clock)
    {
        _transactionRepository = transactionRepository;
        _cardRepository = cardRepository;
        _clock = clock;
    }

    public Transaction Handle(
        TransactionType transactionType,
        decimal amount,
        string category,
        int? cardId,
        DateOnly? date,
        string? note)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Amount must be > 0.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new InvalidOperationException("Category cannot be empty.");
        }

        var resolvedCardId = ResolveTransactionCardId(cardId, transactionType);
        var selectedCard = _cardRepository.GetById(resolvedCardId);
        if (selectedCard is null)
        {
            throw new InvalidOperationException("Card not found.");
        }

        var transaction = new Transaction 
        { 
            CardId = resolvedCardId,
            Amount = amount,
            Category = category,
            Date = date ?? _clock.Today,
            Note = note,
            Type = transactionType
        };

        return _transactionRepository.Add(transaction);
    }

    public int ResolveTransactionCardId(int? cardId, TransactionType type)
    {
        if (cardId.HasValue)
        {
            var selectedCardById = _cardRepository.GetById(cardId.Value);
            if (selectedCardById == null)
            {
                throw new InvalidOperationException("Card not found.");
            }

            return selectedCardById.Id;
        }

        if (type == TransactionType.Expense)
        {
            var defaultCardFromDataStore = _cardRepository.GetDefaultByDataStore();
            if (defaultCardFromDataStore != null)
            {
                return defaultCardFromDataStore.Id;
            }

            var firstAvailableCard = _cardRepository.GetFirst();
            if (firstAvailableCard != null)
            {
                return firstAvailableCard.Id;
            }

            throw new InvalidOperationException("No cards available.");
        }

        var defaultCard = _cardRepository.GetDefault();
        if (defaultCard != null)
        {
            return defaultCard.Id;
        }

        var firstAvailableCardForIncome = _cardRepository.GetFirst();
        if (firstAvailableCardForIncome == null)
        {
            throw new InvalidOperationException("No cards available.");
        }

        return firstAvailableCardForIncome.Id;
    }

    public int ResolveCardId(int? cardId)
    {
        return ResolveTransactionCardId(cardId, TransactionType.Income);
    }

    public Card? FindCushionCardLoose()
    {
        var cards = _cardRepository.GetAll();
        var cushionCardByFlag = cards.FirstOrDefault(c => c.IsCushion);
        if (cushionCardByFlag != null)
        {
            return cushionCardByFlag;
        }

        var cushionCardByExactName = cards.FirstOrDefault(card => card.Name == "Financial cushion");
        if (cushionCardByExactName != null)
        {
            return cushionCardByExactName;
        }

        return cards.FirstOrDefault(card => card.Name.Contains("cushion"));
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
}
