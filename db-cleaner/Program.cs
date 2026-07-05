using Npgsql;
using System;
using System.Net.Http.Json;
using System.Text.Json;

string connString = "Host=localhost;Database=clientscout;Username=postgres;Password=postgres";

try {
    using var conn = new NpgsqlConnection(connString);
    conn.Open();

    if (args.Contains("clear-leads", StringComparer.OrdinalIgnoreCase)) {
        using var stopSearch = new NpgsqlCommand("UPDATE \"SearchSettings\" SET \"IsEnabled\" = false, \"UpdatedAt\" = now()", conn);
        var stopped = stopSearch.ExecuteNonQuery();

        using var deleteLeads = new NpgsqlCommand("DELETE FROM \"JobLeads\"", conn);
        var deleted = deleteLeads.ExecuteNonQuery();

        using var resetTelegramMarkers = new NpgsqlCommand("""
            UPDATE "Sources"
            SET "Credentials" = regexp_replace(
                    COALESCE("Credentials", '{}'),
                    ',"lastMessageId":[0-9]+|"lastMessageId":[0-9]+,?',
                    '',
                    'g'
                ),
                "LastScraped" = NULL,
                "LastError" = NULL,
                "Status" = 1
            WHERE "Type" = 0
            """, conn);
        var resetMarkers = resetTelegramMarkers.ExecuteNonQuery();

        Console.WriteLine($"Search settings stopped: {stopped}");
        Console.WriteLine($"Deleted JobLeads: {deleted}");
        Console.WriteLine($"Reset Telegram scan markers: {resetMarkers}");
        return;
    }

    if (args.Contains("wipe-user-data", StringComparer.OrdinalIgnoreCase)) {
        using var wipe = new NpgsqlCommand("""
            TRUNCATE TABLE
                "OutreachLogs",
                "OutreachCampaigns",
                "MessageTemplates",
                "JobLeads",
                "ExchangeConnections",
                "SearchSettings",
                "Sources",
                "UserbotSessions",
                "Profiles",
                "Users",
                "Accounts"
            RESTART IDENTITY CASCADE
            """, conn);
        wipe.ExecuteNonQuery();
        Console.WriteLine("Wiped all user data tables.");
        return;
    }

    if (args.Contains("clear-telegram-transient-errors", StringComparer.OrdinalIgnoreCase)) {
        using var clearTransient = new NpgsqlCommand("""
            UPDATE "Sources"
            SET "Status" = 1,
                "LastError" = NULL
            WHERE "Type" = 0
              AND "Status" = 2
              AND "LastError" ILIKE '%Object reference%'
            """, conn);
        var cleared = clearTransient.ExecuteNonQuery();
        Console.WriteLine($"Cleared Telegram transient source errors: {cleared}");
        return;
    }

    if (args.Contains("reset-telegram-markers", StringComparer.OrdinalIgnoreCase)) {
        using var resetTelegramMarkers = new NpgsqlCommand("""
            UPDATE "Sources"
            SET "Credentials" = regexp_replace(
                    COALESCE("Credentials", '{}'),
                    ',"lastMessageId":[0-9]+|"lastMessageId":[0-9]+,?',
                    '',
                    'g'
                ),
                "LastScraped" = NULL,
                "LastError" = NULL,
                "Status" = 1
            WHERE "Type" = 0
            """, conn);
        var resetMarkers = resetTelegramMarkers.ExecuteNonQuery();
        Console.WriteLine($"Reset Telegram scan markers: {resetMarkers}");
        return;
    }

    if (args.Contains("reset-kwork-full-scan", StringComparer.OrdinalIgnoreCase)) {
        using var resetKwork = new NpgsqlCommand("""
            UPDATE "Sources"
            SET "Credentials" = jsonb_set(
                    jsonb_set(
                        COALESCE(NULLIF("Credentials", ''), '{}')::jsonb,
                        '{kworkNextPage}',
                        '4'::jsonb,
                        true
                    ),
                    '{kworkFullScanCompleted}',
                    'false'::jsonb,
                    true
                )::text,
                "LastError" = NULL,
                "Status" = 1
            WHERE "Type" = 1
            """, conn);
        var reset = resetKwork.ExecuteNonQuery();
        Console.WriteLine($"Reset Kwork full-scan markers: {reset}");
        return;
    }

    if (args.Contains("enable-search", StringComparer.OrdinalIgnoreCase)) {
        using var enableSearch = new NpgsqlCommand("""
            UPDATE "SearchSettings"
            SET "IsEnabled" = true,
                "UpdatedAt" = now()
            """, conn);
        var enabled = enableSearch.ExecuteNonQuery();
        Console.WriteLine($"Enabled search settings: {enabled}");
        return;
    }

    if (args.Contains("inspect-hangfire", StringComparer.OrdinalIgnoreCase)) {
        Console.WriteLine("--- hangfire tables ---");
        using (var hangfireTables = new NpgsqlCommand("""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema ILIKE '%hangfire%' OR table_name ILIKE '%job%' OR table_name ILIKE '%server%'
            ORDER BY table_schema, table_name
            """, conn))
        using (var reader = hangfireTables.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)}.{reader.GetString(1)}");
            }
        }

        Console.WriteLine("--- hangfire jobs ---");
        using (var jobs = new NpgsqlCommand("""
            SELECT j.id, j.statename, j.createdat, COALESCE(s.reason, ''), LEFT(COALESCE(j.invocationdata::text, ''), 180)
            FROM hangfire.job j
            LEFT JOIN hangfire.state s ON s.jobid = j.id AND s.name = j.statename
            ORDER BY j.createdat DESC
            LIMIT 20
            """, conn))
        using (var reader = jobs.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetInt64(0)} | {reader.GetString(1)} | {reader.GetDateTime(2):O} | {reader.GetString(3)} | {reader.GetString(4)}");
            }
        }

        Console.WriteLine("--- hangfire servers ---");
        using (var servers = new NpgsqlCommand("""
            SELECT id, heartbeat, queues::text
            FROM hangfire.server
            ORDER BY heartbeat DESC
            """, conn))
        using (var reader = servers.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)} | {reader.GetDateTime(1):O} | {reader.GetString(2)}");
            }
        }
        return;
    }

    if (args.Contains("set-leads-ttl-24h", StringComparer.OrdinalIgnoreCase)) {
        using var alterDefault = new NpgsqlCommand("""
            ALTER TABLE "JobLeads"
            ALTER COLUMN "ExpiresAt" SET DEFAULT (now() + interval '24 hours')
            """, conn);
        alterDefault.ExecuteNonQuery();

        using var updateLeads = new NpgsqlCommand("""
            UPDATE "JobLeads"
            SET "ExpiresAt" = "FoundAt" + interval '24 hours'
            WHERE "ExpiresAt" > "FoundAt" + interval '24 hours'
            """, conn);
        var updated = updateLeads.ExecuteNonQuery();
        Console.WriteLine($"Updated leads TTL to 24h: {updated}");
        return;
    }

    if (args.Contains("repair-topic-source-names", StringComparer.OrdinalIgnoreCase)) {
        var repaired = 0;
        using var selectSources = new NpgsqlCommand("""
            SELECT "Id", "Name", "Credentials"
            FROM "Sources"
            WHERE "Type" = 0
              AND "Credentials" IS NOT NULL
            """, conn);

        var repairs = new List<(Guid SourceId, string SourceName)>();
        using (var reader = selectSources.ExecuteReader()) {
            while (reader.Read()) {
                var sourceId = reader.GetGuid(0);
                var name = reader.GetString(1);
                var credentials = reader.IsDBNull(2) ? "" : reader.GetString(2);
                string? topicName = null;

                try {
                    using var document = JsonDocument.Parse(credentials);
                    if (document.RootElement.TryGetProperty("TopicName", out var pascal) && pascal.ValueKind == JsonValueKind.String) {
                        topicName = pascal.GetString();
                    } else if (document.RootElement.TryGetProperty("topicName", out var camel) && camel.ValueKind == JsonValueKind.String) {
                        topicName = camel.GetString();
                    }
                } catch {
                    topicName = null;
                }

                if (!string.IsNullOrWhiteSpace(topicName)) {
                    repairs.Add((sourceId, $"{name} › {topicName}"));
                }
            }
        }

        foreach (var repair in repairs) {
            using var update = new NpgsqlCommand("""
                UPDATE "JobLeads"
                SET "SourceName" = @sourceName
                WHERE "SourceId" = @sourceId
                """, conn);
            update.Parameters.AddWithValue("sourceName", repair.SourceName);
            update.Parameters.AddWithValue("sourceId", repair.SourceId);
            repaired += update.ExecuteNonQuery();
        }

        Console.WriteLine($"Repaired topic source names: {repaired}");
        return;
    }

    if (args.Contains("restore-hidden-unverified-leads", StringComparer.OrdinalIgnoreCase)) {
        using var restore = new NpgsqlCommand("""
            UPDATE "JobLeads"
            SET "Status" = 0
            WHERE "Status" = 3
              AND "AiStatus" IN (0, 3, 4, 5)
              AND "ExpiresAt" > now()
            """, conn);
        var restored = restore.ExecuteNonQuery();
        Console.WriteLine($"Restored hidden unverified leads: {restored}");
        return;
    }

    if (args.Contains("restore-hidden-ai-rejected-leads", StringComparer.OrdinalIgnoreCase)) {
        using var restore = new NpgsqlCommand("""
            UPDATE "JobLeads"
            SET "Status" = 0
            WHERE "Status" = 3
              AND "AiStatus" = 2
              AND "ExpiresAt" > now()
            """, conn);
        var restored = restore.ExecuteNonQuery();
        Console.WriteLine($"Restored hidden AI rejected leads: {restored}");
        return;
    }

    if (args.Contains("inspect-search-settings", StringComparer.OrdinalIgnoreCase)) {
        Console.WriteLine("--- search settings ---");
        using (var cmd = new NpgsqlCommand("""
            SELECT s."ProfileId", p."Name", s."IsEnabled", s."IntervalMinutes", s."NotificationsEnabled",
                   s."UserKeywords", s."NegativeKeywords", s."NeedsAiExpansion", s."LastAiExpandedAt",
                   s."SearchProfileSummary", s."MustIncludeSignals", s."SoftSignals", s."RejectSignals",
                   s."ExpandedPositiveTerms", s."ExpandedIntentTerms", s."StrongTerms"
            FROM "SearchSettings" s
            JOIN "Profiles" p ON p."Id" = s."ProfileId"
            ORDER BY s."UpdatedAt" DESC NULLS LAST
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"profile={reader.GetGuid(0)} | name={reader.GetString(1)} | enabled={reader.GetBoolean(2)} | interval={reader.GetInt32(3)} | notify={reader.GetBoolean(4)}");
                Console.WriteLine($"keywords={string.Join(", ", reader.GetFieldValue<string[]>(5))}");
                Console.WriteLine($"negative={string.Join(", ", reader.GetFieldValue<string[]>(6))}");
                Console.WriteLine($"needsAi={reader.GetBoolean(7)} | lastAi={(reader.IsDBNull(8) ? "" : reader.GetValue(8))}");
                Console.WriteLine($"summary={(reader.IsDBNull(9) ? "" : reader.GetString(9))}");
                Console.WriteLine($"must={string.Join(", ", reader.GetFieldValue<string[]>(10).Take(20))}");
                Console.WriteLine($"soft={string.Join(", ", reader.GetFieldValue<string[]>(11).Take(20))}");
                Console.WriteLine($"reject={string.Join(", ", reader.GetFieldValue<string[]>(12).Take(20))}");
                Console.WriteLine($"expanded={string.Join(", ", reader.GetFieldValue<string[]>(13).Take(30))}");
                Console.WriteLine($"intent={string.Join(", ", reader.GetFieldValue<string[]>(14).Take(30))}");
                Console.WriteLine($"strong={string.Join(", ", reader.GetFieldValue<string[]>(15).Take(30))}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("--- search sources ---");
        using (var cmd = new NpgsqlCommand("""
            SELECT "Id", "ProfileId", "Type", "Name", "Url", "Status", "LastScraped", "Credentials"
            FROM "Sources"
            ORDER BY "Type", "CreatedAt"
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetGuid(0)} | profile={reader.GetGuid(1)} | type={reader.GetInt32(2)} | name={reader.GetString(3)} | status={reader.GetInt32(5)} | scraped={(reader.IsDBNull(6) ? "" : reader.GetValue(6))} | url={reader.GetString(4)} | credentials={(reader.IsDBNull(7) ? "" : reader.GetString(7))}");
            }
        }

        return;
    }

    if (args.Contains("mark-search-profile-for-ai-expansion", StringComparer.OrdinalIgnoreCase)) {
        using var mark = new NpgsqlCommand("""
            UPDATE "SearchSettings"
            SET "NeedsAiExpansion" = true,
                "UpdatedAt" = now()
            WHERE "UserKeywords" && ARRAY['сайт','frontend','react','web','angular','vue','js','html/css','ui/ux']::text[]
            """, conn);
        var marked = mark.ExecuteNonQuery();
        Console.WriteLine($"Marked search profiles for AI expansion: {marked}");
        return;
    }

    if (args.Contains("inspect-kwork-recent", StringComparer.OrdinalIgnoreCase)) {
        Console.WriteLine("--- recent kwork leads ---");
        using (var cmd = new NpgsqlCommand("""
            SELECT "ExternalId", "Title", "Status", "AiStatus", "FoundAt", "ExpiresAt"
            FROM "JobLeads"
            WHERE "SourceType" = 1
            ORDER BY "FoundAt" DESC
            LIMIT 80
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)} | title={reader.GetString(1)} | status={reader.GetInt32(2)} | ai={reader.GetInt32(3)} | found={reader.GetValue(4)} | exp={reader.GetValue(5)}");
            }
        }

        Console.WriteLine("--- exact new kwork ids ---");
        using (var cmd = new NpgsqlCommand("""
            SELECT "ExternalId", "Title", "Status", "AiStatus", "FoundAt", "ExpiresAt"
            FROM "JobLeads"
            WHERE "ExternalId" = ANY(@ids)
            ORDER BY "ExternalId"
            """, conn)) {
            cmd.Parameters.AddWithValue("ids", new[] {
                "kwork:3210494",
                "kwork:3210473",
                "kwork:3210379",
                "kwork:3210428",
                "kwork:3210369",
                "kwork:3195442"
            });

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)} | title={reader.GetString(1)} | status={reader.GetInt32(2)} | ai={reader.GetInt32(3)} | found={reader.GetValue(4)} | exp={reader.GetValue(5)}");
            }
        }

        return;
    }

    if (args.Contains("repair-current-gamedev-search", StringComparer.OrdinalIgnoreCase)) {
        using var clearExpansion = new NpgsqlCommand("""
            UPDATE "SearchSettings"
            SET "SearchProfileSummary" = '',
                "MustIncludeSignals" = ARRAY[]::text[],
                "SoftSignals" = ARRAY[]::text[],
                "RejectSignals" = ARRAY[]::text[],
                "ExpandedPositiveTerms" = ARRAY[]::text[],
                "ExpandedIntentTerms" = ARRAY[]::text[],
                "StrongTerms" = ARRAY[]::text[],
                "NeedsAiExpansion" = true,
                "UpdatedAt" = now()
            WHERE "UserKeywords" && ARRAY['gamedev','unity','webgl','игра','C#']::text[]
            """, conn);
        var cleared = clearExpansion.ExecuteNonQuery();

        using var activateSources = new NpgsqlCommand("""
            UPDATE "Sources"
            SET "Status" = 1,
                "LastError" = NULL
            WHERE "Type" = 0
              AND COALESCE("Credentials", '{}')::jsonb ->> 'Purpose' = '0'
            """, conn);
        var activated = activateSources.ExecuteNonQuery();

        using var restoreLeads = new NpgsqlCommand("""
            UPDATE "JobLeads"
            SET "Status" = 0,
                "AiStatus" = 3,
                "AiConfidence" = NULL,
                "AiReason" = 'Старый AI-профиль был сброшен после смены ключевых слов.',
                "AiSummary" = NULL,
                "AiCategory" = NULL
            WHERE "Status" = 3
              AND "ExpiresAt" > now()
              AND (
                  "Title" ILIKE '%unity%' OR "Content" ILIKE '%unity%' OR
                  "Title" ILIKE '%gamedev%' OR "Content" ILIKE '%gamedev%' OR
                  "Title" ILIKE '%webgl%' OR "Content" ILIKE '%webgl%' OR
                  "Title" ILIKE '%игр%' OR "Content" ILIKE '%игр%'
              )
            """, conn);
        var restored = restoreLeads.ExecuteNonQuery();

        Console.WriteLine($"Cleared stale GameDev expansion: {cleared}");
        Console.WriteLine($"Activated search Telegram sources: {activated}");
        Console.WriteLine($"Restored hidden GameDev leads as unverified: {restored}");
        return;
    }

    if (args.Contains("hide-ai-rejected-leads", StringComparer.OrdinalIgnoreCase)) {
        using var hide = new NpgsqlCommand("""
            UPDATE "JobLeads"
            SET "Status" = 3
            WHERE "Status" <> 3
              AND "AiStatus" = 2
            """, conn);
        var hidden = hide.ExecuteNonQuery();
        Console.WriteLine($"Hidden AI rejected leads: {hidden}");
        return;
    }

    if (args.Contains("retry-ai-errors", StringComparer.OrdinalIgnoreCase)) {
        var ai = ReadAiConfig();
        if (string.IsNullOrWhiteSpace(ai.ApiKey)) {
            Console.WriteLine("AI key is missing.");
            return;
        }

        using var select = new NpgsqlCommand("""
            SELECT l."Id", l."ExternalId", l."Title", l."Content", l."SourceName",
                   s."UserKeywords", s."NegativeKeywords", s."SearchProfileSummary",
                   s."MustIncludeSignals", s."SoftSignals", s."RejectSignals",
                   s."ExpandedPositiveTerms", s."ExpandedIntentTerms", s."StrongTerms"
            FROM "JobLeads" l
            JOIN "SearchSettings" s ON s."ProfileId" = l."ProfileId"
            WHERE l."AiStatus" = 5
              AND l."Status" <> 3
              AND l."ExpiresAt" > now()
            ORDER BY l."FoundAt" DESC
            LIMIT 80
            """, conn);

        var leads = new List<RetryLead>();
        using (var reader = select.ExecuteReader()) {
            while (reader.Read()) {
                leads.Add(new RetryLead(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(5),
                    reader.IsDBNull(6) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(6),
                    reader.IsDBNull(7) ? "" : reader.GetString(7),
                    reader.IsDBNull(8) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(8),
                    reader.IsDBNull(9) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(9),
                    reader.IsDBNull(10) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(10),
                    reader.IsDBNull(11) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(11),
                    reader.IsDBNull(12) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(12),
                    reader.IsDBNull(13) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(13)));
            }
        }

        var updated = 0;
        foreach (var batch in leads.Chunk(3)) {
            var result = await ClassifyRetryBatchAsync(ai, batch);
            if (result?.Items == null) {
                continue;
            }

            foreach (var item in result.Items.Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))) {
                var lead = batch.FirstOrDefault(lead => string.Equals(lead.ExternalId, item.ExternalId, StringComparison.OrdinalIgnoreCase));
                if (lead == null) {
                    continue;
                }

                using var update = new NpgsqlCommand("""
                    UPDATE "JobLeads"
                    SET "AiStatus" = @aiStatus,
                        "Status" = CASE WHEN @aiStatus = 2 THEN 3 WHEN "Status" = 3 THEN 0 ELSE "Status" END,
                        "AiConfidence" = @confidence,
                        "AiSummary" = @summary,
                        "AiCategory" = @category,
                        "AiReason" = @reason
                    WHERE "Id" = @id
                    """, conn);
                update.Parameters.AddWithValue("aiStatus", item.IsRelevant && item.Confidence >= 70 ? 1 : 2);
                update.Parameters.AddWithValue("confidence", Math.Clamp(item.Confidence, 0, 100));
                update.Parameters.AddWithValue("summary", item.Summary ?? "");
                update.Parameters.AddWithValue("category", item.Category ?? "");
                update.Parameters.AddWithValue("reason", item.Reason ?? "");
                update.Parameters.AddWithValue("id", lead.Id);
                updated += update.ExecuteNonQuery();
            }
        }

        Console.WriteLine($"Retried AI error leads: {updated} of {leads.Count}");
        return;
    }

    if (args.Contains("inspect", StringComparer.OrdinalIgnoreCase)) {
        using (var cmd = new NpgsqlCommand("""
            select 'profiles' as kind, count(*)::text from "Profiles"
            union all select 'sources', count(*)::text from "Sources"
            union all select 'jobleads', count(*)::text from "JobLeads"
            union all select 'kwork_sources', count(*)::text from "Sources" where "Type" = 1
            union all select 'kwork_leads', count(*)::text from "JobLeads" where "SourceType" = 1
            union all select 'visible_leads', count(*)::text from "JobLeads" where "Status" <> 3 and "ExpiresAt" > now()
            union all select 'visible_kwork_leads', count(*)::text from "JobLeads" where "SourceType" = 1 and "Status" <> 3 and "ExpiresAt" > now()
            union all select 'hidden_telegram_leads', count(*)::text from "JobLeads" where "SourceType" = 0 and "Status" = 3
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
            }
        }

        Console.WriteLine("--- sources ---");
        using (var cmd = new NpgsqlCommand("""
            select a."Id", a."Email", a."ActiveProfileId", p."Name"
            from "Accounts" a
            left join "Profiles" p on p."Id" = a."ActiveProfileId"
            order by a."CreatedAt" desc
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            Console.WriteLine("--- accounts ---");
            while (reader.Read()) {
                var activeProfileId = reader.IsDBNull(2) ? "" : reader.GetGuid(2).ToString();
                var profileName = reader.IsDBNull(3) ? "" : reader.GetString(3);
                Console.WriteLine($"{reader.GetGuid(0)} | email={reader.GetString(1)} | activeProfile={activeProfileId} | activeProfileName={profileName}");
            }
        }

        Console.WriteLine("--- sources ---");
        using (var cmd = new NpgsqlCommand("""
            select "Id", "ProfileId", "Type", "Name", "Url", "Status", "LastError", "LastScraped", "CreatedAt"
            from "Sources"
            order by "CreatedAt" desc
            limit 20
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                var lastError = reader.IsDBNull(6) ? "" : reader.GetString(6);
                var lastScraped = reader.IsDBNull(7) ? "" : reader.GetValue(7)?.ToString();
                Console.WriteLine($"{reader.GetGuid(0)} | profile={reader.GetGuid(1)} | type={reader.GetInt32(2)} | name={reader.GetString(3)} | url={reader.GetString(4)} | status={reader.GetInt32(5)} | err={lastError} | scraped={lastScraped} | created={reader.GetValue(8)}");
            }
        }

        Console.WriteLine("--- leads ---");
        using (var cmd = new NpgsqlCommand("""
            select "Id", "ProfileId", "SourceId", "SourceType", "SourceName", "ExternalId", "Title", "Status", "AiStatus", "FoundAt", "ExpiresAt"
            from "JobLeads"
            order by "FoundAt" desc
            limit 50
            """, conn))
        using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
                var title = reader.IsDBNull(6) ? "" : reader.GetString(6);
                Console.WriteLine($"{reader.GetGuid(0)} | profile={reader.GetGuid(1)} | source={reader.GetGuid(2)} | stype={reader.GetInt32(3)} | sname={reader.GetString(4)} | ext={reader.GetString(5)} | title={title} | status={reader.GetInt32(7)} | ai={reader.GetInt32(8)} | found={reader.GetValue(9)} | exp={reader.GetValue(10)}");
            }
        }

        return;
    }

    var tables = new[] { "Profiles", "SearchSettings", "Sources", "Accounts" };
    foreach (var table in tables) {
        using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", conn);
        var count = cmd.ExecuteScalar();
        Console.WriteLine($"Total {table} in DB: {count}");
    }
} catch (Exception ex) {
    Console.WriteLine(ex.Message);
}

