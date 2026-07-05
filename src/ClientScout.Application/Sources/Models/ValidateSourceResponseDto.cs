using System.Collections.Generic;

namespace ClientScout.Application.Sources.Models;

public class ValidateSourceResponseDto
{
    public bool IsValid { get; set; }
    public bool IsForum { get; set; }
    public string? ErrorCode { get; set; } // NOT_A_MEMBER, READ_ONLY, NOT_FOUND
    public List<TopicDto>? Topics { get; set; }
}

public class TopicDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool CanWrite { get; set; } = true;
}
