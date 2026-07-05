using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Search;

public class SearchSettingsService : ISearchSettingsService
{
    private static readonly int[] AllowedIntervals = [5, 30, 60];
    private const int MaxKeywords = 20;
    private const int MaxNegativeKeywords = 10;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ExpansionLocks = new();

    private readonly IAppDbContext _dbContext;
    private readonly IAiSearchExpansionService _expansionService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<SearchSettingsService> _logger;

    public SearchSettingsService(
        IAppDbContext dbContext,
        IAiSearchExpansionService expansionService,
        IBackgroundJobClient backgroundJobs,
        ILogger<SearchSettingsService> logger)
    {
        _dbContext = dbContext;
        _expansionService = expansionService;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public async Task<SearchSettingsDto> GetSettingsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(profileId, accountId, cancellationToken);

        var settings = await _dbContext.SearchSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        return MapToDto(settings ?? CreateDefault(profileId));
    }

    public async Task<SearchSettingsDto> UpdateSettingsAsync(Guid accountId, UpdateSearchSettingsDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(dto.ProfileId, accountId, cancellationToken);
        Validate(dto);

        var normalizedKeywords = NormalizeTerms(dto.UserKeywords, MaxKeywords);
        var normalizedNegativeKeywords = NormalizeTerms(dto.NegativeKeywords, MaxNegativeKeywords);
        var settings = await _dbContext.SearchSettings
            .FirstOrDefaultAsync(s => s.ProfileId == dto.ProfileId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (settings == null)
        {
            settings = new SearchSettings
            {
                Id = Guid.NewGuid(),
                ProfileId = dto.ProfileId,
                CreatedAt = now
            };
            _dbContext.SearchSettings.Add(settings);
        }

        var wasEnabled = settings.IsEnabled;
        var previousKeywords = settings.UserKeywords;
        var previousNegativeKeywords = settings.NegativeKeywords;
        var previousExpandedPositiveTerms = settings.ExpandedPositiveTerms;
        var previousExpandedIntentTerms = settings.ExpandedIntentTerms;
        var previousStrongTerms = settings.StrongTerms;
        var previousSearchProfileSummary = settings.SearchProfileSummary;
        var previousMustIncludeSignals = settings.MustIncludeSignals;
        var previousSoftSignals = settings.SoftSignals;
        var previousRejectSignals = settings.RejectSignals;

        var keywordsChanged =
            !previousKeywords.SequenceEqual(normalizedKeywords, StringComparer.OrdinalIgnoreCase) ||
            !previousNegativeKeywords.SequenceEqual(normalizedNegativeKeywords, StringComparer.OrdinalIgnoreCase);
        var runtimeSettingsChanged =
            settings.NotificationsEnabled != dto.NotificationsEnabled ||
            settings.IntervalMinutes != dto.IntervalMinutes ||
            keywordsChanged;
        var needsInitialHiddenProfile =
            normalizedKeywords.Length > 0 &&
            string.IsNullOrWhiteSpace(previousSearchProfileSummary);

        var finalIsEnabled = wasEnabled && runtimeSettingsChanged ? false : dto.IsEnabled;
        if (finalIsEnabled)
        {
            if (normalizedKeywords.Length == 0)
            {
                throw new ArgumentException("SEARCH_KEYWORDS_REQUIRED");
            }

            if (!await HasSearchSourceAsync(dto.ProfileId, cancellationToken))
            {
                throw new ArgumentException("SEARCH_SOURCE_REQUIRED");
            }
        }

        settings.IsEnabled = finalIsEnabled;
        settings.NotificationsEnabled = dto.NotificationsEnabled;
        settings.IntervalMinutes = dto.IntervalMinutes;
        settings.UserKeywords = normalizedKeywords;
        settings.NegativeKeywords = normalizedNegativeKeywords;
        settings.UpdatedAt = now;

        SearchExpansionRequest? expansionRequest = null;
        var shouldExpandHiddenProfile = normalizedKeywords.Length > 0 && (keywordsChanged || needsInitialHiddenProfile);

        if (keywordsChanged && normalizedKeywords.Length == 0)
        {
            ClearHiddenSearchProfile(settings);
            settings.NeedsAiExpansion = false;
            await ResetKworkFullScanAsync(dto.ProfileId, cancellationToken);
            await ResetTelegramSearchMarkersAsync(dto.ProfileId, cancellationToken);
        }
        else if (shouldExpandHiddenProfile)
        {
            if (keywordsChanged)
            {
                await ResetKworkFullScanAsync(dto.ProfileId, cancellationToken);
                await ResetTelegramSearchMarkersAsync(dto.ProfileId, cancellationToken);
            }

            settings.NeedsAiExpansion = true;
            var rebuildFromScratch = ShouldRebuildHiddenProfileFromScratch(previousKeywords, normalizedKeywords);
            expansionRequest = new Models.SearchExpansionRequest(
                normalizedKeywords,
                normalizedNegativeKeywords,
                rebuildFromScratch ? Array.Empty<string>() : previousExpandedPositiveTerms,
                rebuildFromScratch ? Array.Empty<string>() : previousExpandedIntentTerms,
                rebuildFromScratch ? Array.Empty<string>() : previousStrongTerms,
                rebuildFromScratch ? null : previousSearchProfileSummary,
                rebuildFromScratch ? Array.Empty<string>() : previousMustIncludeSignals,
                rebuildFromScratch ? Array.Empty<string>() : previousSoftSignals,
                rebuildFromScratch ? Array.Empty<string>() : previousRejectSignals,
                normalizedKeywords.Except(previousKeywords, StringComparer.OrdinalIgnoreCase).ToArray(),
                previousKeywords.Except(normalizedKeywords, StringComparer.OrdinalIgnoreCase).ToArray(),
                normalizedNegativeKeywords.Except(previousNegativeKeywords, StringComparer.OrdinalIgnoreCase).ToArray(),
                previousNegativeKeywords.Except(normalizedNegativeKeywords, StringComparer.OrdinalIgnoreCase).ToArray()
            );

            WriteExpansionDebugFile(settings.ProfileId, expansionRequest, null, now, "queued_background");
        }
        else
        {
            WriteExpansionDebugFile(settings.ProfileId, new Models.SearchExpansionRequest(
                normalizedKeywords,
                normalizedNegativeKeywords,
                previousExpandedPositiveTerms,
                previousExpandedIntentTerms,
                previousStrongTerms,
                previousSearchProfileSummary,
                previousMustIncludeSignals,
                previousSoftSignals,
                previousRejectSignals,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()), null, now, "keywords_unchanged_ai_not_called");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (expansionRequest != null)
        {
            _backgroundJobs.Enqueue<ISearchSettingsService>(s =>
                s.ExpandProfileBackgroundAsync(dto.ProfileId, expansionRequest, CancellationToken.None));
        }

        return MapToDto(settings);
    }

    public async Task EnqueuePendingProfileExpansionsAsync(CancellationToken cancellationToken = default)
    {
        var pendingProfileIds = await _dbContext.SearchSettings
            .AsNoTracking()
            .Where(settings => settings.NeedsAiExpansion && settings.UserKeywords.Length > 0)
            .Select(settings => settings.ProfileId)
            .ToListAsync(cancellationToken);

        foreach (var profileId in pendingProfileIds)
        {
            _backgroundJobs.Enqueue<ISearchSettingsService>(service =>
                service.ExpandProfileBackgroundAsync(profileId, CancellationToken.None));
        }

        var emptyKeywordSettings = await _dbContext.SearchSettings
            .Where(settings => settings.NeedsAiExpansion && settings.UserKeywords.Length == 0)
            .ToListAsync(cancellationToken);

        foreach (var settings in emptyKeywordSettings)
        {
            ClearHiddenSearchProfile(settings);
            settings.NeedsAiExpansion = false;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (emptyKeywordSettings.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ExpandProfileBackgroundAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SearchSettings.FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);
        if (settings == null || !settings.NeedsAiExpansion)
        {
            return;
        }

        if (settings.UserKeywords.Length == 0)
        {
            ClearHiddenSearchProfile(settings);
            settings.NeedsAiExpansion = false;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var expansionRequest = new Models.SearchExpansionRequest(
            settings.UserKeywords,
            settings.NegativeKeywords,
            settings.ExpandedPositiveTerms,
            settings.ExpandedIntentTerms,
            settings.StrongTerms,
            settings.SearchProfileSummary,
            settings.MustIncludeSignals,
            settings.SoftSignals,
            settings.RejectSignals,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        await ExpandProfileBackgroundAsync(profileId, expansionRequest, cancellationToken);
    }

    public async Task ExpandProfileBackgroundAsync(Guid profileId, SearchExpansionRequest request, CancellationToken cancellationToken = default)
    {
        var profileLock = ExpansionLocks.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        await profileLock.WaitAsync(cancellationToken);
        try
        {
            var settings = await _dbContext.SearchSettings.FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);
            if (settings == null)
            {
                return;
            }

            if (settings.UserKeywords.Length == 0)
            {
                ClearHiddenSearchProfile(settings);
                settings.NeedsAiExpansion = false;
                settings.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            if (!settings.NeedsAiExpansion ||
                !TermsEqual(settings.UserKeywords, request.UserKeywords) ||
                !TermsEqual(settings.NegativeKeywords, request.NegativeKeywords))
            {
                return;
            }

            await ExpandAndApplyAsync(settings, request, cancellationToken);
        }
        finally
        {
            profileLock.Release();
        }
    }

    private async Task ExpandAndApplyAsync(SearchSettings settings, SearchExpansionRequest expansionRequest, CancellationToken cancellationToken)
    {
        using var expansionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        expansionTimeout.CancelAfter(TimeSpan.FromSeconds(120));

        SearchExpansionResult? expansion;
        try
        {
            expansion = await _expansionService.ExpandAsync(expansionRequest, expansionTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI search profile expansion timed out for profile {ProfileId}", settings.ProfileId);
            expansion = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI search profile expansion failed for profile {ProfileId}", settings.ProfileId);
            expansion = null;
        }

        var now = DateTimeOffset.UtcNow;
        var currentSnapshot = await _dbContext.SearchSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProfileId == settings.ProfileId, cancellationToken);

        if (currentSnapshot == null ||
            !currentSnapshot.NeedsAiExpansion ||
            !TermsEqual(currentSnapshot.UserKeywords, expansionRequest.UserKeywords) ||
            !TermsEqual(currentSnapshot.NegativeKeywords, expansionRequest.NegativeKeywords))
        {
            WriteExpansionDebugFile(settings.ProfileId, expansionRequest, expansion, now, "stale_background_result_ignored");
            return;
        }

        expansion ??= BuildFallbackExpansion(expansionRequest);
        if (expansion != null)
        {
            settings.SearchProfileSummary = expansion.SearchProfileSummary;
            settings.MustIncludeSignals = expansion.MustIncludeSignals;
            settings.SoftSignals = expansion.SoftSignals;
            settings.RejectSignals = expansion.RejectSignals;
            settings.ExpandedPositiveTerms = expansion.ExpandedPositiveTerms;
            settings.ExpandedIntentTerms = expansion.ExpandedIntentTerms;
            settings.StrongTerms = expansion.StrongTerms;
            settings.LastAiExpandedAt = now;
            settings.NeedsAiExpansion = false;
            settings.UpdatedAt = now;

            WriteExpansionDebugFile(settings.ProfileId, expansionRequest, expansion, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureProfileAccessAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken)
    {
        var hasAccess = await _dbContext.Profiles
            .AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private async Task<bool> HasSearchSourceAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var connectedExchange = await _dbContext.ExchangeConnections
            .AsNoTracking()
            .AnyAsync(connection =>
                connection.ProfileId == profileId &&
                connection.IsConnected &&
                !connection.RequiresReconnect,
                cancellationToken);

        if (connectedExchange)
        {
            return true;
        }

        var activeTelegramSources = await _dbContext.Sources
            .AsNoTracking()
            .Where(source =>
                source.ProfileId == profileId &&
                source.Type == SourceType.Telegram &&
                source.Status == SourceStatus.Active)
            .Select(source => source.Credentials)
            .ToListAsync(cancellationToken);

        return activeTelegramSources.Any(credentials => ReadPurpose(credentials) == 0);
    }

    private static void Validate(UpdateSearchSettingsDto dto)
    {
        if (!AllowedIntervals.Contains(dto.IntervalMinutes))
        {
            throw new ArgumentException("INVALID_INTERVAL");
        }

        if (NormalizeTerms(dto.UserKeywords, int.MaxValue).Length > MaxKeywords)
        {
            throw new ArgumentException("TOO_MANY_KEYWORDS");
        }

        if (NormalizeTerms(dto.NegativeKeywords, int.MaxValue).Length > MaxNegativeKeywords)
        {
            throw new ArgumentException("TOO_MANY_NEGATIVE_KEYWORDS");
        }
    }

    private static string[] NormalizeTerms(IEnumerable<string>? terms, int max)
    {
        return (terms ?? Array.Empty<string>())
            .Select(term => term.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();
    }

    private static int ReadPurpose(string? credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(credentials);
            if (document.RootElement.TryGetProperty("Purpose", out var pascal) && pascal.TryGetInt32(out var pascalValue))
            {
                return pascalValue;
            }

            if (document.RootElement.TryGetProperty("purpose", out var camel) && camel.TryGetInt32(out var camelValue))
            {
                return camelValue;
            }
        }
        catch
        {
            return 0;
        }

        return 0;
    }

    private static void ClearHiddenSearchProfile(SearchSettings settings)
    {
        settings.SearchProfileSummary = string.Empty;
        settings.MustIncludeSignals = Array.Empty<string>();
        settings.SoftSignals = Array.Empty<string>();
        settings.RejectSignals = Array.Empty<string>();
        settings.ExpandedPositiveTerms = Array.Empty<string>();
        settings.ExpandedIntentTerms = Array.Empty<string>();
        settings.StrongTerms = Array.Empty<string>();
    }

    private static bool ShouldRebuildHiddenProfileFromScratch(string[] previousKeywords, string[] normalizedKeywords)
    {
        return previousKeywords.Length > 0 &&
               normalizedKeywords.Length > 0 &&
               !previousKeywords.Intersect(normalizedKeywords, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static bool TermsEqual(string[] left, string[] right)
    {
        return left.Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(right.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }

    private static SearchExpansionResult BuildFallbackExpansion(SearchExpansionRequest request)
    {
        var positiveTerms = NormalizeTerms(
            request.UserKeywords
                .Concat(request.PreviousExpandedPositiveTerms)
                .Concat(request.AddedKeywords)
                .SelectMany(term => new[]
                {
                    term,
                    $"нужен {term}",
                    $"ищу {term}",
                    $"заказать {term}",
                    $"разработка {term}",
                    $"создать {term}",
                    $"доработать {term}",
                    $"fix {term}",
                    $"build {term}"
                }),
            80);

        var intentTerms = NormalizeTerms(new[]
        {
            "нужно сделать",
            "нужен специалист",
            "ищу исполнителя",
            "ищу фрилансера",
            "требуется разработчик",
            "требуется специалист",
            "надо доработать",
            "нужна помощь",
            "looking for developer",
            "need help with",
            "need to build",
            "need to fix",
            "hiring freelancer"
        }, 80);

        var strongTerms = NormalizeTerms(
            request.UserKeywords
                .Concat(request.PreviousStrongTerms)
                .Where(term => term.Length >= 2),
            40);

        var rejectTerms = NormalizeTerms(
            request.NegativeKeywords
                .Concat(request.PreviousRejectSignals),
            40);

        var summary = $"Пользователь ищет оплачиваемые заказы по темам: {string.Join(", ", request.UserKeywords)}.";
        if (request.NegativeKeywords.Length > 0)
        {
            summary += $" Исключать: {string.Join(", ", request.NegativeKeywords)}.";
        }

        return new SearchExpansionResult(
            summary.Length <= 2000 ? summary : summary[..2000],
            positiveTerms.Take(30).ToArray(),
            positiveTerms.Concat(intentTerms).Distinct(StringComparer.OrdinalIgnoreCase).Take(60).ToArray(),
            rejectTerms,
            positiveTerms,
            intentTerms,
            strongTerms,
            NormalizeTerms(request.NegativeKeywords, 50));
    }

    private async Task ResetKworkFullScanAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var kworkSource = await _dbContext.Sources
            .FirstOrDefaultAsync(source => source.ProfileId == profileId && source.Type == SourceType.Kwork, cancellationToken);
        if (kworkSource == null)
        {
            return;
        }

        JsonObject metadata;
        try
        {
            metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(kworkSource.Credentials) ? "{}" : kworkSource.Credentials) as JsonObject ?? new JsonObject();
        }
        catch
        {
            metadata = new JsonObject();
        }

        metadata["purpose"] = ReadPurpose(kworkSource.Credentials);
        metadata["kworkNextPage"] = 4;
        metadata["kworkFullScanCompleted"] = false;
        kworkSource.Credentials = metadata.ToJsonString();
    }

    private async Task ResetTelegramSearchMarkersAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var telegramSources = await _dbContext.Sources
            .Where(source => source.ProfileId == profileId && source.Type == SourceType.Telegram)
            .ToListAsync(cancellationToken);

        foreach (var source in telegramSources)
        {
            if (ReadPurpose(source.Credentials) != 0)
            {
                continue;
            }

            JsonObject metadata;
            try
            {
                metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(source.Credentials) ? "{}" : source.Credentials) as JsonObject ?? new JsonObject();
            }
            catch
            {
                metadata = new JsonObject();
            }

            metadata.Remove("lastMessageId");
            source.Credentials = metadata.ToJsonString();
            source.LastScraped = null;
            source.LastError = null;
        }
    }

    private static void WriteExpansionDebugFile(
        Guid profileId,
        SearchExpansionRequest request,
        SearchExpansionResult? result,
        DateTimeOffset createdAt,
        string? statusOverride = null)
    {
        try
        {
            var directory = Path.Combine(FindProjectRoot(), "debug", "search-ai-expansions");
            Directory.CreateDirectory(directory);

            var fileName = $"{createdAt:yyyyMMdd-HHmmss}-{profileId}.txt";
            var filePath = Path.Combine(directory, fileName);

            var builder = new StringBuilder();
            builder.AppendLine("ClientScout Search AI Expansion Debug");
            builder.AppendLine($"CreatedAtUtc: {createdAt:O}");
            builder.AppendLine($"ProfileId: {profileId}");
            builder.AppendLine();
            AppendTerms(builder, "UserKeywords", request.UserKeywords);
            AppendTerms(builder, "NegativeKeywords", request.NegativeKeywords);
            AppendTerms(builder, "AddedKeywords", request.AddedKeywords);
            AppendTerms(builder, "RemovedKeywords", request.RemovedKeywords);
            AppendTerms(builder, "AddedNegativeKeywords", request.AddedNegativeKeywords);
            AppendTerms(builder, "RemovedNegativeKeywords", request.RemovedNegativeKeywords);
            AppendTerms(builder, "PreviousExpandedPositiveTerms", request.PreviousExpandedPositiveTerms);
            AppendTerms(builder, "PreviousExpandedIntentTerms", request.PreviousExpandedIntentTerms);
            AppendTerms(builder, "PreviousStrongTerms", request.PreviousStrongTerms);
            builder.AppendLine($"PreviousSearchProfileSummary: {request.PreviousSearchProfileSummary}");
            builder.AppendLine();
            AppendTerms(builder, "PreviousMustIncludeSignals", request.PreviousMustIncludeSignals);
            AppendTerms(builder, "PreviousSoftSignals", request.PreviousSoftSignals);
            AppendTerms(builder, "PreviousRejectSignals", request.PreviousRejectSignals);
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(statusOverride))
            {
                builder.AppendLine($"AI_RESULT: {statusOverride}");
            }
            else if (result == null)
            {
                builder.AppendLine("AI_RESULT: unavailable_or_failed");
            }
            else
            {
                builder.AppendLine("AI_RESULT: ok");
                builder.AppendLine($"SearchProfileSummary: {result.SearchProfileSummary}");
                builder.AppendLine();
                AppendTerms(builder, "MustIncludeSignals", result.MustIncludeSignals);
                AppendTerms(builder, "SoftSignals", result.SoftSignals);
                AppendTerms(builder, "RejectSignals", result.RejectSignals);
                AppendTerms(builder, "ExpandedPositiveTerms", result.ExpandedPositiveTerms);
                AppendTerms(builder, "ExpandedIntentTerms", result.ExpandedIntentTerms);
                AppendTerms(builder, "StrongTerms", result.StrongTerms);
                AppendTerms(builder, "NormalizedNegativeTerms", result.NormalizedNegativeTerms);
            }

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Debug export must never block saving user settings.
        }
    }

    private static void AppendTerms(StringBuilder builder, string label, IReadOnlyCollection<string> terms)
    {
        builder.AppendLine($"{label} ({terms.Count}):");
        foreach (var term in terms)
        {
            builder.AppendLine($"- {term}");
        }

        builder.AppendLine();
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ClientScout.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static SearchSettings CreateDefault(Guid profileId) => new()
    {
        Id = Guid.Empty,
        ProfileId = profileId,
        IsEnabled = false,
        NotificationsEnabled = true,
        IntervalMinutes = 30,
        UserKeywords = Array.Empty<string>(),
        NegativeKeywords = Array.Empty<string>(),
        SearchProfileSummary = null,
        MustIncludeSignals = Array.Empty<string>(),
        SoftSignals = Array.Empty<string>(),
        RejectSignals = Array.Empty<string>(),
        ExpandedPositiveTerms = Array.Empty<string>(),
        ExpandedIntentTerms = Array.Empty<string>(),
        StrongTerms = Array.Empty<string>(),
        NeedsAiExpansion = false,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static SearchSettingsDto MapToDto(SearchSettings settings) => new(
        settings.Id,
        settings.ProfileId,
        settings.IsEnabled,
        settings.NotificationsEnabled,
        settings.IntervalMinutes,
        settings.UserKeywords,
        settings.NegativeKeywords,
        settings.NeedsAiExpansion,
        settings.LastAiExpandedAt,
        settings.CreatedAt,
        settings.UpdatedAt);
}
