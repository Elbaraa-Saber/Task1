using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Infrastructure.Persistence;

public sealed class JsonLimitRepository : ILimitRepository
{
    private readonly JsonDataStore _store;

    public JsonLimitRepository(JsonDataStore store)
    {
        _store = store;
    }

    public DailyLimit? GetByDate(DateOnly date)
    {
        return _store.Load().DailyLimits.FirstOrDefault(dailyLimit  => dailyLimit .Date == date);
    }

    public DailyLimit Upsert(DateOnly date, decimal dailyLimitAmount, Currency currency)
    {
        var storedData = _store.Load();
        var existingDailyLimit = storedData.DailyLimits.FirstOrDefault(dailyLimit => dailyLimit.Date == date);
        
        if (existingDailyLimit is null)
        {
            existingDailyLimit = new DailyLimit
            {
                Id = storedData.DailyLimits.Count == 0 
                ? 1 
                : storedData.DailyLimits.Max(x => x.Id) + 1,
                Date = date,
                Amount = dailyLimitAmount,
                Currency = currency
            };

            storedData.DailyLimits.Add(existingDailyLimit);
        }
        else
        {
            existingDailyLimit.Amount = dailyLimitAmount;
            existingDailyLimit.Currency = currency;
        }

        _store.Save(storedData);

        return existingDailyLimit;
    }
}
