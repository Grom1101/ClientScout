using System.Text.Json;
using ClientScout.Application.Search.Models;

namespace ClientScout.Application.Search;

public class AiSearchExpansionService : IAiSearchExpansionService
{
    private readonly AiJsonClient _ai;

    public AiSearchExpansionService(AiJsonClient ai)
    {
        _ai = ai;
    }

    public bool IsAvailable => _ai.IsAvailable;

    public async Task<SearchExpansionResult?> ExpandAsync(SearchExpansionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var prompt = $$"""
Ты экспертная AI-система для создания очень сильного скрытого поискового профиля в приложении мониторинга фриланс-заказов.
Пользователь дает короткие ключевые слова. Твоя задача — понять реальное намерение и создать большой внутренний профиль для поиска заказов.

ВАЖНО ПО ЯЗЫКУ:
- Основной язык результата: русский.
- searchProfileSummary должен быть на русском.
- В каждом массиве сначала должны идти русские фразы, потом английские аналоги.
- Не отдавай профиль только на английском.
- Kwork и Telegram часто содержат русские сообщения, поэтому русские формулировки обязательны.

Return STRICT JSON only with this exact shape:
{
  "searchProfileSummary": "первая строка: РУССКОЕ НАЗВАНИЕ ТИПА ПРОФИЛЯ ЗАГЛАВНЫМИ БУКВАМИ, например ПРОФИЛЬ РАЗРАБОТКИ ИГР. Затем большой скрытый документ профиля на русском, 2500-5000 символов",
  "mustIncludeSignals": ["очень конкретный русский сигнал", "English equivalent"],
  "softSignals": ["русское контекстное слово", "English equivalent"],
  "rejectSignals": ["русский сигнал полностью другого типа работы", "English equivalent"],
  "expandedPositiveTerms": ["русская конкретная фраза услуги/задачи", "English equivalent"],
  "expandedIntentTerms": ["русская фраза запроса клиента", "English equivalent"],
  "strongTerms": ["русский или технический маркер", "English equivalent"],
  "normalizedNegativeTerms": ["термин"]
}

Правила:
- Сначала определи тип профиля по всем UserKeywords вместе. Не создавай захардкоженные профили под одну нишу. Тип профиля должен быть динамическим: разработка игр, frontend, backend, дизайн, монтаж, маркетинг, парсинг, боты, аналитика, тексты или любая другая ниша.
- Первой строкой searchProfileSummary напиши тип профиля заглавными буквами на русском: "ПРОФИЛЬ ...". Это главный контекст для дальнейшей проверки заказов.
- В searchProfileSummary добавь разделы: "Тип профиля", "Идеальный заказ", "Обязательный контекст", "Стек/инструменты как ограничения", "Что похоже по словам, но не подходит", "Правило спорных случаев".
- Если ключевые слова содержат и домен/намерение, и стек/инструменты, явно объясни, какие слова задают домен, а какие являются только ограничениями. Один стековый или инструментальный термин не должен подтверждать релевантность без совпадения с доменом профиля. Например: C# без Unity/игрового контекста не подходит для профиля разработки игр; React без сайта/frontend-задачи не подходит для frontend-профиля; Photoshop без дизайн-задачи не подходит для дизайн-профиля.
- Профиль может описывать любую нишу. Если ключевые слова неоднозначны, приоритет: IT, разработка, дизайн, маркетинг, автоматизация, аналитика, digital-услуги.
- searchProfileSummary — главный скрытый документ профиля. Он должен быть подробным, практичным и на русском.
- В searchProfileSummary включи: какой заказ идеален, какие смежные работы допустимы, какие смежные работы недопустимы, признаки намерения заказчика, признаки качества заказа, примеры релевантных заказов, примеры нерелевантных сообщений, как решать спорные случаи.
- Отдельно подчеркни: релевантны только сообщения, где клиент ищет исполнителя/просит выполнить работу/хочет заказать услугу. Нерелевантны резюме, портфолио, самореклама, "я разработчик", "предлагаю услуги", обсуждения, новости, обучение.
- mustIncludeSignals: 40-80 конкретных фраз, которые доказывают, что сообщение про нужную работу. Русские фразы обязательны.
- softSignals: 80-160 связанных контекстных терминов и фраз. Русские фразы обязательны.
- rejectSignals: только полностью другие типы задач или самореклама/резюме/портфолио. Не отклоняй случайно связанные технологии.
- expandedPositiveTerms: 100-200 конкретных многословных фраз услуг и задач. Не используй одиночные общие слова вроде "разработка", "создание", "проект".
- expandedIntentTerms: 80-160 конкретных фраз запроса клиента: "нужно разработать сайт", "ищем frontend-разработчика", "требуется доработать React-приложение".
- strongTerms: 60-100 доменных маркеров, фреймворков, инструментов, форматов, жаргона.
- Расширяй двуязычно: русский -> английский и английский -> русский. Но русский должен быть первым и преобладать.
- ВАЖНО: Для КАЖДОГО слова из массива UserKeywords обязательно сгенерируй его прямой перевод и русскую транслитерацию (если это английское слово). Эти прямые переводы (например, API -> АПИ, database -> база данных, C# -> си шарп) ДОЛЖНЫ БЫТЬ помещены в массив strongTerms.
- Не придумывай финальные negative keywords. Только нормализуй пользовательские negative keywords и очевидные переводы.
- Если предыдущий словарь есть, можно использовать его как контекст, но если он на английском, перепиши его на русский и добавь русские формулировки.
- Max 80 mustIncludeSignals, 160 softSignals, 100 rejectSignals, 200 expandedPositiveTerms, 160 expandedIntentTerms, 100 strongTerms, 50 normalizedNegativeTerms.
- Удаляй дубли без учета регистра.
- Сделай словарь достаточно сильным, чтобы локальный фильтр мог убрать большую часть мусора до AI-классификации.

Input JSON:
{{JsonSerializer.Serialize(request)}}
""";

        var result = await _ai.GenerateJsonAsync<SearchExpansionResult>(prompt, AiTaskKind.ProfileExpansion, cancellationToken);
        if (result == null)
        {
            return null;
        }

        return new SearchExpansionResult(
            Trim(result.SearchProfileSummary, 5000),
            Normalize(result.MustIncludeSignals, 80),
            Normalize(result.SoftSignals, 160),
            Normalize(result.RejectSignals, 100),
            Normalize(result.ExpandedPositiveTerms, 200),
            Normalize(result.ExpandedIntentTerms, 160),
            Normalize(result.StrongTerms, 100),
            Normalize(result.NormalizedNegativeTerms, 50));
    }

    private static string Trim(string? value, int max)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    private static string[] Normalize(IEnumerable<string>? terms, int max)
    {
        return (terms ?? Array.Empty<string>())
            .Select(term => term.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();
    }
}