static AiConfig ReadAiConfig() {
    var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "ClientScout.Api", "appsettings.Development.json");
    path = Path.GetFullPath(path);
    if (!File.Exists(path)) {
        path = Path.GetFullPath(Path.Combine("src", "ClientScout.Api", "appsettings.Development.json"));
    }

    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var ai = document.RootElement.GetProperty("AI");
    var fallbacks = ai.TryGetProperty("ModelFallbacks", out var fallbackElement) && fallbackElement.ValueKind == JsonValueKind.Array
        ? fallbackElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray()
        : Array.Empty<string>();

    return new AiConfig(
        ai.GetProperty("ApiKey").GetString() ?? "",
        ai.GetProperty("BaseUrl").GetString() ?? "https://openrouter.ai/api/v1",
        ai.GetProperty("Model").GetString() ?? "openrouter/free",
        fallbacks);
}

static async Task<RetryBatchResult?> ClassifyRetryBatchAsync(AiConfig config, IReadOnlyCollection<RetryLead> leads) {
    var models = new[] { config.Model }
        .Concat(config.ModelFallbacks)
        .Where(model => !string.IsNullOrWhiteSpace(model))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var first = leads.First();
    var input = new {
        first.UserKeywords,
        first.NegativeKeywords,
        first.SearchProfileSummary,
        MustIncludeSignals = first.MustIncludeSignals.Take(20).ToArray(),
        SoftSignals = first.SoftSignals.Take(30).ToArray(),
        RejectSignals = first.RejectSignals.Take(20).ToArray(),
        ExpandedPositiveTerms = first.ExpandedPositiveTerms.Take(35).ToArray(),
        ExpandedIntentTerms = first.ExpandedIntentTerms.Take(35).ToArray(),
        StrongTerms = first.StrongTerms.Take(25).ToArray(),
        candidates = leads.Select(lead => new {
            lead.ExternalId,
            source = lead.SourceName,
            rawText = $"{lead.Title}\n{lead.Content}"
        }).ToArray()
    };

    var prompt = $$"""
Classify a batch of freelance/job lead candidates against the user's hidden search profile.
Return STRICT JSON only:
{
  "items": [
    {
      "externalId": "candidate id",
      "isRelevant": true,
      "confidence": 0,
      "summary": "short Russian summary, max 180 chars",
      "category": "short category",
      "reason": "short reason"
    }
  ]
}

Rules:
- confidence is 0-100.
- isRelevant=true only when the main requested deliverable directly matches the profile.
- If relevance is unclear or weakly adjacent, return isRelevant=false with confidence below 70.
- Return one result for every candidate externalId from the input.
- reason must explain the semantic comparison in one short Russian sentence.

Input JSON:
{{JsonSerializer.Serialize(input)}}
""";

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
    foreach (var model in models) {
        var payload = new {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl.TrimEnd('/')}/chat/completions") {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://clientscout.local");
        request.Headers.TryAddWithoutValidation("X-Title", "ClientScout");

        try {
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                Console.WriteLine($"AI retry model failed: {model} {(int)response.StatusCode}");
                continue;
            }

            using var responseDocument = JsonDocument.Parse(body);
            var content = responseDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(content)) {
                continue;
            }

            content = ExtractJson(content);
            return JsonSerializer.Deserialize<RetryBatchResult>(content, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        } catch (Exception ex) {
            Console.WriteLine($"AI retry model failed locally: {model} {ex.GetType().Name}");
        }
    }

    return null;
}

static string ExtractJson(string text) {
    text = text.Trim();
    if (text.StartsWith("```", StringComparison.Ordinal)) {
        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd >= 0) {
            text = text[(firstLineEnd + 1)..].Trim();
        }

        if (text.EndsWith("```", StringComparison.Ordinal)) {
            text = text[..^3].Trim();
        }
    }

    return text;
}

sealed record AiConfig(string ApiKey, string BaseUrl, string Model, string[] ModelFallbacks);

sealed record RetryLead(
    Guid Id,
    string ExternalId,
    string Title,
    string Content,
    string SourceName,
    string[] UserKeywords,
    string[] NegativeKeywords,
    string SearchProfileSummary,
    string[] MustIncludeSignals,
    string[] SoftSignals,
    string[] RejectSignals,
    string[] ExpandedPositiveTerms,
    string[] ExpandedIntentTerms,
    string[] StrongTerms);

sealed record RetryBatchResult(RetryBatchItem[]? Items);

sealed record RetryBatchItem(
    string ExternalId,
    bool IsRelevant,
    int Confidence,
    string? Summary,
    string? Category,
    string? Reason);
