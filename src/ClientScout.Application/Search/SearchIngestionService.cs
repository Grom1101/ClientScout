using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Leads;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Search;

public class SearchIngestionService : ISearchIngestionService
{
    private const int AiRetryBatchSize = 10;
    private const int MaxAiRetryLeadsPerRun = 50;
    private static readonly SemaphoreSlim AiRetryLock = new(1, 1);
    private readonly IAppDbContext _dbContext;
    private readonly ISearchCandidateFilter _candidateFilter;
    private readonly IAiLeadClassifier _classifier;
    private readonly ILeadNotificationService _notificationService;

    public SearchIngestionService(
        IAppDbContext dbContext,
        ISearchCandidateFilter candidateFilter,
        IAiLeadClassifier classifier,
        ILeadNotificationService notificationService)
    {
        _dbContext = dbContext;
        _candidateFilter = candidateFilter;
        _classifier = classifier;
        _notificationService = notificationService;
    }

    public async Task<TestCandidateResult> IngestTestCandidateAsync(Guid accountId, TestCandidateRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProfileId && p.AccountId == accountId, cancellationToken);

        if (profile == null)
        {
            throw new UnauthorizedAccessException();
        }

        var source = request.SourceId.HasValue
            ? await _dbContext.Sources.FirstOrDefaultAsync(s => s.Id == request.SourceId.Value && s.ProfileId == request.ProfileId, cancellationToken)
            : await GetOrCreateTestSourceAsync(request.ProfileId, cancellationToken);

        if (source == null)
        {
            throw new InvalidOperationException("SOURCE_NOT_FOUND");
        }

        var text = request.Text.Trim();
        var candidate = new LeadCandidate(
            request.ProfileId,
            source.Id,
            string.IsNullOrWhiteSpace(request.ExternalId) ? CreateStableExternalId(source.Id, text) : request.ExternalId.Trim(),
            request.Title?.Trim(),
            text,
            string.IsNullOrWhiteSpace(request.OriginalUrl) ? source.Url : request.OriginalUrl.Trim(),
            request.AuthorUrl?.Trim(),
            source,
            null);

        var lead = await ProcessCandidateAsync(candidate, cancellationToken);
        if (lead != null)
        {
            return new TestCandidateResult(true, true, null, null, lead);
        }

