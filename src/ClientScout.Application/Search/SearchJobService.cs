using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Leads;
using ClientScout.Application.Search.Models;
using ClientScout.Application.Telegram;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ClientScout.Application.Search;

public class SearchJobService : ISearchJobService
{
    private static readonly TimeSpan CandidateFlushDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan KworkScanTimeout = TimeSpan.FromMinutes(20);
    // High concurrency to allow AiJsonClient's load balancer to distribute across all models
    private static readonly SemaphoreSlim ClassifierThrottle = new(50, 50);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> KworkScanLocks = new();
    private static readonly ConcurrentDictionary<Guid, List<SearchCandidateJobDto>> CandidateBuffers = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CandidateBufferLocks = new();
    private static readonly ConcurrentDictionary<Guid, byte> CandidateFlushScheduled = new();
    private readonly IAppDbContext _dbContext;
    private readonly ITelegramClientManager _telegramClientManager;
    private readonly ISearchIngestionService _ingestionService;
    private readonly ISearchCandidateFilter _candidateFilter;
    private readonly IAiLeadClassifier _classifier;
    private readonly IExchangeConnectionService _exchangeConnectionService;
    private readonly ILeadNotificationService _notificationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<SearchJobService> _logger;

    public SearchJobService(
        IAppDbContext dbContext,
        ITelegramClientManager telegramClientManager,
        ISearchIngestionService ingestionService,
        ISearchCandidateFilter candidateFilter,
        IAiLeadClassifier classifier,
        IExchangeConnectionService exchangeConnectionService,
        ILeadNotificationService notificationService,
        IHttpClientFactory httpClientFactory,
        IBackgroundJobClient backgroundJobs,
        ILogger<SearchJobService> logger)
    {
        _dbContext = dbContext;
        _telegramClientManager = telegramClientManager;
        _ingestionService = ingestionService;
        _candidateFilter = candidateFilter;
        _classifier = classifier;
        _exchangeConnectionService = exchangeConnectionService;
        _notificationService = notificationService;
        _httpClientFactory = httpClientFactory;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public async Task ScheduleDueSearchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var settings = await _dbContext.SearchSettings
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var setting in settings)
        {
            var telegramJitter = TimeSpan.Zero;
            var kworkJitter = TimeSpan.Zero;

            var kworkConnection = await _dbContext.ExchangeConnections
                .FirstOrDefaultAsync(c => c.ProfileId == setting.ProfileId && c.ExchangeType == ExchangeType.Kwork, cancellationToken);

            if (kworkConnection is { IsConnected: true, RequiresReconnect: false } &&
                IsDue(kworkConnection.LastCheckedAt, setting.IntervalMinutes, kworkJitter, now))
            {
                _backgroundJobs.Enqueue<ISearchJobService>(service => service.ScanKworkAsync(setting.ProfileId, CancellationToken.None));
            }

            var searchSources = await _dbContext.Sources
                .Where(s => s.ProfileId == setting.ProfileId && s.Type == SourceType.Telegram && s.Status == SourceStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var source in searchSources.Where(source => ReadPurpose(source.Credentials) == 0))
            {
                if (IsDue(source.LastScraped, setting.IntervalMinutes, telegramJitter, now))
                {
                    _backgroundJobs.Enqueue<ISearchJobService>(service => service.ScanSourceAsync(source.Id, CancellationToken.None));
                }
            }
        }
    }

    public async Task ScanSourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var scanStartedAt = DateTimeOffset.UtcNow;
        var scanLog = new StringBuilder();
        var source = await _dbContext.Sources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);

        if (source == null || source.Type != SourceType.Telegram || ReadPurpose(source.Credentials) != 0)
        {
            return;
        }

        try
        {
            var marker = ReadMarker(source.Credentials, "lastMessageId");
            var limit = 25;
            scanLog.AppendLine("ClientScout Telegram Scan Debug");
            scanLog.AppendLine($"StartedAtUtc: {scanStartedAt:O}");
            scanLog.AppendLine($"ProfileId: {source.ProfileId}");
            scanLog.AppendLine($"SourceId: {source.Id}");
            scanLog.AppendLine($"SourceName: {source.Name}");
            scanLog.AppendLine($"Url: {source.Url}");
            scanLog.AppendLine($"MarkerBefore: {marker}");
            scanLog.AppendLine($"Limit: {limit}");

            var messages = await _telegramClientManager.ReadLatestMessagesAsync(source.Profile!.AccountId.ToString(), source.Url, limit, marker);
            var sourceTopicName = ReadStringMarker(source.Credentials, "TopicName")
                ?? ReadStringMarker(source.Credentials, "topicName");
            var maxId = marker;
            var candidates = new List<SearchCandidateJobDto>();

            scanLog.AppendLine($"MessagesRead: {messages.Count}");

            foreach (var message in messages.OrderBy(m => m.MessageId))
            {
                maxId = Math.Max(maxId, message.MessageId);
                scanLog.AppendLine($"Message: id={message.MessageId}; date={message.Date:O}; url={message.OriginalUrl}; text={Truncate(message.Text, 220)}");
                candidates.Add(new SearchCandidateJobDto(
                    source.ProfileId,
                    source.Id,
                    $"tg:{message.MessageId}",
                    CreateTitle(message.Text),
                    message.Text,
                    message.OriginalUrl,
                    message.AuthorUrl,
                    message.TopicName ?? sourceTopicName));
            }

            EnqueueCandidateBatches(candidates);
            scanLog.AppendLine($"CandidatesEnqueued: {candidates.Count}");
            scanLog.AppendLine($"MarkerAfter: {maxId}");

            source.LastScraped = DateTimeOffset.UtcNow;
            source.LastError = null;
            source.Credentials = WriteMarker(source.Credentials, "lastMessageId", maxId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            WriteTelegramScanDebugFile(source.Id, scanStartedAt, scanLog);
        }
        catch (Exception ex)
        {
            if (IsTransientTelegramClientError(ex))
            {
                source.LastError = null;
            }
            else
            {
                source.LastError = ToFriendlyTelegramSourceError(ex);
                source.Status = SourceStatus.Error;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            scanLog.AppendLine($"Error: {ex}");
            WriteTelegramScanDebugFile(source.Id, scanStartedAt, scanLog);
            _logger.LogWarning(ex, "Telegram search scan failed for source {SourceId}", source.Id);
        }
    }

    public async Task ScanKworkAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var scanLock = KworkScanLocks.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        if (!await scanLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Kwork scan skipped for profile {ProfileId}: previous scan is still running", profileId);
            return;
        }

        var connection = await _dbContext.ExchangeConnections
            .FirstOrDefaultAsync(c => c.ProfileId == profileId && c.ExchangeType == ExchangeType.Kwork, cancellationToken);

        try
        {
            if (connection is not { IsConnected: true, RequiresReconnect: false })
            {
                return;
            }

            var session = DecodeSession(connection.EncryptedSession);
            var candidates = await FetchKworkCandidatesAsync(profileId, connection, session, cancellationToken)
                .WaitAsync(KworkScanTimeout, cancellationToken);
            _logger.LogInformation("Kwork scan for profile {ProfileId} produced {Count} candidates", profileId, candidates.Count);
            EnqueueCandidateBatches(candidates);

            connection.LastCheckedAt = DateTimeOffset.UtcNow;
            connection.LastError = null;
            connection.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            await StopSearchAfterKworkErrorAsync(profileId, ex.Message, true, cancellationToken);
            await _exchangeConnectionService.MarkRequiresReconnectAsync(profileId, ExchangeType.Kwork, ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            if (connection != null)
            {
                connection.LastCheckedAt = DateTimeOffset.UtcNow;
                connection.LastError = ex.Message;
                connection.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning(ex, "Kwork scan failed for profile {ProfileId}", profileId);
        }
        finally
        {
            scanLock.Release();
        }
    }

    public async Task ProcessCandidateAsync(SearchCandidateJobDto dto, CancellationToken cancellationToken = default)
    {
        await ProcessCandidateBatchAsync(new SearchCandidateBatchJobDto([dto]), cancellationToken);
    }

    public async Task ProcessCandidateBatchAsync(SearchCandidateBatchJobDto dto, CancellationToken cancellationToken = default)
    {
        await ClassifierThrottle.WaitAsync(cancellationToken);
        try
        {
            if (dto.Candidates.Length == 0)
            {
                return;
            }

            var sourceId = dto.Candidates[0].SourceId;
            var source = await _dbContext.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
            if (source == null)
            {
                foreach (var candidate in dto.Candidates)
                {
                    WriteCandidateDebugFile(candidate, null, null, "SOURCE_NOT_FOUND", null);
                }
                return;
            }

            var settings = await _dbContext.SearchSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProfileId == dto.Candidates[0].ProfileId, cancellationToken);

            var prefilters = new Dictionary<string, PrefilterResult?>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in dto.Candidates)
            {
                var prefilter = settings == null
                    ? null
                    : _candidateFilter.Evaluate($"{candidate.Title} {candidate.Content}", settings);
                prefilters[candidate.ExternalId] = prefilter;
                WriteCandidateDebugFile(candidate, source, prefilter, "PROCESS_START", null);
            }

            if (settings is { IsEnabled: false })
            {
                foreach (var candidate in dto.Candidates)
                {
                    prefilters.TryGetValue(candidate.ExternalId, out var prefilter);
                    WriteCandidateDebugFile(candidate, source, prefilter, "SEARCH_DISABLED", null);
                }
                return;
            }

            var leads = await _ingestionService.ProcessCandidatesAsync(
                dto.Candidates.Select(candidate => new LeadCandidate(
                    candidate.ProfileId,
                    candidate.SourceId,
                    candidate.ExternalId,
                    candidate.Title,
                    candidate.Content,
                    candidate.OriginalUrl,
                    candidate.AuthorUrl,
                    source,
                    candidate.TopicName)).ToArray(),
                cancellationToken);
            var leadByExternalId = leads.ToDictionary(lead => lead.ExternalId, StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in dto.Candidates)
            {
                leadByExternalId.TryGetValue(candidate.ExternalId, out var lead);
                prefilters.TryGetValue(candidate.ExternalId, out var prefilter);
                WriteCandidateDebugFile(candidate, source, prefilter, lead == null ? "LEAD_NOT_SAVED" : (lead.Status == LeadStatus.Hidden ? "LEAD_SAVED_HIDDEN" : "LEAD_SAVED"), lead);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
        catch (Exception ex)
        {
            foreach (var candidate in dto.Candidates)
            {
                WriteCandidateDebugFile(candidate, null, null, $"PROCESS_EXCEPTION:{ex.GetType().Name}:{ex.Message}", null);
            }
            throw;
        }
        finally
        {
            ClassifierThrottle.Release();
        }
    }

    private void EnqueueCandidateBatches(IReadOnlyCollection<SearchCandidateJobDto> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        var profileId = candidates.First().ProfileId;
        var bufferLock = CandidateBufferLocks.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        var shouldFlushNow = false;
        bufferLock.Wait();
        try
        {
            var buffer = CandidateBuffers.GetOrAdd(profileId, _ => []);
            buffer.AddRange(candidates);
            shouldFlushNow = buffer.Count >= _classifier.OptimalBatchSize;
        }
        finally
        {
            bufferLock.Release();
        }

        if (shouldFlushNow)
        {
            CandidateFlushScheduled.TryAdd(profileId, 0);
            _backgroundJobs.Enqueue<ISearchJobService>(
                service => service.FlushCandidateBatchesAsync(profileId, CancellationToken.None));
        }
        else if (CandidateFlushScheduled.TryAdd(profileId, 0))
        {
            _backgroundJobs.Schedule<ISearchJobService>(
                service => service.FlushCandidateBatchesAsync(profileId, CancellationToken.None),
                CandidateFlushDelay);
        }
    }

    public async Task FlushCandidateBatchesAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var bufferLock = CandidateBufferLocks.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        List<SearchCandidateJobDto> candidates;
        await bufferLock.WaitAsync(cancellationToken);
        try
        {
            CandidateFlushScheduled.TryRemove(profileId, out _);
            if (!CandidateBuffers.TryGetValue(profileId, out var buffer) || buffer.Count == 0)
            {
                return;
            }

            candidates = buffer
                .OrderBy(candidate => candidate.ExternalId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            buffer.Clear();
        }
        finally
        {
            bufferLock.Release();
        }

        foreach (var batch in await BuildBalancedCandidateBatchesAsync(candidates, cancellationToken))
        {
            _backgroundJobs.Enqueue<ISearchJobService>(service => service.ProcessCandidateBatchAsync(
                new SearchCandidateBatchJobDto(batch),
                CancellationToken.None));
        }
    }

    private async Task<List<SearchCandidateJobDto[]>> BuildBalancedCandidateBatchesAsync(
        IReadOnlyCollection<SearchCandidateJobDto> candidates,
        CancellationToken cancellationToken)
    {
        var sourceIds = candidates.Select(candidate => candidate.SourceId).Distinct().ToArray();
        var sourceTypes = await _dbContext.Sources
            .Where(source => sourceIds.Contains(source.Id))
            .Select(source => new { source.Id, source.Type })
            .ToDictionaryAsync(source => source.Id, source => source.Type, cancellationToken);

        return candidates
            .GroupBy(candidate => candidate.SourceId)
            .OrderByDescending(group => sourceTypes.TryGetValue(group.Key, out var type) && type == SourceType.Kwork)
            .ThenByDescending(group => group.Count())
            .SelectMany(group => group.OrderBy(candidate => candidate.ExternalId, StringComparer.OrdinalIgnoreCase).Chunk(_classifier.OptimalBatchSize))
            .Select(batch => batch.ToArray())
            .ToList();
    }

    private async Task StopSearchAfterKworkErrorAsync(Guid profileId, string error, bool requiresReconnect, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SearchSettings
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        if (settings != null)
        {
            settings.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var account = await _dbContext.Profiles
            .Where(profile => profile.Id == profileId)
            .Select(profile => profile.Account)
            .FirstOrDefaultAsync(cancellationToken);

        if (account == null)
        {
            return;
        }

        try
        {
            var message = requiresReconnect
                ? "Kwork запросил повторное подключение или антибот-проверку. Поиск по Telegram продолжает работать. Открой Kwork в браузере, пройди проверку и переподключи биржу в приложении."
                : $"Kwork не ответил стабильно. Поиск по Telegram продолжает работать. Ошибка: {Truncate(error, 180)}";

            await _notificationService.NotifySearchStoppedAsync(
                account,
                message,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify account {AccountId} about stopped Kwork search", account.Id);
        }
    }

    private async Task<List<SearchCandidateJobDto>> FetchKworkCandidatesAsync(Guid profileId, ExchangeConnection connection, string session, CancellationToken cancellationToken)
    {
        var scanStartedAt = DateTimeOffset.UtcNow;
        var scanLog = new StringBuilder();
        scanLog.AppendLine("ClientScout Kwork Scan Debug");
        scanLog.AppendLine($"StartedAtUtc: {scanStartedAt:O}");
        scanLog.AppendLine($"ProfileId: {profileId}");
        scanLog.AppendLine($"ConnectionId: {connection.Id}");
        scanLog.AppendLine($"IsConnected: {connection.IsConnected}");
        scanLog.AppendLine($"RequiresReconnect: {connection.RequiresReconnect}");
        scanLog.AppendLine($"HasSession: {!string.IsNullOrWhiteSpace(session)}");
        scanLog.AppendLine();

        var source = await GetOrCreateKworkSourceAsync(profileId, cancellationToken);
        scanLog.AppendLine($"SourceId: {source.Id}");
        scanLog.AppendLine($"SourceLastScraped: {source.LastScraped:O}");
        scanLog.AppendLine();

        try
        {
            await using var browserResult = await FetchKworkItemsWithBrowserAsync(session, source, scanLog);
            var items = browserResult.Items;
            scanLog.AppendLine($"ParsedProjectLinksCount: {items.Count}");
            foreach (var item in items.Take(120))
            {
                scanLog.AppendLine($"ParsedLink: {item.Url} | title='{Truncate(item.Title, 180)}'");
            }
            scanLog.AppendLine();

            var settings = await _dbContext.SearchSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);
            scanLog.AppendLine($"SettingsFound: {settings != null}");
            scanLog.AppendLine($"UserKeywords: {string.Join(", ", settings?.UserKeywords ?? Array.Empty<string>())}");
            scanLog.AppendLine($"NegativeKeywords: {string.Join(", ", settings?.NegativeKeywords ?? Array.Empty<string>())}");
            scanLog.AppendLine($"ExpandedPositiveTermsCount: {settings?.ExpandedPositiveTerms.Length ?? 0}");
            scanLog.AppendLine($"ExpandedIntentTermsCount: {settings?.ExpandedIntentTerms.Length ?? 0}");
            scanLog.AppendLine($"StrongTermsCount: {settings?.StrongTerms.Length ?? 0}");
            scanLog.AppendLine();

            const int maxKworkDetailsPerScan = 240;
            var result = new List<SearchCandidateJobDto>();
            foreach (var item in items)
            {
                if (result.Count >= maxKworkDetailsPerScan)
                {
                    scanLog.AppendLine($"DetailLimitReached: {maxKworkDetailsPerScan}");
                    break;
                }

                if (settings != null)
                {
                    var listPrefilter = _candidateFilter.Evaluate($"{item.Title} {item.Description}", settings);
                    scanLog.AppendLine($"ListItem: {item.Url}");
                    scanLog.AppendLine($"  ListTitle: {Truncate(item.Title, 250)}");
                    scanLog.AppendLine($"  ListPrefilterIsCandidate: {listPrefilter.IsCandidate}");
                    scanLog.AppendLine($"  ListPrefilterScore: {listPrefilter.Score}");
                    scanLog.AppendLine($"  ListPrefilterRejectionReason: {listPrefilter.RejectionReason}");
                    scanLog.AppendLine($"  ListPrefilterMatchedTerms: {string.Join(", ", listPrefilter.MatchedTerms)}");
                    if (!listPrefilter.IsCandidate && !listPrefilter.MatchedTerms.Any() && !listPrefilter.MatchedStrongTerms.Any())
                    {
                        scanLog.AppendLine();
                        continue;
                    }
                }

                var detail = await FetchKworkDetailWithBrowserAsync(browserResult.Context, item);
                var externalId = ExtractKworkExternalId(detail.Url);
                scanLog.AppendLine($"Detail: {detail.Url}");
                scanLog.AppendLine($"  DetailStatus: {detail.StatusCode} {detail.StatusText}");
                scanLog.AppendLine($"  DetailHtmlLength: {detail.HtmlLength}");
                scanLog.AppendLine($"  ExternalId: {externalId}");
                scanLog.AppendLine($"  Title: {Truncate(detail.Title, 250)}");
                scanLog.AppendLine($"  DescriptionSnippet: {Truncate(detail.Description, 800)}");

                var exists = await _dbContext.JobLeads
                    .AnyAsync(lead => lead.SourceId == source.Id && lead.ExternalId == externalId, cancellationToken);
                scanLog.AppendLine($"  AlreadyExistsInJobLeads: {exists}");
                if (exists)
                {
                    scanLog.AppendLine();
                    continue;
                }

                if (settings != null)
                {
                    var prefilter = _candidateFilter.Evaluate($"{detail.Title} {detail.Description}", settings);
                    scanLog.AppendLine($"  PrefilterIsCandidate: {prefilter.IsCandidate}");
                    scanLog.AppendLine($"  PrefilterScore: {prefilter.Score}");
                    scanLog.AppendLine($"  PrefilterRejectionReason: {prefilter.RejectionReason}");
                    scanLog.AppendLine($"  PrefilterMatchedTerms: {string.Join(", ", prefilter.MatchedTerms)}");
                    scanLog.AppendLine($"  PrefilterStrongTerms: {prefilter.MatchedStrongTerms}");
                }

                result.Add(new SearchCandidateJobDto(
                    profileId,
                    source.Id,
                    externalId,
                    detail.Title,
                    string.IsNullOrWhiteSpace(detail.Description) ? detail.Title : $"{detail.Title}\n{detail.Description}",
                    detail.Url,
                    null));
                scanLog.AppendLine("  CandidateQueued: true");
                scanLog.AppendLine();
            }

            source.LastScraped = DateTimeOffset.UtcNow;
            source.LastError = null;
            scanLog.AppendLine($"CandidatesQueuedCount: {result.Count}");
            return result;
        }
        catch (Exception ex)
        {
            scanLog.AppendLine($"Exception occurred during scan: {ex.GetType().Name}: {ex.Message}");
            scanLog.AppendLine(ex.StackTrace);
            throw;
        }
        finally
        {
            scanLog.AppendLine($"FinishedAtUtc: {DateTimeOffset.UtcNow:O}");
            WriteKworkScanDebugFile(profileId, scanStartedAt, scanLog);
        }
    }

    private static async Task<KworkBrowserScanResult> FetchKworkItemsWithBrowserAsync(string session, Source source, StringBuilder scanLog)
    {
        scanLog.AppendLine("BrowserScan: true");

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--disable-blink-features=AutomationControlled"]
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "ru-RU",
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        });

        var cookies = ParseCookieHeader(session).ToArray();
        scanLog.AppendLine($"BrowserCookiesAdded: {cookies.Length}");
        if (cookies.Length > 0)
        {
            await context.AddCookiesAsync(cookies);
        }

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync(BuildKworkProjectsPageUrl(1), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });

        scanLog.AppendLine($"BrowserProjectsStatus: {response?.Status}");
        scanLog.AppendLine($"BrowserProjectsUrl: {page.Url}");

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });
        }
        catch (TimeoutException)
        {
            scanLog.AppendLine("BrowserNetworkIdle: timeout");
        }

        await page.WaitForTimeoutAsync(5000);
        await SelectAllKworkRubricsAsync(page, scanLog);

        var firstPageResult = await ReadKworkItemsFromCurrentPageAsync(page, response?.Status ?? 0, scanLog, 1);
        if (IsKworkAccessBlocked(page.Url, firstPageResult.BodyText))
        {
            await context.CloseAsync();
            await browser.CloseAsync();
            playwright.Dispose();
            scanLog.AppendLine("Result: KWORK_ACCESS_BLOCKED");
            throw new UnauthorizedAccessException("KWORK_ACCESS_BLOCKED: Kwork temporarily blocked automated access. Open Kwork in browser and complete anti-bot check, then reconnect Kwork.");
        }

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            (firstPageResult.Html.Contains("login", StringComparison.OrdinalIgnoreCase) &&
             firstPageResult.Html.Contains("password", StringComparison.OrdinalIgnoreCase) &&
             !firstPageResult.Html.Contains("/projects/", StringComparison.OrdinalIgnoreCase)))
        {
            await context.CloseAsync();
            await browser.CloseAsync();
            playwright.Dispose();
            scanLog.AppendLine("Result: KWORK_SESSION_EXPIRED_BY_BROWSER");
            throw new UnauthorizedAccessException("KWORK_SESSION_EXPIRED");
        }

        var items = new List<KworkItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddKworkItems(items, seen, firstPageResult.Items);

        var lastPage = await DetectKworkLastPageAsync(page, scanLog);
        scanLog.AppendLine($"DetectedLastPage: {lastPage}");
        const int maxOlderPagesPerScan = 5;
        var pagesToRead = BuildKworkPageBatch(source, lastPage, maxOlderPagesPerScan, scanLog);
        foreach (var pageNumber in pagesToRead)
        {
            var pageUrl = BuildKworkProjectsPageUrl(pageNumber);
            var pageResponse = await page.GotoAsync(pageUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 12000 });
            }
            catch (TimeoutException)
            {
                scanLog.AppendLine($"BrowserPage{pageNumber}NetworkIdle: timeout");
            }

            await page.WaitForTimeoutAsync(1200);
            var pageResult = await ReadKworkItemsFromCurrentPageAsync(page, pageResponse?.Status ?? 0, scanLog, pageNumber);
            if (IsKworkAccessBlocked(page.Url, pageResult.BodyText))
            {
                scanLog.AppendLine($"Result: KWORK_ACCESS_BLOCKED_ON_PAGE_{pageNumber}");
                throw new UnauthorizedAccessException("KWORK_ACCESS_BLOCKED: Kwork temporarily blocked automated access. Open Kwork in browser and complete anti-bot check, then reconnect Kwork.");
            }

            AddKworkItems(items, seen, pageResult.Items);
            await page.WaitForTimeoutAsync(4000);
        }

        scanLog.AppendLine($"BrowserTotalProjectLinksCount: {items.Count}");
        return new KworkBrowserScanResult(playwright, browser, context, items);
    }

    private static async Task<KworkPageReadResult> ReadKworkItemsFromCurrentPageAsync(IPage page, int statusCode, StringBuilder scanLog, int pageNumber)
    {
        var html = await page.ContentAsync();
        var bodyText = await page.EvaluateAsync<string>("() => document.body ? document.body.innerText : ''");
        scanLog.AppendLine($"BrowserPage: {pageNumber}");
        scanLog.AppendLine($"BrowserPageUrl: {page.Url}");
        scanLog.AppendLine($"BrowserHtmlLength: {html.Length}");
        scanLog.AppendLine($"BrowserBodyTextLength: {bodyText.Length}");
        scanLog.AppendLine($"BrowserHtmlContainsLoginPassword: {html.Contains("login", StringComparison.OrdinalIgnoreCase) && html.Contains("password", StringComparison.OrdinalIgnoreCase)}");
        scanLog.AppendLine($"BrowserBodyContains3183617: {bodyText.Contains("3183617", StringComparison.OrdinalIgnoreCase) || html.Contains("3183617", StringComparison.OrdinalIgnoreCase)}");
        scanLog.AppendLine($"BrowserBodyContains3163214: {bodyText.Contains("3163214", StringComparison.OrdinalIgnoreCase) || html.Contains("3163214", StringComparison.OrdinalIgnoreCase)}");
        scanLog.AppendLine();

        var domLinks = await page.EvaluateAsync<string[]>(
            """
            () => Array.from(document.querySelectorAll('a[href*="/projects/"], a[href*="project"]'))
              .filter(a => /\/projects\/\d+(?:\/view)?(?:[?#].*)?$/.test(a.getAttribute('href') || ''))
              .map(a => {
                const href = a.getAttribute('href');
                const text = (a.textContent || '').replace(/\s+/g, ' ').trim();
                let container = a;
                let foundContainer = false;
                for (let i = 0; i < 6 && container.parentElement; i++) {
                  container = container.parentElement;
                  const containerText = (container.textContent || '').replace(/\s+/g, ' ').trim();
                  if (
                    containerText.includes('РџРѕРєСѓРїР°С‚РµР»СЊ:') ||
                    containerText.includes('РџСЂРµРґР»РѕР¶РµРЅРёР№:') ||
                    containerText.includes('Р–РµР»Р°РµРјС‹Р№ Р±СЋРґР¶РµС‚') ||
                    containerText.includes('Р”РѕРїСѓСЃС‚РёРјС‹Р№:') ||
                    containerText.includes('РћСЃС‚Р°Р»РѕСЃСЊ')
                  ) {
                    foundContainer = true;
                    break;
                  }
                }

                let description = text;
                if (foundContainer) {
                  const clone = container.cloneNode(true);
                  const metaKeywords = ['Р–РµР»Р°РµРјС‹Р№ Р±СЋРґР¶РµС‚', 'Р”РѕРїСѓСЃС‚РёРјС‹Р№', 'РџРѕРєСѓРїР°С‚РµР»СЊ:', 'РџСЂРµРґР»РѕР¶РµРЅРёР№:', 'РћСЃС‚Р°Р»РѕСЃСЊ', 'РќР°РЅСЏС‚Рѕ', 'Р Р°Р·РјРµС‰РµРЅРѕ', 'РџСЂРѕСЃРјРѕС‚СЂРѕРІ:'];
                  Array.from(clone.querySelectorAll('*')).forEach(el => {
                    const elText = el.textContent || '';
                    if (metaKeywords.some(k => elText.includes(k))) {
                      el.remove();
                    }
                  });
                  Array.from(clone.querySelectorAll('a')).forEach(el => {
                    const elHref = el.getAttribute('href') || '';
                    if (!elHref.includes('/view')) {
                      el.remove();
                    }
                  });
                  // Remove the title itself to avoid duplication
                  Array.from(clone.querySelectorAll('a')).forEach(el => {
                    const elHref = el.getAttribute('href') || '';
                    if (elHref.includes('/view')) {
                      el.remove();
                    }
                  });
                  description = (clone.textContent || '').replace(/\s+/g, ' ').trim();
                  if (!description || description.length < 5) description = text;
                }

                return new URL(href, location.href).href + '\t' + text + '\t' + description;
              })
            """);

        var items = new List<KworkItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in domLinks)
        {
            var parts = line.Split('\t', 2);
            var fullParts = line.Split('\t', 3);
            var url = parts[0].Trim();
            var title = fullParts.Length > 1 ? fullParts[1].Trim() : string.Empty;
            var description = fullParts.Length > 2 ? fullParts[2].Trim() : title;
            if (seen.Add(url))
            {
                items.Add(new KworkItem(string.IsNullOrWhiteSpace(title) ? "Kwork Р·Р°РєР°Р·" : title, description, url)
                {
                    StatusCode = statusCode,
                    StatusText = $"dom-page-{pageNumber}"
                });
            }
        }

        foreach (var item in ParseKworkItems(html))
        {
            if (seen.Add(item.Url))
            {
                items.Add(item with { StatusText = $"html-page-{pageNumber}" });
            }
        }

        if (items.Count == 0)
        {
            var textItems = ParseKworkItemsFromRenderedText(bodyText).ToList();
            scanLog.AppendLine($"RenderedTextProjectItemsCount: {textItems.Count}");
            foreach (var item in textItems)
            {
                if (seen.Add(item.Url))
                {
                    items.Add(item with { StatusText = $"rendered-text-page-{pageNumber}" });
                }
            }
        }

        scanLog.AppendLine($"DomProjectLinksCount: {domLinks.Length}");
        scanLog.AppendLine($"BrowserPageProjectLinksCount: {items.Count}");
        return new KworkPageReadResult(html, bodyText, items);
    }

    private static void AddKworkItems(List<KworkItem> destination, HashSet<string> seen, IEnumerable<KworkItem> items)
    {
        foreach (var item in items)
        {
            if (seen.Add(item.Url))
            {
                destination.Add(item);
            }
        }
    }

    private static List<int> BuildKworkPageBatch(Source source, int lastPage, int maxOlderPagesPerScan, StringBuilder scanLog)
    {
        var pages = new List<int>();
        if (lastPage < 2 || maxOlderPagesPerScan <= 0)
        {
            source.Credentials = WriteMarker(source.Credentials, "kworkNextPage", 4);
            source.Credentials = WriteMarker(source.Credentials, "kworkFullScanCompleted", true);
            scanLog.AppendLine("KworkPageBatch: first-page-only");
            return pages;
        }

        for (var hotPage = 2; hotPage <= Math.Min(3, lastPage); hotPage++)
        {
            pages.Add(hotPage);
        }

        if (ReadBoolMarker(source.Credentials, "kworkFullScanCompleted"))
        {
            scanLog.AppendLine($"KworkPageBatch: 1,{string.Join(",", pages)}");
            scanLog.AppendLine("KworkFullScanCompleted: true");
            return pages;
        }

        var cursor = ReadMarker(source.Credentials, "kworkNextPage");
        if (cursor < 4 || cursor > lastPage)
        {
            cursor = 4;
        }

        var page = cursor;
        var olderPagesRead = 0;
        while (page <= lastPage && olderPagesRead < maxOlderPagesPerScan)
        {
            if (!pages.Contains(page))
            {
                pages.Add(page);
                olderPagesRead++;
            }

            page++;
        }

        var fullScanCompleted = page > lastPage;
        source.Credentials = WriteMarker(source.Credentials, "kworkNextPage", fullScanCompleted ? lastPage + 1 : page);
        source.Credentials = WriteMarker(source.Credentials, "kworkFullScanCompleted", fullScanCompleted);
        scanLog.AppendLine($"KworkPageBatch: 1,{string.Join(",", pages)}");
        scanLog.AppendLine($"KworkNextPageCursor: {page}");
        scanLog.AppendLine($"KworkFullScanCompleted: {fullScanCompleted}");
        return pages;
    }

    private static async Task<int> DetectKworkLastPageAsync(IPage page, StringBuilder scanLog)
    {
        try
        {
            var lastPage = await page.EvaluateAsync<int>(
                """
                () => {
                  const pages = new Set([1]);
                  for (const anchor of Array.from(document.querySelectorAll('a[href]'))) {
                    try {
                      const url = new URL(anchor.getAttribute('href'), location.href);
                      const value = url.searchParams.get('page') || url.searchParams.get('p');
                      const page = Number.parseInt(value || '', 10);
                      if (Number.isFinite(page) && page > 0 && page < 1000) pages.add(page);
                    } catch {}
                  }

                  const docHeight = Math.max(document.body.scrollHeight || 0, document.documentElement.scrollHeight || 0);
                  const bottomLine = Math.max(0, docHeight - 900);
                  for (const el of Array.from(document.querySelectorAll('a, button, span, div'))) {
                    const text = (el.textContent || '').trim();
                    if (!/^\d+$/.test(text)) continue;
                    const box = el.getBoundingClientRect();
                    const absoluteTop = box.top + window.scrollY;
                    if (box.width <= 0 || box.height <= 0 || absoluteTop < bottomLine) continue;
                    const page = Number.parseInt(text, 10);
                    if (Number.isFinite(page) && page > 0 && page < 1000) pages.add(page);
                  }

                  return Math.max(...Array.from(pages));
                }
                """);

            return Math.Max(1, lastPage);
        }
        catch (Exception ex)
        {
            scanLog.AppendLine($"DetectLastPageError: {ex.GetType().Name}:{ex.Message}");
            return 1;
        }
    }

    private static string BuildKworkProjectsPageUrl(int pageNumber)
    {
        return pageNumber <= 1
            ? "https://kwork.ru/projects?c=all"
            : $"https://kwork.ru/projects?c=all&page={pageNumber}";
    }

    private static bool IsKworkAccessBlocked(string url, string bodyText)
    {
        return url.Contains("not_access", StringComparison.OrdinalIgnoreCase) ||
               bodyText.Contains("Р·Р°Р±Р»РѕРєРёСЂРѕРІР°РЅ РґРѕСЃС‚СѓРї", StringComparison.OrdinalIgnoreCase) ||
               bodyText.Contains("РїРѕРґС‚РІРµСЂРґРёС‚Рµ, С‡С‚Рѕ РІС‹ РЅРµ СЂРѕР±РѕС‚", StringComparison.OrdinalIgnoreCase) ||
               bodyText.Contains("Р±РѕР»СЊС€РѕР№ РЅР°РіСЂСѓР·РєРѕР№", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<KworkItem> FetchKworkDetailWithBrowserAsync(IBrowserContext context, KworkItem item)
    {
        if (item.Url.StartsWith("internal://", StringComparison.OrdinalIgnoreCase))
        {
            return item;
        }

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync(item.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
            }
            catch (TimeoutException)
            {
                // Rendered text is still usually available after DOMContentLoaded.
            }

            await page.WaitForTimeoutAsync(1500);
            var html = await page.ContentAsync();
            var data = await page.EvaluateAsync<string[]>(
                """
                () => {
                  const title = (document.querySelector('h1')?.textContent || document.querySelector('meta[property="og:title"]')?.content || document.title || '').replace(/\s+/g, ' ').trim();
                  const meta = document.querySelector('meta[property="og:description"]')?.content || document.querySelector('meta[name="description"]')?.content || '';
                  
                  const descEl = document.querySelector('.project-detail__description') || document.querySelector('.wants-card__description-text');
                  let body = '';
                  if (descEl) {
                      body = descEl.textContent;
                  } else {
                      const main = document.querySelector('main') || document.querySelector('.center-block') || document.body;
                      let clone = main.cloneNode(true);
                      const toRemove = ['.sidebar', '.header', '.footer', '.navbar', '.menu', 'nav', 'header', 'footer', 'aside'];
                      toRemove.forEach(sel => {
                          Array.from(clone.querySelectorAll(sel)).forEach(el => el.remove());
                      });
                      body = clone.textContent;
                  }
                  body = (body || '').replace(/\s+/g, ' ').trim();
                  
                  return [title, meta, body];
                }
                """);

            var title = CleanHtml(data.ElementAtOrDefault(0) ?? item.Title);
            var metaDescription = CleanHtml(data.ElementAtOrDefault(1) ?? string.Empty);
            var bodyText = CleanHtml(data.ElementAtOrDefault(2) ?? string.Empty);
            var description = !string.IsNullOrWhiteSpace(metaDescription) ? metaDescription : bodyText;

            return new KworkItem(string.IsNullOrWhiteSpace(title) ? item.Title : title, description, item.Url)
            {
                StatusCode = response?.Status ?? 0,
                StatusText = response?.StatusText,
                HtmlLength = html.Length
            };
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task SelectAllKworkRubricsAsync(IPage page, StringBuilder scanLog)
    {
        try
        {
            var target = await page.EvaluateAsync<double[]?>(
                """
                () => {
                  const elements = Array.from(document.querySelectorAll('button, a, div, span, label'));
                  const rubricTitle = elements.find(el => (el.textContent || '').trim() === 'Р СѓР±СЂРёРєРё');
                  const favorite = elements.find(el => (el.textContent || '').trim() === 'Р›СЋР±РёРјС‹Рµ');
                  const rubricTop = rubricTitle ? rubricTitle.getBoundingClientRect().top : 0;
                  const favoriteBox = favorite ? favorite.getBoundingClientRect() : null;
                  const candidates = elements
                    .filter(el => (el.textContent || '').trim() === 'Р’СЃРµ')
                    .map(el => ({ el, box: el.getBoundingClientRect() }))
                    .filter(x => x.box.width > 0 && x.box.height > 0 && x.box.top >= rubricTop)
                    .filter(x => !favoriteBox || Math.abs(x.box.top - favoriteBox.top) < 80)
                    .sort((a, b) => a.box.top - b.box.top);

                  if (!candidates.length) return null;
                  const box = candidates[0].box;
                  return [box.left + box.width / 2, box.top + box.height / 2];
                }
                """);

            if (target == null || target.Length < 2)
            {
                scanLog.AppendLine("RubricsAllClickResult: all_button_not_found");
                return;
            }

            scanLog.AppendLine($"RubricsAllClickTarget: all@{target[0]:0},{target[1]:0}");
            await page.Mouse.ClickAsync((float)target[0], (float)target[1]);
            await page.WaitForTimeoutAsync(3500);
            scanLog.AppendLine("RubricsAllClickResult: trusted_mouse_click");

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 12000 });
                scanLog.AppendLine("RubricsAllNetworkIdle: ok");
            }
            catch (TimeoutException)
            {
                scanLog.AppendLine("RubricsAllNetworkIdle: timeout");
            }

            await page.WaitForTimeoutAsync(2500);
        }
        catch (Exception ex)
        {
            scanLog.AppendLine($"RubricsAllClickResult: exception:{ex.GetType().Name}:{ex.Message}");
        }
    }

    private static async Task<KworkItem> FetchKworkDetailAsync(HttpClient client, KworkItem item, string session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 ClientScout/1.0");
        request.Headers.TryAddWithoutValidation("Cookie", session);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return item with
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.StatusCode.ToString()
            };
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var title =
            TryMatch(html, "<h1[^>]*>(?<value>.*?)</h1>") ??
            TryMeta(html, "og:title") ??
            item.Title;

        var description =
            TryMeta(html, "og:description") ??
            TryMatch(html, "<div[^>]+class=[\"'][^\"']*(?:description|desc|project)[^\"']*[\"'][^>]*>(?<value>.*?)</div>") ??
            CreateDescriptionFromHtml(html);

        return new KworkItem(CleanHtml(title), CleanHtml(description), item.Url)
        {
            StatusCode = (int)response.StatusCode,
            StatusText = response.StatusCode.ToString(),
            HtmlLength = html.Length
        };
    }

    private async Task<Source> GetOrCreateKworkSourceAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources.FirstOrDefaultAsync(s =>
            s.ProfileId == profileId && s.Type == SourceType.Kwork && s.Url == "https://kwork.ru/projects", cancellationToken);

        if (source != null)
        {
            if (source.Status != SourceStatus.Active)
            {
                source.Status = SourceStatus.Active;
                source.LastError = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return source;
        }

        source = new Source
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Type = SourceType.Kwork,
            Name = "Kwork",
            Url = "https://kwork.ru/projects",
            Credentials = JsonSerializer.Serialize(new { purpose = 0 }),
            Status = SourceStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return source;
    }

    private static IEnumerable<KworkItem> ParseKworkItems(string html)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            html,
            "<a[^>]+href=[\"'](?<href>[^\"']*/projects/\\d+/view[^\"']*)[\"'][^>]*>(?<title>.*?)</a>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var title = CleanHtml(match.Groups["title"].Value);
            var href = match.Groups["href"].Value.Trim();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : $"https://kwork.ru{href}";
            if (!seenUrls.Add(url))
            {
                continue;
            }

            yield return new KworkItem(string.IsNullOrWhiteSpace(title) ? "Kwork Р·Р°РєР°Р·" : title, title, url);
        }
    }

    private static IEnumerable<KworkItem> ParseKworkItemsFromRenderedText(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            yield break;
        }

        var lines = bodyText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var started = false;
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (!started)
            {
                if (line.Equals("Р’СЃРµ РїСЂРµРґР»РѕР¶РµРЅРёСЏ", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("РќРѕРІС‹Рµ", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("РџСЂРѕСЃРјРѕС‚СЂРµРЅРЅС‹Рµ", StringComparison.OrdinalIgnoreCase))
                {
                    started = true;
                }

                index++;
                continue;
            }

            if (IsKworkNoiseLine(line))
            {
                index++;
                continue;
            }

            var title = line;
            var chunk = new List<string> { title };
            index++;

            while (index < lines.Length && chunk.Count < 18)
            {
                var next = lines[index];
                if (IsLikelyKworkCardStart(next, lines, index) && chunk.Any(item => item.Contains("РџСЂРµРґР»РѕР¶РµРЅРёР№", StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }

                chunk.Add(next);
                if (next.Contains("РџСЂРµРґР»РѕР¶РµРЅРёР№", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    break;
                }

                index++;
            }

            var description = string.Join("\n", chunk);
            if (description.Length < 40 || IsKworkNoiseLine(title))
            {
                continue;
            }

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(description)))[..16].ToLowerInvariant();
            yield return new KworkItem(title, description, $"internal://kwork-rendered/{hash}")
            {
                StatusCode = 200,
                StatusText = "rendered-text",
                HtmlLength = bodyText.Length
            };
        }
    }

    private static bool IsLikelyKworkCardStart(string line, string[] lines, int index)
    {
        if (IsKworkNoiseLine(line) || line.Length < 8 || line.Length > 160)
        {
            return false;
        }

        var window = string.Join(" ", lines.Skip(index).Take(8));
        return window.Contains("Р–РµР»Р°РµРјС‹Р№ Р±СЋРґР¶РµС‚", StringComparison.OrdinalIgnoreCase) ||
               window.Contains("Р”РѕРїСѓСЃС‚РёРјС‹Р№", StringComparison.OrdinalIgnoreCase) ||
               window.Contains("РџРѕРєСѓРїР°С‚РµР»СЊ", StringComparison.OrdinalIgnoreCase) ||
               window.Contains("РџСЂРµРґР»РѕР¶РµРЅРёР№", StringComparison.OrdinalIgnoreCase) ||
               window.Contains("РћСЃС‚Р°Р»РѕСЃСЊ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKworkNoiseLine(string line)
    {
        var noise = new[]
        {
            "Р¤Р РР›РђРќРЎ РњРђР РљР•РўРџР›Р•Р™РЎ",
            "РљРІРѕСЂРєРё",
            "Р—Р°РєР°Р·С‹",
            "Р‘РёСЂР¶Р°",
            "РџРѕСЂС‚С„РѕР»РёРѕ",
            "Р§Р°С‚",
            "Р”РёР·Р°Р№РЅ",
            "Р Р°Р·СЂР°Р±РѕС‚РєР° Рё IT",
            "РўРµРєСЃС‚С‹ Рё РїРµСЂРµРІРѕРґС‹",
            "SEO Рё С‚СЂР°С„РёРє",
            "РЎРѕС†СЃРµС‚Рё Рё РјР°СЂРєРµС‚РёРЅРі",
            "РђСѓРґРёРѕ, РІРёРґРµРѕ, СЃСЉРµРјРєР°",
            "Р‘РёР·РЅРµСЃ Рё Р¶РёР·РЅСЊ",
            "Р‘РёСЂР¶Р° РїСЂРѕРµРєС‚РѕРІ",
            "РњРѕРё РѕС‚РєР»РёРєРё",
            "РџСЂРѕРµРєС‚С‹",
            "РљРѕРЅРЅРµРєС‚С‹",
            "Р СѓР±СЂРёРєРё",
            "Р›СЋР±РёРјС‹Рµ",
            "Р’СЃРµ",
            "Р‘СЋРґР¶РµС‚",
            "РќР°РЅСЏС‚Рѕ, %",
            "РљР»СЋС‡РµРІС‹Рµ СЃР»РѕРІР°",
            "РљРѕР»РёС‡РµСЃС‚РІРѕ РїСЂРµРґР»РѕР¶РµРЅРёР№",
            "РЎС‚Р°С‚РёСЃС‚РёРєР° Р±РёСЂР¶Рё",
            "РџРѕРєР°Р·Р°С‚СЊ:",
            "РІСЃРµ РїСЂРµРґР»РѕР¶РµРЅРёСЏ",
            "Р’СЃРµ РїСЂРµРґР»РѕР¶РµРЅРёСЏ",
            "РќРѕРІС‹Рµ",
            "РџСЂРѕСЃРјРѕС‚СЂРµРЅРЅС‹Рµ"
        };

        return noise.Any(value => line.Equals(value, StringComparison.OrdinalIgnoreCase)) ||
               line.StartsWith("РћСЃС‚Р°Р»РѕСЃСЊ ", StringComparison.OrdinalIgnoreCase) && line.Contains(" РёР· ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Р”Р°С‚Р° РїРѕРїРѕР»РЅРµРЅРёСЏ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("РќР°СЃС‚СЂРѕР№РєР° СѓРІРµРґРѕРјР»РµРЅРёР№", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("РЈР·РЅР°Р№С‚Рµ,", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("РћС‚РєСЂС‹С‚СЊ СѓСЂРѕРє", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDue(DateTimeOffset? lastRun, int intervalMinutes, TimeSpan jitter, DateTimeOffset now)
    {
        if (lastRun == null)
        {
            return true;
        }

        return lastRun.Value.AddMinutes(intervalMinutes).Add(jitter) <= now;
    }

    private static int ReadPurpose(string? credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials)) return 0;
        try
        {
            var node = JsonNode.Parse(credentials);
            return node?["Purpose"]?.GetValue<int>() ?? node?["purpose"]?.GetValue<int>() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadMarker(string? credentials, string key)
    {
        if (string.IsNullOrWhiteSpace(credentials)) return 0;
        try
        {
            var node = JsonNode.Parse(credentials);
            return node?[key]?.GetValue<int>() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string? ReadStringMarker(string? credentials, string key)
    {
        if (string.IsNullOrWhiteSpace(credentials)) return null;
        try
        {
            var node = JsonNode.Parse(credentials);
            return node?[key]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadBoolMarker(string? credentials, string key)
    {
        if (string.IsNullOrWhiteSpace(credentials)) return false;
        try
        {
            var node = JsonNode.Parse(credentials);
            return node?[key]?.GetValue<bool>() ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static string WriteMarker(string? credentials, string key, int value)
    {
        JsonObject obj;
        try
        {
            obj = JsonNode.Parse(string.IsNullOrWhiteSpace(credentials) ? "{}" : credentials) as JsonObject ?? new JsonObject();
        }
        catch
        {
            obj = new JsonObject();
        }

        obj[key] = value;
        return obj.ToJsonString();
    }

    private static string WriteMarker(string? credentials, string key, bool value)
    {
        JsonObject obj;
        try
        {
            obj = JsonNode.Parse(string.IsNullOrWhiteSpace(credentials) ? "{}" : credentials) as JsonObject ?? new JsonObject();
        }
        catch
        {
            obj = new JsonObject();
        }

        obj[key] = value;
        return obj.ToJsonString();
    }

    private static string DecodeSession(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch { return value; }
    }

    private static string CreateTitle(string text)
    {
        text = CleanHtml(text);
        return text.Length <= 90 ? text : text[..90].Trim() + "...";
    }

    private static string CleanHtml(string value)
    {
        value = System.Net.WebUtility.HtmlDecode(value);
        value = System.Text.RegularExpressions.Regex.Replace(value, "<script.*?</script>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        value = System.Text.RegularExpressions.Regex.Replace(value, "<style.*?</style>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        value = System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", " ");
        value = System.Text.RegularExpressions.Regex.Replace(value, "\\s+", " ");
        return value.Trim();
    }

    private static string ExtractKworkExternalId(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/projects/(?<id>\d+)(?:/view)?(?:[?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return $"kwork:{match.Groups["id"].Value}";
        }

        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return $"kwork:{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }

    private static string? TryMeta(string html, string property)
    {
        return TryMatch(html, $"<meta[^>]+(?:property|name)=[\"']{System.Text.RegularExpressions.Regex.Escape(property)}[\"'][^>]+content=[\"'](?<value>.*?)[\"'][^>]*>")
            ?? TryMatch(html, $"<meta[^>]+content=[\"'](?<value>.*?)[\"'][^>]+(?:property|name)=[\"']{System.Text.RegularExpressions.Regex.Escape(property)}[\"'][^>]*>");
    }

    private static string? TryMatch(string html, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        return match.Success ? match.Groups["value"].Value : null;
    }

    private static IEnumerable<Cookie> ParseCookieHeader(string session)
    {
        if (string.IsNullOrWhiteSpace(session))
        {
            yield break;
        }

        foreach (var rawPart in session.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = rawPart.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var name = rawPart[..index].Trim();
            var value = rawPart[(index + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new Cookie
            {
                Name = name,
                Value = value,
                Domain = ".kwork.ru",
                Path = "/",
                Secure = true
            };
        }
    }

    private static string CreateDescriptionFromHtml(string html)
    {
        var text = CleanHtml(html);
        return text.Length <= 4000 ? text : text[..4000];
    }

    private static void WriteKworkScanDebugFile(Guid profileId, DateTimeOffset scanStartedAt, StringBuilder builder)
    {
        try
        {
            var directory = Path.Combine(FindProjectRoot(), "debug", "kwork-scans");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"{scanStartedAt:yyyyMMdd-HHmmss}-{profileId}.txt");
            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Diagnostics should not break scanning.
        }
    }

    private static void WriteTelegramScanDebugFile(Guid sourceId, DateTimeOffset scanStartedAt, StringBuilder builder)
    {
        try
        {
            var directory = Path.Combine(FindProjectRoot(), "debug", "telegram-scans");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"{scanStartedAt:yyyyMMdd-HHmmss}-{sourceId}.txt");
            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Diagnostics should not break scanning.
        }
    }

    private static string ToFriendlyTelegramSourceError(Exception ex)
    {
        var message = ex.Message;
        var normalized = message.ToUpperInvariant();
        if (normalized.Contains("NOT_A_MEMBER") ||
            normalized.Contains("USER_NOT_PARTICIPANT") ||
            normalized.Contains("CHANNEL_PRIVATE") ||
            normalized.Contains("CHAT_WRITE_FORBIDDEN") ||
            normalized.Contains("CHAT_ADMIN_REQUIRED"))
        {
            return "NOT_A_MEMBER: user left the chat or the chat is unavailable.";
        }

        if (normalized.Contains("NOT_AUTHORIZED") || normalized.Contains("AUTH_KEY"))
        {
            return "NOT_AUTHORIZED: Telegram account should be connected again.";
        }

        if (normalized.Contains("NOT_FOUND") || normalized.Contains("INVALID TELEGRAM URL") || normalized.Contains("NOT A CHANNEL/GROUP"))
        {
            return "NOT_FOUND: chat was deleted or the link is invalid.";
        }

        return message;
    }

    private static bool IsTransientTelegramClientError(Exception ex)
    {
        var msg = ex.Message;
        return ex is NullReferenceException ||
               msg.Contains("Object reference", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("You must connect to Telegram first", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("must connect", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("connection", StringComparison.OrdinalIgnoreCase) && msg.Contains("closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("WTException", StringComparison.OrdinalIgnoreCase) && msg.Contains("connect", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteCandidateDebugFile(
        SearchCandidateJobDto dto,
        Source? source,
        PrefilterResult? prefilter,
        string status,
        LeadDto? lead)
    {
        try
        {
            var isKwork = source?.Type == SourceType.Kwork || dto.ExternalId.StartsWith("kwork:", StringComparison.OrdinalIgnoreCase);
            var isTelegram = source?.Type == SourceType.Telegram || dto.ExternalId.StartsWith("tg:", StringComparison.OrdinalIgnoreCase);
            if (!isKwork && !isTelegram)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var directoryName = isTelegram ? "telegram-candidates" : "kwork-candidates";
            var directory = Path.Combine(FindProjectRoot(), "debug", directoryName);
            Directory.CreateDirectory(directory);
            var safeExternalId = string.Concat(dto.ExternalId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            var filePath = Path.Combine(directory, $"{now:yyyyMMdd-HHmmss}-{safeExternalId}.txt");

            var builder = new StringBuilder();
            builder.AppendLine(isTelegram ? "ClientScout Telegram Candidate Debug" : "ClientScout Kwork Candidate Debug");
            builder.AppendLine($"CreatedAtUtc: {now:O}");
            builder.AppendLine($"Status: {status}");
            builder.AppendLine($"LeadId: {lead?.Id}");
            builder.AppendLine($"ProfileId: {dto.ProfileId}");
            builder.AppendLine($"SourceId: {dto.SourceId}");
            builder.AppendLine($"SourceName: {source?.Name}");
            builder.AppendLine($"ExternalId: {dto.ExternalId}");
            builder.AppendLine($"OriginalUrl: {dto.OriginalUrl}");
            builder.AppendLine($"Title: {dto.Title}");
            builder.AppendLine();

            if (prefilter != null)
            {
                builder.AppendLine($"PrefilterIsCandidate: {prefilter.IsCandidate}");
                builder.AppendLine($"PrefilterScore: {prefilter.Score}");
                builder.AppendLine($"PrefilterRejectionReason: {prefilter.RejectionReason}");
                builder.AppendLine($"PrefilterMatchedTerms: {string.Join(", ", prefilter.MatchedTerms)}");
                builder.AppendLine($"PrefilterStrongTerms: {string.Join(", ", prefilter.MatchedStrongTerms)}");
                builder.AppendLine();
            }

            if (lead != null)
            {
                builder.AppendLine($"AiStatus: {lead.AiStatus}");
                builder.AppendLine($"AiConfidence: {lead.AiConfidence}");
                builder.AppendLine($"AiCategory: {lead.AiCategory}");
                builder.AppendLine($"AiReason: {lead.AiReason}");
                builder.AppendLine($"AiSummary: {lead.AiSummary}");
                builder.AppendLine();
            }

            builder.AppendLine("Content:");
            builder.AppendLine(Truncate(dto.Content, 4000));
            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Diagnostics should not break processing.
        }
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

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= max ? value : value[..max] + "...";
    }

    private sealed record KworkItem(string Title, string Description, string Url)
    {
        public int StatusCode { get; init; }
        public string? StatusText { get; init; }
        public int HtmlLength { get; init; }
    }

    private sealed record KworkPageReadResult(string Html, string BodyText, List<KworkItem> Items);

    private sealed class KworkBrowserScanResult : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;

        public KworkBrowserScanResult(IPlaywright playwright, IBrowser browser, IBrowserContext context, List<KworkItem> items)
        {
            _playwright = playwright;
            _browser = browser;
            Context = context;
            Items = items;
        }

        public IBrowserContext Context { get; }
        public List<KworkItem> Items { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.CloseAsync();
            await _browser.CloseAsync();
            _playwright.Dispose();
        }
    }
}
