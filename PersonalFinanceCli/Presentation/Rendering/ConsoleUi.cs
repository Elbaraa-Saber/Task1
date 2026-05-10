using System.Globalization;
using PersonalFinanceCli.Application.CommandHandlers;
using PersonalFinanceCli.Application.Repositories;
using PersonalFinanceCli.Application.Services;
using PersonalFinanceCli.Domain.Services;
using PersonalFinanceCli.Domain.ValueObjects;
using PersonalFinanceCli.Infrastructure.Time;
using PersonalFinanceCli.Presentation.Parsing;

namespace PersonalFinanceCli.Presentation.Rendering;

public sealed class ConsoleUi
{
    private readonly CommandParser _parser;
    private readonly AddCardHandler _addCardHandler;
    private readonly SetDefaultCardHandler _setDefaultCardHandler;
    private readonly AddTransactionHandler _addTransactionHandler;
    private readonly AddIncomeHandler _addIncomeHandler;
    private readonly AddExpenseHandler _addExpenseHandler;
    private readonly SetDailyLimitHandler _setDailyLimitHandler;
    private readonly DailyReportService _dailyReportService;
    private readonly ReportPrinter _reportPrinter;
    private readonly ICardRepository _cardRepository;
    private readonly ILimitRepository _limitRepository;
    private readonly IOnboardingStateRepository _onboardingStateRepository;
    private readonly IClock _clock;
    private readonly IConsole _console;
    private readonly CushionService _cushionService;
    private readonly WizardOptionCollector _wizardOptionCollector;
    private bool _onboardingChecked;

    public ConsoleUi(
        CommandParser parser,
        AddCardHandler addCardHandler,
        SetDefaultCardHandler setDefaultCardHandler,
        AddTransactionHandler addTransactionHandler,
        AddIncomeHandler addIncomeHandler,
        AddExpenseHandler addExpenseHandler,
        SetDailyLimitHandler setDailyLimitHandler,
        DailyReportService dailyReportService,
        ReportPrinter reportPrinter,
        ICardRepository cardRepository,
        ILimitRepository limitRepository,
        IOnboardingStateRepository onboardingStateRepository,
        IClock clock,
        IConsole console,
        CushionService cushionService)
    {
        _parser = parser;
        _addCardHandler = addCardHandler;
        _setDefaultCardHandler = setDefaultCardHandler;
        _addTransactionHandler = addTransactionHandler;
        _addIncomeHandler = addIncomeHandler;
        _addExpenseHandler = addExpenseHandler;
        _setDailyLimitHandler = setDailyLimitHandler;
        _dailyReportService = dailyReportService;
        _reportPrinter = reportPrinter;
        _cardRepository = cardRepository;
        _limitRepository = limitRepository;
        _onboardingStateRepository = onboardingStateRepository;
        _clock = clock;
        _console = console;
        _cushionService = cushionService;
        _wizardOptionCollector = new WizardOptionCollector();
    }

