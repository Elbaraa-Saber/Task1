using System.Text.Json;
using PersonalFinanceCli.Domain.Entities;

namespace PersonalFinanceCli.Infrastructure.Persistence;

public sealed class JsonDataStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonDataStore(string filePath)
    {
        _filePath = filePath;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public DataFile Load()
    {
        if (!File.Exists(_filePath))
        {
            var emptyDataFile = new DataFile();
            Save(emptyDataFile);
            return emptyDataFile;
        }

        var jsonContent = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            var emptyDataFile = new DataFile();
            Save(emptyDataFile);
            return emptyDataFile;
        }

        var dataFile = JsonSerializer.Deserialize<DataFile>(jsonContent, _serializerOptions);
        if (dataFile == null)
        {
            var emptyDataFile = new DataFile();
            Save(emptyDataFile);
            return emptyDataFile;
        }

        dataFile.Cards ??= new List<Card>();
        dataFile.Transactions ??= new List<Transaction>();
        dataFile.DailyLimits ??= new List<DailyLimit>();

        return dataFile;
    }

    public void Save(DataFile data)
    {
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var jsonContent = JsonSerializer.Serialize(data, _serializerOptions);
        File.WriteAllText(_filePath, jsonContent);
    }
}