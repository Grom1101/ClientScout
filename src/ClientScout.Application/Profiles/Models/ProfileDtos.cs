using System;
using System.Collections.Generic;

namespace ClientScout.Application.Profiles.Models;

public record ProfileDto(
    Guid Id,
    string Name,
    string Color,
    bool IsActive,
    bool IsDefault,
    List<string> Keywords,
    List<string> NegativeKeywords,
    decimal? MinBudget,
    string? LanguageFilter,
    DateTimeOffset CreatedAt
);

public record CreateProfileDto(
    string Name,
    string? Color,
    List<string>? Keywords,
    List<string>? NegativeKeywords,
    decimal? MinBudget,
    string? LanguageFilter
);

public record UpdateProfileDto(
    string Name,
    string? Color,
    bool IsActive,
    List<string>? Keywords,
    List<string>? NegativeKeywords,
    decimal? MinBudget,
    string? LanguageFilter
);
