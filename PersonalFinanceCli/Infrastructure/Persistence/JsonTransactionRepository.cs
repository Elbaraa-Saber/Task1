using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Domain.Entities;

namespace PersonalFinanceCli.Infrastructure.Persistence;

public sealed class JsonTransactionRepository : ITransactionRepository
{
    private readonly JsonDataStore _store;

    public JsonTransactionRepository(JsonDataStore store)
    {
        _store = store;
    }

    public IReadOnlyList<Transaction> GetAll()
    {
        return _store.Load().Transactions.OrderBy(t => t.Id).ToList();
    }

    public Transaction Add(Transaction transaction)
    {
        var storedData = _store.Load();
        transaction.Id = storedData.Transactions.Count == 0 ? 1 : storedData.Transactions.Max(transaction => transaction.Id) + 1;
        storedData.Transactions.Add(transaction);
        _store.Save(storedData);
        return transaction;
    }
}
