using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Infrastructure.Time;

namespace PersonalFinanceCli.Application.CommandHandlers;

public sealed class SetDailyLimitHandler
{
    private readonly ILimitRepository _limitRepository;
    private readonly ICardRepository _cardRepository;
    private readonly IClock _clock;

    public SetDailyLimitHandler(
        ILimitRepository limitRepository,
        ICardRepository cardRepository,
        IClock clock)
    {
        _limitRepository = limitRepository;
        _cardRepository = cardRepository;
        _clock = clock;
    }

    public void Handle(decimal dailyLimitAmount)
    {
        if (dailyLimitAmount <= 0)
        {
            throw new InvalidOperationException("Limit must be > 0.");
        }

        var existingCards = _cardRepository.GetAll();
        if (existingCards.Count == 0)
        {
            throw new InvalidOperationException("Cannot set limit without cards.");
        }

        var limitCurrency = _cardRepository.GetDefault()?.Currency
            ?? _cardRepository.GetFirst()?.Currency
            ?? existingCards[0].Currency;

        _limitRepository.Upsert(_clock.Today, dailyLimitAmount, limitCurrency);
    }
}
