using ClientScout.Application.Search;
using ClientScout.Domain.Entities;

var users = ReadIntArg(args, "--users", 1000);
var candidatesPerUser = ReadIntArg(args, "--candidates", 15);
var batchSize = ReadIntArg(args, "--batch", 10);

var filter = new SearchCandidateFilter();
var totalCandidates = 0;
var prefilterPassed = 0;
var aiBatches = 0;
var confirmedEstimate = 0;
var providerOffersRejected = 0;

var started = DateTimeOffset.UtcNow;
for (var userIndex = 0; userIndex < users; userIndex++)
{
    var settings = CreateSettings(userIndex);
    var profileCandidates = new List<string>();
    for (var candidateIndex = 0; candidateIndex < candidatesPerUser; candidateIndex++)
    {
        totalCandidates++;
        var text = CreateCandidateText(userIndex, candidateIndex);
        var prefilter = filter.Evaluate(text, settings);
        if (!prefilter.IsCandidate)
        {
            if (string.Equals(prefilter.RejectionReason, "PROVIDER_OFFER_OR_RESUME", StringComparison.OrdinalIgnoreCase))
            {
                providerOffersRejected++;
            }

            continue;
        }

        prefilterPassed++;
        profileCandidates.Add(text);
        if (prefilter.Score >= 45)
        {
            confirmedEstimate++;
        }
    }

    aiBatches += (int)Math.Ceiling(profileCandidates.Count / (double)batchSize);
}

var elapsed = DateTimeOffset.UtcNow - started;
var estimatedAiRequestsAtBatchTwo = (int)Math.Ceiling(prefilterPassed / 2.0);
var estimatedAiRequestsAtBatchTen = aiBatches;

Console.WriteLine("ClientScout AI load dry-run");
Console.WriteLine($"Users: {users}");
Console.WriteLine($"Candidates per user: {candidatesPerUser}");
Console.WriteLine($"Total candidates: {totalCandidates}");
Console.WriteLine($"Prefilter passed: {prefilterPassed}");
Console.WriteLine($"Provider offers/resumes rejected: {providerOffersRejected}");
Console.WriteLine($"Estimated confirmed by local score before AI: {confirmedEstimate}");
Console.WriteLine($"Estimated AI requests with old batch=2: {estimatedAiRequestsAtBatchTwo}");
Console.WriteLine($"Estimated AI requests with dynamic batch={batchSize}: {estimatedAiRequestsAtBatchTen}");
Console.WriteLine($"AI request reduction: {PercentReduction(estimatedAiRequestsAtBatchTwo, estimatedAiRequestsAtBatchTen):0.0}%");
Console.WriteLine($"Dry-run elapsed: {elapsed.TotalMilliseconds:0} ms");

static SearchSettings CreateSettings(int userIndex)
{
    var stacks = new[]
    {
        "react frontend",
        "telegram bot",
        "crm system",
        "landing page",
        "figma design",
        "python parser"
    };
    var keyword = stacks[userIndex % stacks.Length];

    return new SearchSettings
    {
        IsEnabled = true,
        UserKeywords = [keyword],
        NegativeKeywords = ["resume", "tutorial", "course"],
        SearchProfileSummary = $"Hidden profile for paid freelance orders about {keyword}. Accept implementation, fixes, integrations, redesign, automation, and deployment. Reject tutorials, resumes, news, and discussions.",
        MustIncludeSignals = [$"need {keyword}", $"build {keyword}", $"fix {keyword}", $"разработать {keyword}", $"нужен {keyword}"],
        SoftSignals = ["budget", "deadline", "freelancer", "заказ", "исполнитель", "оплата"],
        RejectSignals = ["resume", "tutorial", "course", "portfolio review"],
        ExpandedPositiveTerms = [$"create {keyword}", $"develop {keyword}", $"implement {keyword}", $"доработать {keyword}", $"создать {keyword}"],
        ExpandedIntentTerms = ["need to build", "looking for developer", "нужен разработчик", "требуется специалист"],
        StrongTerms = [keyword]
    };
}

static string CreateCandidateText(int userIndex, int candidateIndex)
{
    var good = candidateIndex % 3 != 0;
    var stacks = new[]
    {
        "react frontend",
        "telegram bot",
        "crm system",
        "landing page",
        "figma design",
        "python parser"
    };
    var stack = stacks[userIndex % stacks.Length];

    if (candidateIndex % 5 == 0)
    {
        return $"Добрый день. Коротко обо мне: я frontend разработчик, опыт работы 4 года, React, Vue, HTML/CSS. Портфолио отправлю в личку.";
    }

    if (good)
    {
        return $"Need freelancer to build {stack}. Budget discussed, deadline this week, paid order, integration and fixes required.";
    }

    return $"Tutorial discussion and resume portfolio about {stack}, no paid order, no client task.";
}

static int ReadIntArg(string[] args, string name, int fallback)
{
    var index = Array.IndexOf(args, name);
    if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var value))
    {
        return fallback;
    }

    return Math.Max(1, value);
}

static double PercentReduction(int oldValue, int newValue)
{
    return oldValue <= 0 ? 0 : (1 - newValue / (double)oldValue) * 100;
}
