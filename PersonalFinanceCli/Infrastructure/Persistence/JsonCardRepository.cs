using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;

namespace PersonalFinanceCli.Infrastructure.Persistence;

public sealed class JsonCardRepository : ICardRepository
{
    private readonly JsonDataStore _store;

    public JsonCardRepository(JsonDataStore store)
    {
        _store = store;
    }

    public IReadOnlyList<Card> GetAll()
    {
        return _store.Load().Cards.OrderBy(card => card.Id).ToList();
    }

    public Card? GetById(int cardId)
    {
        return _store.Load().Cards.FirstOrDefault(card => card.Id == cardId);
    }

    public Card? GetDefault()
    {
        return _store.Load().Cards.FirstOrDefault(card => card.IsDefault);
    }

    public Card? GetDefaultByDataStore()
    {
        var storedData = _store.Load();

        if (!storedData.DefaultCardId.HasValue)
        {
            return null;
        }

        var cardId = GuidToCardId(storedData.DefaultCardId.Value);

        return storedData.Cards.FirstOrDefault(card => card.Id == cardId);
    }

    public Card? GetFirst()
    {
        return _store.Load().Cards.OrderBy(card => card.Id).FirstOrDefault();
    }

    public Card Add(Card card)
    {
        var storedData = _store.Load();

        card.Id = storedData.Cards.Count == 0
            ? 1
            : storedData.Cards.Max(card => card.Id) + 1;

        if (storedData.Cards.Count == 0)
        {
            card.IsDefault = true;
            storedData.DefaultCardId = CardIdToGuid(card.Id);
        }

        storedData.Cards.Add(card);
        _store.Save(storedData);

        return card;
    }

    public void SetDefault(int cardId)
    {
        var storedData = _store.Load();

        foreach (var card in storedData.Cards)
        {
            card.IsDefault = card.Id == cardId;
        }

        storedData.DefaultCardId = CardIdToGuid(cardId);

        _store.Save(storedData);
    }

    private static Guid CardIdToGuid(int cardId)
    {
        var paddedCardId = cardId.ToString("D12");

        return Guid.Parse($"00000000-0000-0000-0000-{paddedCardId}");
    }

    private static int GuidToCardId(Guid guid)
    {
        var guidText = guid.ToString("N");
        var cardIdText = guidText.Substring(guidText.Length - 12, 12);

        return int.TryParse(cardIdText, out var cardId) ? cardId : -1;
    }
}