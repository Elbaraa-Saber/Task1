using PersonalFinanceCli.Application.Repositories;

namespace PersonalFinanceCli.Infrastructure.Persistence;

public sealed class JsonOnboardingStateRepository : IOnboardingStateRepository
{
    private readonly JsonDataStore _store;

    public JsonOnboardingStateRepository(JsonDataStore store)
    {
        _store = store;
    }

    public DateOnly? GetLastCushionDeclinedDate()
    {
        return _store.Load().LastCushionDeclinedDate;
    }

    public void SetLastCushionDeclinedDate(DateOnly? lastCushionDeclinedDate)
    {
        var storedData = _store.Load();
        storedData.LastCushionDeclinedDate = lastCushionDeclinedDate;
        _store.Save(storedData);
    }

    public bool GetHasSeenOnboarding()
    {
        return _store.Load().HasSeenOnboarding;
    }

    public void SetHasSeenOnboarding(bool hasSeenOnboarding)
    {
        var data = _store.Load();
        data.HasSeenOnboarding = hasSeenOnboarding;
        _store.Save(data);
    }
}
