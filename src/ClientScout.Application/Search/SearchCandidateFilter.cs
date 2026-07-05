using System.Text;
using System.Text.RegularExpressions;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public partial class SearchCandidateFilter : ISearchCandidateFilter
{
    public PrefilterResult Evaluate(string rawText, SearchSettings settings)
    {
        var normalizedText = NormalizeText(rawText);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new PrefilterResult(false, Array.Empty<string>(), Array.Empty<string>(), "EMPTY_TEXT", 0);
        }

        var negativeTerms = Merge(settings.NegativeKeywords);
        var negativeMatch = negativeTerms.FirstOrDefault(term => ContainsTerm(normalizedText, term));
        if (negativeMatch != null)
        {
            return new PrefilterResult(false, Array.Empty<string>(), Array.Empty<string>(), $"NEGATIVE:{negativeMatch}", 0);
        }

        var regularTerms = Merge(
            settings.UserKeywords,
            settings.MustIncludeSignals,
            settings.SoftSignals,
            settings.ExpandedPositiveTerms,
            settings.ExpandedIntentTerms);
        var profileContextTerms = Merge(
            settings.UserKeywords,
            settings.MustIncludeSignals,
            settings.SoftSignals,
            settings.ExpandedPositiveTerms);
        var strongTerms = Merge(settings.StrongTerms);
        var rejectSignalTerms = Merge(settings.RejectSignals);

        var regularMatches = regularTerms
            .Where(term => ContainsTerm(normalizedText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var userKeywordMatches = Merge(settings.UserKeywords)
            .Where(term => ContainsTerm(normalizedText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profileContextMatches = profileContextTerms
            .Where(term => ContainsTerm(normalizedText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var strongMatches = strongTerms
            .Where(term => ContainsTerm(normalizedText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rejectSignalMatches = rejectSignalTerms
            .Where(term => ContainsTerm(normalizedText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasBuyerIntent = HasBuyerIntent(normalizedText);
        var hasConcreteDeliveryRequest = HasConcreteDeliveryRequest(normalizedText);
        var hasProviderOffer = HasProviderOfferSignal(normalizedText);
        if (hasProviderOffer && !hasBuyerIntent)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "PROVIDER_OFFER_OR_RESUME", 0);
        }

        var hasHiringOrResumeNoise = HasHiringOrResumeNoise(normalizedText);
        if (hasHiringOrResumeNoise && !hasConcreteDeliveryRequest)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "HIRING_OR_RESUME_NOISE", 0);
        }

        if (HasInformationalContentSignal(normalizedText) && !hasConcreteDeliveryRequest)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "INFORMATIONAL_CONTENT", 0);
        }

        if (HasAdjacentVideoEditingSignal(normalizedText) && !ProfileAllowsVideoEditing(settings))
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "ADJACENT_VIDEO_EDITING", 0);
        }

        var hasWorkRequest = hasBuyerIntent || hasConcreteDeliveryRequest;
        if (!hasWorkRequest)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "NO_BUYER_REQUEST", 0);
        }

        if (profileContextMatches.Length == 0 && strongMatches.Length == 0)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, "INTENT_ONLY_NO_PROFILE_MATCH", 0);
        }

        var hasMultiKeywordProfile = Merge(settings.UserKeywords).Length >= 3;
        if (hasMultiKeywordProfile &&
            userKeywordMatches.Length == 1 &&
            IsTechnicalStackOnlyTerm(userKeywordMatches[0]))
        {
            return new PrefilterResult(false, regularMatches, strongMatches, $"ISOLATED_STACK_KEYWORD:{userKeywordMatches[0]}", 0);
        }

        if (hasMultiKeywordProfile &&
            userKeywordMatches.Length == 1 &&
            regularMatches.Length <= 2 &&
            strongMatches.Length == 0)
        {
            return new PrefilterResult(false, regularMatches, strongMatches, $"ISOLATED_KEYWORD:{userKeywordMatches[0]}", 0);
        }

        var isCandidate = regularMatches.Length >= 1 ||
                          strongMatches.Length >= 1;

        if (isCandidate &&
            rejectSignalMatches.Length > 0 &&
            strongMatches.Length == 0 &&
            regularMatches.Length < 2)
        {
            return new PrefilterResult(
                false,
                regularMatches,
                strongMatches,
                $"REJECT_SIGNAL:{rejectSignalMatches[0]}",
                0);
        }

        var score = regularMatches.Length * 10 +
                    strongMatches.Length * 25 +
                    (hasBuyerIntent ? 5 : 0) +
                    (hasConcreteDeliveryRequest ? 5 : 0) -
                    rejectSignalMatches.Length * 15;

        return new PrefilterResult(
            isCandidate,
            regularMatches.Concat(strongMatches).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            strongMatches,
            isCandidate ? null : "NOT_ENOUGH_MATCHES",
            score);
    }

    public static string NormalizeText(string value)
    {
        value = value.ToLowerInvariant().Trim();
        value = PunctuationRegex().Replace(value, " ");
        value = SpacesRegex().Replace(value, " ");
        return value.Trim();
    }

    private static bool ContainsTerm(string normalizedText, string term)
    {
        term = NormalizeText(term);
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])";
        if (Regex.IsMatch(normalizedText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        if (!IsSingleCyrillicTerm(term))
        {
            return false;
        }

        var stem = GetRussianStem(term);
        if (stem.Length < 3)
        {
            return false;
        }

        var stemPattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(stem)}[\p{{L}}\p{{N}}]*(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(normalizedText, stemPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasBuyerIntent(string normalizedText)
    {
        var intentTerms = new[]
        {
            "\u043d\u0443\u0436\u043d\u043e",
            "\u043d\u0443\u0436\u0435\u043d",
            "\u043d\u0443\u0436\u043d\u0430",
            "\u043d\u0443\u0436\u043d\u044b",
            "\u0442\u0440\u0435\u0431\u0443\u0435\u0442\u0441\u044f",
            "\u043d\u0435\u043e\u0431\u0445\u043e\u0434\u0438\u043c\u043e",
            "\u0438\u0449\u0443",
            "\u0438\u0449\u0435\u043c",
            "\u043d\u0443\u0436\u043d\u0430 \u043f\u043e\u043c\u043e\u0449\u044c",
            "\u0442\u0440\u0435\u0431\u0443\u0435\u0442\u0441\u044f \u0441\u043f\u0435\u0446\u0438\u0430\u043b\u0438\u0441\u0442",
            "\u0438\u0449\u0435\u043c \u0441\u043f\u0435\u0446\u0438\u0430\u043b\u0438\u0441\u0442\u0430",
            "\u0441\u043e\u0437\u0434\u0430\u0442\u044c",
            "\u0441\u0434\u0435\u043b\u0430\u0442\u044c",
            "\u0440\u0430\u0437\u0440\u0430\u0431\u043e\u0442\u0430\u0442\u044c",
            "\u0434\u043e\u0440\u0430\u0431\u043e\u0442\u0430\u0442\u044c",
            "\u0434\u043e\u0434\u0435\u043b\u0430\u0442\u044c",
            "\u0434\u043e\u043f\u0438\u043b\u0438\u0442\u044c",
            "\u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c",
            "\u0432\u0438\u0434\u043e\u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c",
            "\u043f\u0435\u0440\u0435\u0434\u0435\u043b\u0430\u0442\u044c",
            "\u043f\u0435\u0440\u0435\u0440\u0430\u0431\u043e\u0442\u0430\u0442\u044c",
            "\u043f\u043e\u043f\u0440\u0430\u0432\u0438\u0442\u044c",
            "\u0438\u0441\u043f\u0440\u0430\u0432\u0438\u0442\u044c",
            "\u043f\u043e\u0447\u0438\u043d\u0438\u0442\u044c",
            "\u0443\u0441\u0442\u0440\u0430\u043d\u0438\u0442\u044c",
            "\u043f\u043e\u0444\u0438\u043a\u0441\u0438\u0442\u044c",
            "\u0440\u0435\u0434\u0438\u0437\u0430\u0439\u043d",
            "\u0441\u0434\u0435\u043b\u0430\u0442\u044c \u0440\u0435\u0434\u0438\u0437\u0430\u0439\u043d",
            "\u0444\u0438\u043a\u0441",
            "\u0431\u0430\u0433",
            "\u043e\u0448\u0438\u0431\u043a\u0430",
            "\u0434\u043e\u0440\u0430\u0431\u043e\u0442\u043a\u0430",
            "\u043f\u0435\u0440\u0435\u0434\u0435\u043b\u043a\u0430",
            "\u0443\u043b\u0443\u0447\u0448\u0438\u0442\u044c",
            "\u043e\u0431\u043d\u043e\u0432\u0438\u0442\u044c",
            "\u043c\u043e\u0434\u0435\u0440\u043d\u0438\u0437\u0438\u0440\u043e\u0432\u0430\u0442\u044c",
            "\u043f\u0435\u0440\u0435\u043d\u0435\u0441\u0442\u0438",
            "\u043f\u0435\u0440\u0435\u043f\u0438\u0441\u0430\u0442\u044c",
            "\u043e\u043f\u0442\u0438\u043c\u0438\u0437\u0438\u0440\u043e\u0432\u0430\u0442\u044c",
            "\u0441\u043e\u0431\u0440\u0430\u0442\u044c",
            "\u043d\u0430\u043f\u0438\u0441\u0430\u0442\u044c",
            "\u0440\u0435\u0430\u043b\u0438\u0437\u043e\u0432\u0430\u0442\u044c",
            "\u0441\u0432\u0435\u0440\u0441\u0442\u0430\u0442\u044c",
            "\u0440\u0430\u0437\u0432\u0435\u0440\u043d\u0443\u0442\u044c",
            "\u0438\u043d\u0442\u0435\u0433\u0440\u0438\u0440\u043e\u0432\u0430\u0442\u044c",
            "\u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u0442\u044c",
            "\u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u0435",
            "\u043d\u0430\u0441\u0442\u0440\u043e\u0438\u0442\u044c",
            "\u043d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0430",
            "need",
            "looking for",
            "looking to hire",
            "hire",
            "create",
            "develop",
            "implement",
            "finish",
            "complete",
            "change",
            "modify",
            "redesign",
            "revamp",
            "fix",
            "repair",
            "debug",
            "bugfix",
            "fix bug",
            "build",
            "integrate",
            "configure",
            "set up",
            "optimize",
            "update",
            "refactor",
            "rewrite",
            "deploy"
        };

        return intentTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool HasProviderOfferSignal(string normalizedText)
    {
        var providerOfferTerms = new[]
        {
            "я разработчик",
            "я фронтенд",
            "я frontend",
            "я backend",
            "я fullstack",
            "я full stack",
            "я дизайнер",
            "я верстальщик",
            "я программист",
            "я специалист",
            "мы разработчики",
            "мы команда",
            "наша команда",
            "предлагаю услуги",
            "оказываю услуги",
            "предоставляю услуги",
            "занимаюсь разработкой",
            "занимаюсь созданием",
            "разрабатываю сайты",
            "создаю сайты",
            "делаю сайты",
            "помогу с разработкой",
            "готов взяться",
            "готов выполнить",
            "ищу работу",
            "ищу проект",
            "ищу заказ",
            "ищу подработку",
            "ищу вакансию",
            "открыт к предложениям",
            "возьму заказ",
            "беру заказы",
            "портфолио",
            "мое портфолио",
            "моё портфолио",
            "коротко обо мне",
            "обо мне",
            "мой опыт",
            "опыт работы",
            "стаж работы",
            "резюме",
            "cv",
            "resume",
            "i am a developer",
            "i'm a developer",
            "i am frontend",
            "i'm frontend",
            "i am backend",
            "i'm backend",
            "i offer",
            "offering services",
            "my portfolio",
            "looking for work",
            "looking for a job",
            "open to work",
            "available for projects",
            "available for hire"
        };

        return providerOfferTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool HasHiringOrResumeNoise(string normalizedText)
    {
        var hiringNoiseTerms = new[]
        {
            "\u0432\u0430\u043a\u0430\u043d\u0441\u0438\u044f",
            "\u0432\u0430\u043a\u0430\u043d\u0441\u0438\u0438",
            "\u043f\u043e\u0437\u0438\u0446\u0438\u044f",
            "\u0440\u0435\u0437\u044e\u043c\u0435",
            "\u043e\u0442\u043a\u043b\u0438\u043a",
            "\u043e\u0442\u043a\u043b\u0438\u043a\u043d\u0443\u0442\u044c\u0441\u044f",
            "\u0437\u0430\u0440\u043f\u043b\u0430\u0442\u0430",
            "\u0437\u043f",
            "\u043e\u043a\u043b\u0430\u0434",
            "\u0444\u0443\u043b\u0442\u0430\u0439\u043c",
            "\u0444\u0443\u043b\u043b\u0442\u0430\u0439\u043c",
            "fulltime",
            "full time",
            "part time",
            "job opening",
            "vacancy",
            "position",
            "resume",
            "cv"
        };

        return hiringNoiseTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool HasConcreteDeliveryRequest(string normalizedText)
    {
        var deliveryTerms = new[]
        {
            "\u0440\u0430\u0437\u0440\u0430\u0431\u043e\u0442\u0430\u0442\u044c",
            "\u0441\u0434\u0435\u043b\u0430\u0442\u044c",
            "\u0441\u043e\u0437\u0434\u0430\u0442\u044c",
            "\u0434\u043e\u0440\u0430\u0431\u043e\u0442\u0430\u0442\u044c",
            "\u0434\u043e\u0434\u0435\u043b\u0430\u0442\u044c",
            "\u0438\u0441\u043f\u0440\u0430\u0432\u0438\u0442\u044c",
            "\u043f\u043e\u0444\u0438\u043a\u0441\u0438\u0442\u044c",
            "\u0443\u0441\u0442\u0440\u0430\u043d\u0438\u0442\u044c",
            "\u0440\u0435\u0430\u043b\u0438\u0437\u043e\u0432\u0430\u0442\u044c",
            "\u043d\u0430\u043f\u0438\u0441\u0430\u0442\u044c",
            "\u0441\u043e\u0431\u0440\u0430\u0442\u044c",
            "develop",
            "build",
            "create",
            "implement",
            "fix",
            "debug",
            "complete"
        };

        return deliveryTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool HasInformationalContentSignal(string normalizedText)
    {
        var informationalTerms = new[]
        {
            "\u0432\u044b\u0448\u0435\u043b \u043e\u0442\u0447\u0435\u0442",
            "\u0432\u044b\u0448\u0435\u043b \u043e\u0442\u0447\u0451\u0442",
            "\u0432\u044b\u043f\u0443\u0441\u0442\u0438\u043b \u043e\u0442\u0447\u0435\u0442",
            "\u0432\u044b\u043f\u0443\u0441\u0442\u0438\u043b \u043e\u0442\u0447\u0451\u0442",
            "\u0432\u044b\u043f\u0443\u0441\u0442\u0438\u043b\u0430 \u043e\u0442\u0447\u0435\u0442",
            "\u0432\u044b\u043f\u0443\u0441\u0442\u0438\u043b\u0430 \u043e\u0442\u0447\u0451\u0442",
            "\u043e\u0442\u0447\u0435\u0442 \u043f\u043e",
            "\u043e\u0442\u0447\u0451\u0442 \u043f\u043e",
            "\u0430\u043d\u0430\u043b\u0438\u0442\u0438\u0447\u0435\u0441\u043a\u0430\u044f \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430",
            "\u0430\u043d\u0430\u043b\u0438\u0442\u0438\u0447\u0435\u0441\u043a\u0438\u0439 \u043e\u0442\u0447\u0435\u0442",
            "\u0430\u043d\u0430\u043b\u0438\u0442\u0438\u0447\u0435\u0441\u043a\u0438\u0439 \u043e\u0442\u0447\u0451\u0442",
            "\u0438\u0441\u0441\u043b\u0435\u0434\u043e\u0432\u0430\u043d\u0438\u0435",
            "\u0440\u044b\u043d\u043e\u043a",
            "\u0447\u0430\u0440\u0442",
            "\u0447\u0430\u0440\u0442\u044b",
            "\u0442\u043e\u043f \u043f\u043e",
            "\u0442\u043e\u043f 10",
            "\u0442\u043e\u043f 15",
            "\u0442\u043e\u043f\u043e\u0432\u044b\u0435",
            "\u0441\u0430\u043c\u044b\u0435 \u0437\u0430\u0440\u0430\u0431\u0430\u0442\u044b\u0432\u0430\u044e\u0449\u0438\u0435",
            "\u0441\u0430\u043c\u044b\u0435 \u0437\u0430\u0433\u0440\u0443\u0436\u0430\u0435\u043c\u044b\u0435",
            "\u043f\u0440\u0435\u0434\u043e\u0441\u0442\u0430\u0432\u043b\u044f\u0435\u0442 \u0434\u0430\u043d\u043d\u044b\u0435",
            "\u0434\u0430\u043d\u043d\u044b\u0435 \u043f\u043e \u0432\u044b\u0440\u0443\u0447\u043a\u0435",
            "\u043f\u0440\u043e\u0434\u0430\u0436\u0438",
            "\u0432\u044b\u0440\u0443\u0447\u043a\u0430",
            "\u0431\u0435\u043d\u0447\u043c\u0430\u0440\u043a",
            "\u0431\u0435\u043d\u0447\u043c\u0430\u0440\u043a\u0438",
            "\u043a\u0435\u0439\u0441 \u0441\u0442\u0430\u0434\u0438",
            "\u043d\u043e\u0432\u043e\u0441\u0442\u044c",
            "\u043d\u043e\u0432\u043e\u0441\u0442\u0438",
            "\u0441\u0442\u0430\u0442\u044c\u044f",
            "\u0440\u0430\u0437\u0431\u043e\u0440 \u043e\u0442\u0447\u0435\u0442\u0430",
            "\u0440\u0430\u0437\u0431\u043e\u0440 \u043e\u0442\u0447\u0451\u0442\u0430",
            "\u043f\u043e\u0447\u0438\u0442\u0430\u0439\u0442\u0435",
            "report",
            "market report",
            "analytics platform",
            "research",
            "study",
            "benchmark",
            "benchmarks",
            "ranking",
            "rankings",
            "top grossing",
            "top downloads",
            "top downloaded",
            "chart",
            "charts",
            "case study",
            "news",
            "article",
            "press release",
            "read the report"
        };

        return informationalTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool HasAdjacentVideoEditingSignal(string normalizedText)
    {
        var videoEditingTerms = new[]
        {
            "\u043c\u043e\u043d\u0442\u0430\u0436",
            "\u0432\u0438\u0434\u0435\u043e\u043c\u043e\u043d\u0442\u0430\u0436",
            "\u0441\u043c\u043e\u043d\u0442\u0438\u0440\u043e\u0432\u0430\u0442\u044c",
            "\u0441\u043c\u043e\u043d\u0442\u0438\u0440\u0443\u0439\u0442\u0435",
            "\u0440\u043e\u043b\u0438\u043a",
            "\u0440\u043e\u043b\u0438\u043a\u0438",
            "\u0448\u043e\u0440\u0442\u0441",
            "\u0448\u043e\u0440\u0442",
            "\u0444\u0443\u0442\u0430\u0436",
            "\u0444\u0443\u0442\u0430\u0436\u0438",
            "\u0432\u0438\u0434\u0435\u043e",
            "\u044e\u0442\u0443\u0431",
            "youtube",
            "video editing",
            "edit video",
            "video editor",
            "shorts",
            "reels",
            "footage"
        };

        return videoEditingTerms.Any(term => ContainsTerm(normalizedText, term));
    }

    private static bool ProfileAllowsVideoEditing(SearchSettings settings)
    {
        var profileTerms = Merge(
            settings.UserKeywords,
            settings.MustIncludeSignals,
            settings.SoftSignals,
            settings.ExpandedPositiveTerms,
            settings.ExpandedIntentTerms,
            settings.StrongTerms);

        var allowedTerms = new[]
        {
            "\u043c\u043e\u043d\u0442\u0430\u0436",
            "\u0432\u0438\u0434\u0435\u043e\u043c\u043e\u043d\u0442\u0430\u0436",
            "\u0440\u043e\u043b\u0438\u043a",
            "\u0440\u043e\u043b\u0438\u043a\u0438",
            "\u0448\u043e\u0440\u0442\u0441",
            "\u044e\u0442\u0443\u0431",
            "youtube",
            "video editing",
            "video editor",
            "shorts",
            "reels"
        };

        return profileTerms.Any(profileTerm => allowedTerms.Any(allowed => ContainsTerm(NormalizeText(profileTerm), allowed)));
    }

    private static bool IsTechnicalStackOnlyTerm(string term)
    {
        var normalized = NormalizeText(term);
        var stackOnlyTerms = new[]
        {
            "c#",
            "csharp",
            "c sharp",
            "c++",
            "cpp",
            "java",
            "kotlin",
            "swift",
            "python",
            "php",
            "go",
            "golang",
            "rust",
            "javascript",
            "js",
            "typescript",
            "ts",
            "react",
            "vue",
            "angular",
            "svelte",
            "node",
            "node js",
            "node.js",
            "next",
            "next js",
            "next.js",
            "nuxt",
            "nuxt js",
            "nuxt.js",
            ".net",
            "dotnet",
            "asp.net",
            "laravel",
            "django",
            "flask",
            "fastapi",
            "figma",
            "photoshop"
        };

        return stackOnlyTerms.Any(stackTerm => normalized == NormalizeText(stackTerm));
    }

    private static string[] Merge(params IEnumerable<string>[] groups)
    {
        return groups
            .SelectMany(group => group ?? Array.Empty<string>())
            .Select(term => term.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .SelectMany(ExpandTerm)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExpandTerm(string term)
    {
        yield return term;

        foreach (var token in TermSplitRegex().Split(term))
        {
            var normalized = token.Trim();
            if (normalized.Length >= 3)
            {
                yield return normalized;
            }
        }
    }

    private static bool IsSingleCyrillicTerm(string term)
    {
        return term.All(ch => ch >= '\u0400' && ch <= '\u04FF');
    }

    private static string GetRussianStem(string term)
    {
        string[] endings =
        [
            "\u0430\u043c\u0438", "\u044f\u043c\u0438", "\u043e\u0433\u043e", "\u0435\u0433\u043e", "\u0435\u043c\u0443",
            "\u044b\u043c\u0438", "\u0438\u043c\u0438", "\u0430\u044f", "\u044f\u044f", "\u043e\u0435", "\u0435\u0435",
            "\u044b\u0435", "\u0438\u0435", "\u044b\u0439", "\u0438\u0439", "\u043e\u0439", "\u0443\u044e", "\u044e\u044e",
            "\u043e\u043c", "\u0435\u043c", "\u0430\u0445", "\u044f\u0445", "\u0430\u043c", "\u044f\u043c",
            "\u043e\u0432", "\u0435\u0432", "\u0430", "\u044f", "\u044b", "\u0438", "\u0443", "\u044e", "\u0435", "\u043e"
        ];

        foreach (var ending in endings.OrderByDescending(value => value.Length))
        {
            if (term.Length - ending.Length >= 3 && term.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return term[..^ending.Length];
            }
        }

        return term;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}#+]+", RegexOptions.Compiled)]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex SpacesRegex();

    [GeneratedRegex(@"[/\\|,;:()\[\]{}<>+]+", RegexOptions.Compiled)]
    private static partial Regex TermSplitRegex();
}