        var settings = await GetSettingsAsync(request.ProfileId, cancellationToken);
        var prefilter = _candidateFilter.Evaluate($"{request.Title} {request.Text}", settings);
        return new TestCandidateResult(false, prefilter.IsCandidate, prefilter.RejectionReason, null, null);
    }

    public async Task<LeadDto?> ProcessCandidateAsync(LeadCandidate candidate, CancellationToken cancellationToken = default)
    {
        var leads = await ProcessCandidatesAsync([candidate], cancellationToken);
        return leads.FirstOrDefault();
    }

    public async Task<List<LeadDto>> ProcessCandidatesAsync(IReadOnlyCollection<LeadCandidate> candidates, CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var profileId = candidates.First().ProfileId;
        var settings = await GetSettingsAsync(profileId, cancellationToken);
        if (!settings.IsEnabled)
        {
            return [];
        }

        var sourceIds = candidates.Select(candidate => candidate.SourceId).Distinct().ToArray();
        var externalIds = candidates.Select(candidate => candidate.ExternalId).Distinct().ToArray();
        var existing = await _dbContext.JobLeads
            .Where(lead => sourceIds.Contains(lead.SourceId) && externalIds.Contains(lead.ExternalId))
            .Select(lead => new { lead.SourceId, lead.ExternalId })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(lead => $"{lead.SourceId:N}:{lead.ExternalId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prepared = candidates
            .Where(candidate => candidate.ProfileId == profileId)
            .Select(candidate =>
            {
                var rawText = $"{candidate.Title} {candidate.Content}";
                var prefilter = _candidateFilter.Evaluate(rawText, settings);
                return new PreparedLeadCandidate(candidate, rawText, prefilter);
            })
            .Where(item => item.Prefilter.IsCandidate)
            .Where(item => !existingKeys.Contains($"{item.Candidate.SourceId:N}:{item.Candidate.ExternalId}"))
            .ToList();

        if (prepared.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, LeadClassificationResult>? classifications = null;
        var aiUnavailable = false;
        var aiError = false;
        if (_classifier.IsAvailable)
        {
            classifications = await _classifier.ClassifyBatchAsync(
                prepared.Select(item => new BatchLeadClassificationInput(
                        item.Candidate.ExternalId,
                        item.RawText,
                        item.Prefilter.MatchedTerms))
                    .ToArray(),
                prepared[0].Candidate.Source,
                settings,
                cancellationToken);

            classifications ??= await ClassifyPreparedLeadsIndividuallyAsync(prepared, settings, cancellationToken);
            aiError = classifications == null;
        }
        else
        {
            aiUnavailable = true;
        }

        var now = DateTimeOffset.UtcNow;
        var leads = new List<JobLead>();
        foreach (var item in prepared)
        {
            LeadClassificationResult? classification = null;
            classifications?.TryGetValue(item.Candidate.ExternalId, out classification);
            var aiStatus = ResolveAiStatus(classification, aiUnavailable, aiError);

            leads.Add(new JobLead
            {
                Id = Guid.NewGuid(),
                ProfileId = item.Candidate.ProfileId,
                SourceId = item.Candidate.SourceId,
                ExternalId = item.Candidate.ExternalId,
                Title = string.IsNullOrWhiteSpace(item.Candidate.Title) ? CreateTitle(item.Candidate.Content) : item.Candidate.Title,
                Content = item.Candidate.Content,
                OriginalUrl = item.Candidate.OriginalUrl,
                AuthorUrl = item.Candidate.AuthorUrl,
                SourceType = item.Candidate.Source.Type,
                SourceName = BuildLeadSourceName(item.Candidate),
                Status = aiStatus == AiLeadStatus.Rejected ? LeadStatus.Hidden : LeadStatus.New,
                MatchedKeywords = item.Prefilter.MatchedTerms.ToList(),
                MatchedTerms = item.Prefilter.MatchedTerms.ToList(),
                Score = item.Prefilter.Score,
                AiConfidence = classification?.Confidence,
                AiSummary = classification?.Summary,
                AiCategory = classification?.Category,
                AiReason = classification?.Reason,
                AiStatus = aiStatus,
                FoundAt = now,
                ExpiresAt = now.AddHours(24)
            });
        }

        _dbContext.JobLeads.AddRange(leads);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var confirmedLeads = leads.Where(lead => lead.AiStatus == AiLeadStatus.Confirmed).ToList();
        if (settings.NotificationsEnabled && confirmedLeads.Count > 0)
        {
            var account = await _dbContext.Profiles
                .Where(p => p.Id == profileId)
                .Select(p => p.Account)
                .FirstOrDefaultAsync(cancellationToken);

            if (account != null)
            {
                foreach (var lead in confirmedLeads)
                {
                    await _notificationService.NotifyLeadAsync(account, lead, cancellationToken);
                }
            }
        }

        return leads.Select(LeadMapper.MapToDto).ToList();
    }

    public async Task ReclassifyUnverifiedLeadsAsync(CancellationToken cancellationToken = default)
    {
        if (!_classifier.IsAvailable)
        {
            return;
        }

        if (!await AiRetryLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var leads = await _dbContext.JobLeads
                .Include(lead => lead.Source)
                .Where(lead =>
                    lead.Status != LeadStatus.Hidden &&
                    lead.ExpiresAt > now &&
                    (lead.AiStatus == AiLeadStatus.NotChecked ||
                     lead.AiStatus == AiLeadStatus.AiUnavailable ||
                     lead.AiStatus == AiLeadStatus.KeywordOnly ||
                     lead.AiStatus == AiLeadStatus.Error))
                .OrderByDescending(lead => lead.FoundAt)
                .Take(MaxAiRetryLeadsPerRun)
                .ToListAsync(cancellationToken);

            if (leads.Count == 0)
            {
                return;
            }

            var settingsByProfile = await _dbContext.SearchSettings
                .Where(settings => leads.Select(lead => lead.ProfileId).Contains(settings.ProfileId))
                .ToDictionaryAsync(settings => settings.ProfileId, cancellationToken);

            var confirmed = new List<JobLead>();
            foreach (var group in leads
                         .Where(lead => lead.Source != null)
                         .GroupBy(lead => new { lead.ProfileId, lead.SourceId }))
            {
                if (!settingsByProfile.TryGetValue(group.Key.ProfileId, out var settings) || !settings.IsEnabled)
                {
                    continue;
                }

                var source = group.First().Source!;
                foreach (var batch in group.Chunk(AiRetryBatchSize))
                {
                    var classifications = await _classifier.ClassifyBatchAsync(
                        batch.Select(lead => new BatchLeadClassificationInput(
                                lead.ExternalId,
                                $"{lead.Title} {lead.Content}",
                                (lead.MatchedTerms.Count > 0 ? lead.MatchedTerms : lead.MatchedKeywords).ToArray()))
                            .ToArray(),
                        source,
                        settings,
                        cancellationToken);

                    classifications ??= await ClassifyExistingLeadsIndividuallyAsync(batch, source, settings, cancellationToken);
                    if (classifications == null)
                    {
                        continue;
                    }

                    foreach (var lead in batch)
                    {
                        if (!classifications.TryGetValue(lead.ExternalId, out var classification))
                        {
                            continue;
                        }

                        var wasConfirmed = lead.AiStatus == AiLeadStatus.Confirmed;
                        ApplyClassification(lead, classification);
                        if (!wasConfirmed && lead.AiStatus == AiLeadStatus.Confirmed)
                        {
                            confirmed.Add(lead);
                        }
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (confirmed.Count == 0)
            {
                return;
            }

            foreach (var profileGroup in confirmed.GroupBy(lead => lead.ProfileId))
            {
                if (!settingsByProfile.TryGetValue(profileGroup.Key, out var settings) || !settings.NotificationsEnabled)
                {
                    continue;
                }

                var account = await _dbContext.Profiles
                    .Where(profile => profile.Id == profileGroup.Key)
                    .Select(profile => profile.Account)
                    .FirstOrDefaultAsync(cancellationToken);

                if (account == null)
                {
                    continue;
                }

                foreach (var lead in profileGroup)
                {
                    await _notificationService.NotifyLeadAsync(account, lead, cancellationToken);
                }
            }
        }
        finally
        {
            AiRetryLock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, LeadClassificationResult>?> ClassifyPreparedLeadsIndividuallyAsync(
        IReadOnlyCollection<PreparedLeadCandidate> candidates,
        SearchSettings settings,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, LeadClassificationResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            var classification = await _classifier.ClassifyAsync(
                item.RawText,
                item.Candidate.Source,
                settings,
                item.Prefilter.MatchedTerms,
                cancellationToken);

            if (classification != null)
            {
                result[item.Candidate.ExternalId] = classification;
            }
        }

        return result.Count > 0 ? result : null;
    }

    private async Task<IReadOnlyDictionary<string, LeadClassificationResult>?> ClassifyExistingLeadsIndividuallyAsync(
        IReadOnlyCollection<JobLead> leads,
        Source source,
        SearchSettings settings,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, LeadClassificationResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var lead in leads)
        {
            var matchedTerms = (lead.MatchedTerms.Count > 0 ? lead.MatchedTerms : lead.MatchedKeywords).ToArray();
            var classification = await _classifier.ClassifyAsync(
                $"{lead.Title} {lead.Content}",
                source,
                settings,
                matchedTerms,
                cancellationToken);

            if (classification != null)
            {
                result[lead.ExternalId] = classification;
            }
        }

        return result.Count > 0 ? result : null;
    }

    public async Task CleanupExpiredLeadsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _dbContext.JobLeads
            .Where(l => l.ExpiresAt <= now || l.FoundAt <= now.AddHours(-24))
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        _dbContext.JobLeads.RemoveRange(expired);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SearchSettings> GetSettingsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SearchSettings
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        return settings ?? new SearchSettings
        {
            ProfileId = profileId,
            NotificationsEnabled = true,
            IntervalMinutes = 30
        };
    }

    private async Task<Source> GetOrCreateTestSourceAsync(Guid profileId, CancellationToken cancellationToken)
    {
        const string testUrl = "internal://search-test";
        var source = await _dbContext.Sources
            .FirstOrDefaultAsync(s => s.ProfileId == profileId && s.Url == testUrl, cancellationToken);

        if (source != null)
        {
            return source;
        }

        source = new Source
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Type = SourceType.Telegram,
            Name = "Test Search",
            Url = testUrl,
            Credentials = JsonSerializer.Serialize(new { purpose = 0, mode = "test" }),
            Status = SourceStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return source;
    }

    private static string CreateStableExternalId(Guid sourceId, string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceId:N}:{text}"));
        return Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }

    private static string CreateTitle(string content)
    {
        content = content.Trim();
        return content.Length <= 80 ? content : content[..80].Trim() + "...";
    }

    private static AiLeadStatus ResolveAiStatus(LeadClassificationResult? classification, bool aiUnavailable, bool aiError)
    {
        if (aiUnavailable)
        {
            return AiLeadStatus.AiUnavailable;
        }

        if (aiError || classification == null)
        {
            return AiLeadStatus.Error;
        }

        return classification.IsRelevant && classification.Confidence >= 70
            ? AiLeadStatus.Confirmed
            : AiLeadStatus.Rejected;
    }

    private static void ApplyClassification(JobLead lead, LeadClassificationResult classification)
    {
        lead.AiConfidence = classification.Confidence;
        lead.AiSummary = classification.Summary;
        lead.AiCategory = classification.Category;
        lead.AiReason = classification.Reason;
        lead.AiStatus = ResolveAiStatus(classification, false, false);
        lead.Status = lead.AiStatus == AiLeadStatus.Rejected
            ? LeadStatus.Hidden
            : LeadStatus.New;
    }

    private static string BuildLeadSourceName(LeadCandidate candidate)
    {
        var sourceName = string.IsNullOrWhiteSpace(candidate.Source.Name)
            ? "Telegram"
            : candidate.Source.Name.Trim();

        if (candidate.Source.Type != SourceType.Telegram || string.IsNullOrWhiteSpace(candidate.TopicName))
        {
            return sourceName;
        }

        return $"{sourceName} › {candidate.TopicName.Trim()}";
    }

    private sealed record PreparedLeadCandidate(
        LeadCandidate Candidate,
        string RawText,
        PrefilterResult Prefilter);
}
