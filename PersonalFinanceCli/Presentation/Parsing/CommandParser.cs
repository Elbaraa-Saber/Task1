using System.Globalization;
using System.Text.RegularExpressions;
using PersonalFinanceCli.Domain.ValueObjects;

namespace PersonalFinanceCli.Presentation.Parsing;

public sealed class CommandParser
{
    private const string Card = "card";
    private const string Expense = "expense";
    private const string Income = "income";
    private const string Limit = "limit";
    private const string Report = "report";

    public ParsedCommand Parse(string[] args)
    {
        return Parse(args.ToList());
    }

    public ParsedCommand Parse(string line)
    {
        return Parse(Tokenizer.Tokenize(line));
    }

    private ParsedCommand Parse(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("Command is empty.");
        }

        var commandName = tokens[0].ToLowerInvariant();
        
        if (commandName == Card) 
        {
            return ParseCard(tokens);
        }

        if (commandName == Expense || commandName == Income)
        {
            return ParseTransaction(
                tokens,
                commandName == Income ? TransactionType.Income : TransactionType.Expense);
        }

        if (commandName == Limit)
        {
            return ParseLimit(tokens);
        }

        if (commandName == Report)
        {
            return ParseReport(tokens);
        }

        throw new InvalidOperationException("Unknown command.");
    }

    private static ParsedCommand ParseCard(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException("Card command is incomplete.");
        }

        var cardAction = tokens[1].ToLowerInvariant();
        if (cardAction == "add")
        {
            if (tokens.Count < 4)
            {
                throw new InvalidOperationException("card add requires: card add \"name\" <currency> [initialBalance].");
            }

            decimal? initialBalance = null;
            if (tokens.Count >= 5)
            {
                if (!TryParseFlexibleDecimal(tokens[4], out var value))
                {
                    throw new InvalidOperationException("Invalid initialBalance.");
                }

                initialBalance = value;
            }

            return new CardAddCommand(tokens[2], tokens[3], initialBalance);
        }

        if (cardAction == "list")
        {
            return new CardListCommand();
        }

        if (cardAction == "set-default")
        {
            if (tokens.Count < 3 || !int.TryParse(tokens[2], out var cardId))
            {
                throw new InvalidOperationException("card set-default requires cardId.");
            }

            return new CardSetDefaultCommand(cardId);
        }

        throw new InvalidOperationException("Unknown card command.");
    }

    private static ParsedCommand ParseTransaction(
        IReadOnlyList<string> tokens,
        TransactionType transactionType)
    {
        if (tokens.Count < 4)
        {
            throw new InvalidOperationException("Transaction command is incomplete.");
        }

        var action = tokens[1].ToLowerInvariant();
        if (action != "add")
        {
            throw new InvalidOperationException("Only add is supported for transactions.");
        }

        if (!decimal.TryParse(tokens[2], out var transactionAmount))
        {
            throw new InvalidOperationException("Invalid amount.");
        }

        var category = transactionType == TransactionType.Expense ? tokens[3].Trim() : tokens[3];
        if (transactionType == TransactionType.Expense && category.Length == 0)
        {
            throw new InvalidOperationException("Category cannot be empty.");
        }

        var options = ParseTransactionOptions(tokens, 4);
        return new TransactionAddCommand(
            transactionType,
            transactionAmount,
            category,
            options.CardId,
            options.Date,
            options.Note);
    }

    private static (
        int? CardId,
        DateOnly? Date,
        string? Note)
        ParseTransactionOptions(IReadOnlyList<string> tokens, int startIndex)
    {
        int? cardId = null;
        DateOnly? date = null;
        string? note = null;

        var optionIndex = startIndex;
        while (optionIndex < tokens.Count)
        {
            var option = tokens[optionIndex];
            if (option == "--card")
            {
                optionIndex++;
                if (optionIndex >= tokens.Count)
                {
                    throw new InvalidOperationException("Invalid --card value.");
                }

                var parsedCardId = ResolveCardFromArgs(tokens[optionIndex]);
                if (!parsedCardId.HasValue)
                {
                    throw new InvalidOperationException("Invalid --card value.");
                }

                cardId = parsedCardId;
            }
            else if (option == "--date")
            {
                optionIndex++;
                if (optionIndex >= tokens.Count 
                    || !DateOnly.TryParse(tokens[optionIndex], out var parsedDate))
                {
                    throw new InvalidOperationException("Invalid --date value. Use YYYY-MM-DD.");
                }

                date = parsedDate;
            }
            else if (option == "--note")
            {
                optionIndex++;
                if (optionIndex >= tokens.Count)
                {
                    throw new InvalidOperationException("Invalid --note value.");
                }

                note = tokens[optionIndex];
            }
            else
            {
                throw new InvalidOperationException($"Unknown option {option}.");
            }

            optionIndex++;
        }

        return (cardId, date, note);
    }

    public static int? ResolveCardFromArgs(string cardArgument)
    {
        if (int.TryParse(cardArgument, out var numericId))
        {
            return numericId;
        }

        if (Regex.IsMatch(cardArgument, "^[0-9a-fA-F-]{36}$") 
            && Guid.TryParse(cardArgument, out var parsedGuid))
        {
            var cardIdText = parsedGuid.ToString("N")[20..];
            if (int.TryParse(cardIdText, out var cardIdFromGuid))
            {
                return cardIdFromGuid;
            }
        }

        return null;
    }

    private static ParsedCommand ParseLimit(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException("Limit command is incomplete.");
        }

        var limitAction = tokens[1].ToLowerInvariant();
        if (limitAction == "set")
        {
            if (tokens.Count < 3 || !decimal.TryParse(tokens[2], out var amount))
            {
                throw new InvalidOperationException("limit set requires amount.");
            }

            return new LimitSetCommand(amount);
        }

        if (limitAction == "show")
        {
            return new LimitShowCommand();
        }

        throw new InvalidOperationException("Unknown limit command.");
    }

    private static ParsedCommand ParseReport(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 2 || tokens[1].ToLowerInvariant() != "day")
        {
            throw new InvalidOperationException("report day is the only supported report command.");
        }

        if (tokens.Count == 2)
        {
            return new ReportDayCommand(null);
        }

        DateOnly? date = null;
        var optionIndex = 2;
        while (optionIndex < tokens.Count)
        {
            var option = tokens[optionIndex];
            if (option == "--date")
            {
                optionIndex++;
                if (optionIndex >= tokens.Count 
                    || !DateOnly.TryParse(tokens[optionIndex], out var parsedDate))
                {
                    throw new InvalidOperationException("Invalid --date value. Use YYYY-MM-DD.");
                }

                date = parsedDate;
            }
            else
            {
                throw new InvalidOperationException($"Unknown option {option}.");
            }

            optionIndex++;
        }

        return new ReportDayCommand(date);
    }

    private static bool TryParseFlexibleDecimal(string rawValue, out decimal value)
    {
        var normalizedValue = rawValue.Trim().Replace(',', '.');
        return decimal.TryParse(
            normalizedValue,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}