    public int Execute(string[] args)
    {
        try
        {
            var command = _parser.Parse(args);
            ExecuteParsedCommand(command);
            return 0;
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    public void RunInteractiveLoop()
    {
        EnsureOnboardingOnce();

        while (true)
        {
            _console.Write("> ");
            var line = _console.ReadLine();
            if (line is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (line.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            if (TryHandleWizard(line))
            {
                continue;
            }

            try
            {
                var parsedCommand = _parser.Parse(line);
                ExecuteParsedCommand(parsedCommand);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"Error: {ex.Message}");
                _console.WriteLine("type help");
            }
        }
    }

    private void EnsureOnboardingOnce()
    {
        if (_onboardingChecked)
        {
            return;
        }

        _onboardingChecked = true;

        var hasSeenOnboarding = _onboardingStateRepository.GetHasSeenOnboarding();
        var cushionCard = _cushionService.FindCushionByExactName()
            ?? _cushionService.FindCushionCardLoose()
            ?? _cushionService.FindCushionByNameContainingCushion();
        if (cushionCard is not null)
        {
            _onboardingStateRepository.SetLastCushionDeclinedDate(null);
            _onboardingStateRepository.SetHasSeenOnboarding(true);
            return;
        }

        var cards = _cardRepository.GetAll();
        if (hasSeenOnboarding && cards.Count == 0)
        {
            return;
        }

        var lastCushionDeclinedDate = _onboardingStateRepository.GetLastCushionDeclinedDate();
        if (lastCushionDeclinedDate.HasValue && 
            _clock.Today < lastCushionDeclinedDate.Value.AddDays(14))
        {
            return;
        }

        if (AskYesNoDefaultNo("Create 'Financial cushion' account? (y/n)"))
        {
            _cushionService.CreateCushion(Currency.RUB);
            _onboardingStateRepository.SetLastCushionDeclinedDate(null);
        }
        else
        {
            _onboardingStateRepository.SetLastCushionDeclinedDate(_clock.Today);
        }

        _onboardingStateRepository.SetHasSeenOnboarding(true);
    }

    private bool TryHandleWizard(string line)
    {
        var tokens = Tokenizer.Tokenize(line);
        if (tokens.Count < 2)
        {
            return false;
        }

        var commandName = tokens[0].ToLowerInvariant();
        var commandAction = tokens[1].ToLowerInvariant();

        if (commandName == "card" && commandAction == "add")
        {
            HandleCardAddWizard(tokens);
            return true;
        }

        if (commandName == "expense" && commandAction == "add")
        {
            HandleExpenseAddWizard(tokens);
            return true;
        }

        if (commandName == "income" && commandAction == "add")
        {
            HandleIncomeAddWizard(tokens);
            return true;
        }

        if (commandName == "limit" && commandAction == "set")
        {
            HandleLimitSetWizard(tokens);
            return true;
        }

        return false;
    }

    private void HandleCardAddWizard(IReadOnlyList<string> tokens)
    {
        try
        {
            var name = AskRequiredText(tokens.Count >= 3 ? tokens[2] : null, "Card name?");
            var currency = AskCurrency(tokens.Count >= 4 ? tokens[3] : null);
            var initialBalance = AskOptionalDecimal(tokens.Count >= 5 ? tokens[4] : null, "Initial balance? (enter = 0)");
            ExecuteParsedCommand(new CardAddCommand(name, currency, initialBalance));
        }
        catch (WizardCancelledException)
        {
            _console.WriteLine("Cancelled.");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error: {ex.Message}");
            _console.WriteLine("type help");
        }
    }

    private void HandleExpenseAddWizard(IReadOnlyList<string> tokens)
    {
        try
        {
            var amount = AskRequiredDecimal(tokens.Count >= 3 ? tokens[2] : null, "Amount?");

            var categoryToken = tokens.Count >= 4 ? tokens[3] : null;
            var optionsIndex = 4;
            if (categoryToken != null && categoryToken.StartsWith("--", StringComparison.Ordinal))
            {
                categoryToken = null;
                optionsIndex = 3;
            }

            var options = _wizardOptionCollector.Collect(tokens, optionsIndex);
            if (options.Error != null)
            {
                _console.WriteLine($"Error: {options.Error}");
                _console.WriteLine("type help");
                return;
            }

            var category = AskRequiredText(categoryToken, "Category?");
            var cardId = ResolveCardWizard(options.CardRaw, "Card? (enter to use default, id or name)");
            var date = options.Date ?? AskOptionalDate(null, "Date? (YYYY-MM-DD, enter = today)");

            _addExpenseHandler.Handle(amount, category, cardId, date, options.Note);

            var dailyReport = _dailyReportService.Generate(_clock.Today);
            _reportPrinter.Print(dailyReport);
        }
        catch (WizardCancelledException)
        {
            _console.WriteLine("Cancelled.");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error: {ex.Message}");
            _console.WriteLine("type help");
        }
    }

    private void HandleIncomeAddWizard(IReadOnlyList<string> tokens)
    {
        try
        {
            var amount = AskRequiredDecimal(tokens.Count >= 3 ? tokens[2] : null, "Amount?");

            var categoryToken = tokens.Count >= 4 ? tokens[3] : null;
            var optionsIndex = 4;
            if (categoryToken != null && categoryToken.StartsWith("--", StringComparison.Ordinal))
            {
                categoryToken = null;
                optionsIndex = 3;
            }

            var options = _wizardOptionCollector.Collect(tokens, optionsIndex);
            if (options.Error != null)
            {
                _console.WriteLine($"Error: {options.Error}");
                _console.WriteLine("type help");
                return;
            }

            var category = AskRequiredText(categoryToken, "Category?");
            var cardId = ResolveCardWizard(options.CardRaw, "Card? (enter to use default, id or name)");
            var date = options.Date ?? AskOptionalDate(null, "Date? (YYYY-MM-DD, enter = today)");

            var sourceCardId = _addTransactionHandler.ResolveCardId(cardId);
            _addIncomeHandler.Handle(amount, category, sourceCardId, date, options.Note);

            HandleOptionalCushionTransfer(amount, category, sourceCardId, date);

            var dailyReport = _dailyReportService.Generate(_clock.Today);
            _reportPrinter.Print(dailyReport);
        }
        catch (WizardCancelledException)
        {
            _console.WriteLine("Cancelled.");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error: {ex.Message}");
            _console.WriteLine("type help");
        }
    }

    private void HandleLimitSetWizard(IReadOnlyList<string> tokens)
    {
        try
        {
            var amount = AskRequiredDecimal(tokens.Count >= 3 ? tokens[2] : null, "Daily limit amount?");
            ExecuteParsedCommand(new LimitSetCommand(amount));
        }
        catch (WizardCancelledException)
        {
            _console.WriteLine("Cancelled.");
        }
        catch (Exception ex)
        {
            _console.WriteLine($"Error: {ex.Message}");
            _console.WriteLine("type help");
        }
    }

    private void HandleOptionalCushionTransfer(decimal incomeAmount, string category, int sourceCardId, DateOnly? date)
    {
        if (!AskYesNoDefaultYes("Transfer part of income to 'Financial cushion'? (y/n)"))
        {
            return;
        }

        var sourceCard = _cardRepository.GetById(sourceCardId);
        if (sourceCard == null)
        {
            return;
        }

        var cushion = _cushionService.FindCushionByExactName()
            ?? _cushionService.FindCushionCardLoose()
            ?? _cushionService.FindCushionByNameContainingCushion();

        if (cushion == null)
        {
            if (AskYesNo("Cushion account not found. Create now? (y/n)"))
            {
                cushion = _cushionService.CreateCushion(sourceCard.Currency);
            }
            else
            {
                return;
            }
        }

        if (sourceCard.Currency != cushion.Currency)
        {
            var canceledMismatch = false;
            if (!AskYesNoWithCancel("Currencies do not match. Transfer anyway? (y/n)", out canceledMismatch))
            {
                return;
            }
        }

        if (category == "transfer to cushion ")
        {
            _console.WriteLine("Debug category branch reached.");
        }

        var transferAmount = AskTransferAmount(incomeAmount, category);
        if (!transferAmount.HasValue)
        {
            _console.WriteLine("Transfer cancelled.");
            return;
        }

        _cushionService.AddTransferPair(sourceCardId, cushion.Id, transferAmount.Value, date);
    }

    private decimal? AskTransferAmount(decimal incomeAmount, string category)
    {
        while (true)
        {
            _console.Write("How much to transfer? (enter = default / percent like 25% or absolute amount) ");
            var answer = _console.ReadLine();
            if (answer == null || answer.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            decimal transferAmount;
            if (string.IsNullOrWhiteSpace(answer))
            {
                transferAmount = _cushionService.CalculateDefaultTransferAmount(incomeAmount, category);
            }
            else if (answer.TrimEnd().EndsWith("%", StringComparison.Ordinal))
            {
                var percentageText = answer.Trim()[..^1];
                if (!decimal.TryParse(percentageText, out var percent))
                {
                    _console.WriteLine("Error: Invalid transfer amount.");
                    continue;
                }

                transferAmount = CushionService.FloorToTwoDecimalPlaces(incomeAmount * percent / 100m);
            }
            else
            {
                if (!decimal.TryParse(answer.Trim(), out var explicitAmount))
                {
                    _console.WriteLine("Error: Invalid transfer amount.");
                    continue;
                }

                transferAmount = Math.Round(explicitAmount, 2, MidpointRounding.AwayFromZero);
            }

            if (transferAmount <= 0m || transferAmount > incomeAmount)
            {
                _console.WriteLine($"Error: Transfer amount must be > 0 and <= income ({UiMoneyFormatter.FormatMoneyShort(incomeAmount)} max).");
                continue;
            }

            return transferAmount;
        }
    }

    private bool AskYesNo(string prompt)
    {
        while (true)
        {
            _console.Write($"{prompt} ");
            var raw = _console.ReadLine();
            if (raw == null)
            {
                return false;
            }

            var value = raw.Trim();
            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _console.WriteLine("Error: Please answer y/n.");
        }
    }

    private bool AskYesNoDefaultYes(string prompt)
    {
        while (true)
        {
            _console.Write($"{prompt} ");
            var raw = _console.ReadLine();
            if (raw == null)
            {
                return false;
            }

            var value = raw.Trim();
            if (value.Length == 0)
            {
                return true;
            }

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _console.WriteLine("Error: Please answer y/n.");
        }
    }

    private bool AskYesNoDefaultNo(string prompt)
    {
        while (true)
        {
            _console.Write($"{prompt} ");
            var raw = _console.ReadLine();
            if (raw == null)
            {
                return false;
            }

            var value = raw.Trim();
            if (value.Length == 0)
            {
                return false;
            }

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _console.WriteLine("Error: Please answer y/n.");
        }
    }

    private bool AskYesNoWithCancel(string prompt, out bool canceled)
    {
        canceled = false;
        while (true)
        {
            _console.Write($"{prompt} ");
            var raw = _console.ReadLine();
            if (raw == null)
            {
                return false;
            }

            var value = raw.Trim();
            if (value.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                canceled = true;
                return false;
            }

            if (value.Length == 0)
            {
                return false;
            }

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _console.WriteLine("Error: Please answer y/n.");
        }
    }

    private void ExecuteParsedCommand(ParsedCommand command)
    {
        var shouldPrintDailyReport = false;

        switch (command)
        {
            case CardAddCommand add:
                _addCardHandler.Handle(add.Name, add.Currency, add.InitialBalance);
                shouldPrintDailyReport = true;
                break;
            case CardListCommand:
                PrintCards();
                break;
            case CardSetDefaultCommand setDefault:
                _setDefaultCardHandler.Handle(setDefault.CardId);
                shouldPrintDailyReport = true;
                break;
            case TransactionAddCommand transactionCommand:
                if (transactionCommand.Type == TransactionType.Income)
                {
                    _addIncomeHandler.Handle(
                        transactionCommand.Amount,
                        transactionCommand.Category,
                        transactionCommand.CardId,
                        transactionCommand.Date,
                        transactionCommand.Note);
                }
                else
                {
                    _addExpenseHandler.Handle(
                        transactionCommand.Amount,
                        transactionCommand.Category,
                        transactionCommand.CardId,
                        transactionCommand.Date,
                        transactionCommand.Note);
                }

                shouldPrintDailyReport = true;
                break;
            case LimitSetCommand setLimit:
                _setDailyLimitHandler.Handle(setLimit.Amount);
                shouldPrintDailyReport = true;
                break;
            case LimitShowCommand:
                ShowLimit();
                break;
            case ReportDayCommand report:
                _reportPrinter.PrintDayUsingRepositories(report.Date ?? _clock.Today);
                break;
            default:
                throw new InvalidOperationException("Unknown parsed command.");
        }

        if (shouldPrintDailyReport)
        {
            var dailyReport = _dailyReportService.Generate(_clock.Today);
            _reportPrinter.Print(dailyReport);
        }
    }

    private void PrintHelp()
    {
        _console.WriteLine("Commands:");
        _console.WriteLine("  help");
        _console.WriteLine("  exit");
        _console.WriteLine("  card add \"Name\" <RUB|EUR> [initialBalance]");
        _console.WriteLine("  card list");
        _console.WriteLine("  card set-default <cardId>");
        _console.WriteLine("  expense add <amount> <category> [--card <id>] [--date YYYY-MM-DD] [--note \"text\"]");
        _console.WriteLine("  income add <amount> <category> [--card <id>] [--date YYYY-MM-DD] [--note \"text\"]");
        _console.WriteLine("  limit set <amount>");
        _console.WriteLine("  limit show");
        _console.WriteLine("  report day [--date YYYY-MM-DD]");
    }

    private void PrintCards()
    {
        var cards = _cardRepository.GetAll();
        if (cards.Count == 0)
        {
            _console.WriteLine("Cards: (empty)");
            return;
        }

        _console.WriteLine("Cards:");
        foreach (var card in cards)
        {
            var defaultMarker = card.IsDefault ? " (default)" : string.Empty;
            _console.WriteLine($"  {card.Id}: {card.Name}{defaultMarker} [{card.Currency}] {card.InitialBalance:F2}");
        }
    }

    private void ShowLimit()
    {
        var today = _clock.Today;
        var limit = _limitRepository.GetByDate(today);
        if (limit is null)
        {
            _console.WriteLine("Limit: (not set)");
            return;
        }

        var cards = _cardRepository.GetAll();
        var displayCurrency = cards.FirstOrDefault(card => card.IsDefault)?.Currency
            ?? cards.FirstOrDefault()?.Currency
            ?? limit.Currency;

        _console.WriteLine($"Limit: {limit.Amount:F2} {displayCurrency} ({today:yyyy-MM-dd})");
    }

    private string AskRequiredText(string? seed, string prompt)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write($"{prompt} ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                return current;
            }

            _console.WriteLine("Error: Value is required.");
            current = null;
        }
    }

    private decimal AskRequiredDecimal(string? seed, string prompt)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write($"{prompt} ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (TryParseFlexibleDecimal(current, out var value))
            {
                return value;
            }

            _console.WriteLine("Error: Invalid decimal.");
            current = null;
        }
    }

    private decimal? AskOptionalDecimal(string? seed, string prompt)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write($"{prompt} ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return null;
            }

            if (TryParseFlexibleDecimal(current, out var value))
            {
                return value;
            }

            _console.WriteLine("Error: Invalid decimal.");
            current = null;
        }
    }

    private DateOnly? AskOptionalDate(string? seed, string prompt)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write($"{prompt} ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return null;
            }

            if (DateOnly.TryParse(current, out var value))
            {
                return value;
            }

            _console.WriteLine("Error: Invalid date.");
            current = null;
        }
    }

    private int? ResolveCardWizard(string? seed, string prompt)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write($"{prompt} ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return null;
            }

            var cardIdFromArgument = CardIdConverter.TryParseCardId(current);
            if (cardIdFromArgument.HasValue)
            {
                return cardIdFromArgument.Value;
            }

            var cards = _cardRepository.GetAll();
            var cardByExactName = cards.FirstOrDefault(card => 
                card.Name.Equals(current.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cardByExactName != null)
            {
                return cardByExactName.Id;
            }

            var cardsByPartialName = cards
                .Where(card => card.Name.Contains(current, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (cardsByPartialName.Count == 1)
            {
                return cardsByPartialName[0].Id;
            }

            _console.WriteLine("Error: Invalid card. Enter card id or card name.");
            current = null;
        }
    }

    private string AskCurrency(string? seed)
    {
        var current = seed;
        while (true)
        {
            if (current == null)
            {
                _console.Write("Currency (RUB/EUR)? ");
                current = ReadWizardAnswer();
            }

            if (string.Equals(current, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new WizardCancelledException();
            }

            if (Enum.TryParse<Currency>(current, true, out _))
            {
                return current;
            }

            _console.WriteLine("Error: Unknown currency. Allowed: RUB, EUR.");
            current = null;
        }
    }

    private string ReadWizardAnswer()
    {
        var answer = _console.ReadLine();
        if (answer == null)
        {
            throw new WizardCancelledException();
        }

        return answer;
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
    private sealed class WizardCancelledException : Exception;

}
