namespace PersonalFinanceCli.Domain.Services;

public static class CardIdConverter
{
    public static Guid ToLegacyGuid(int cardId)
    {
        var paddedCardId = cardId.ToString("D12");

        return Guid.Parse($"00000000-0000-0000-0000-{paddedCardId}");
    }

    public static int ToCardId(Guid guid)
    {
        return TryConvertGuidToCardId(guid) ?? -1;
    }

    public static int? TryParseCardId(string cardArgument)
    {
        var normalizedArgument = cardArgument.Trim();

        if (int.TryParse(normalizedArgument, out var numericId))
        {
            return numericId;
        }

        if (Guid.TryParse(normalizedArgument, out var parsedGuid))
        {
            return TryConvertGuidToCardId(parsedGuid);
        }

        return null;
    }

    private static int? TryConvertGuidToCardId(Guid guid)
    {
        var guidText = guid.ToString("N");
        var cardIdText = guidText[^12..];

        return int.TryParse(cardIdText, out var cardId) ? cardId : null;
    }
}