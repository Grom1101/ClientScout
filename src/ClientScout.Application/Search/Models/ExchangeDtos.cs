using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search.Models;

public record ExchangeConnectionDto(
    Guid Id,
    Guid ProfileId,
    ExchangeType ExchangeType,
    ExchangeConnectionStatus Status,
    bool IsConnected,
    bool RequiresReconnect,
    DateTimeOffset? LastCheckedAt,
    string? LastError,
    DateTimeOffset UpdatedAt);

public record ConnectExchangeDto(
    Guid ProfileId,
    ExchangeType ExchangeType,
    string Session);

public record DisconnectExchangeDto(
    Guid ProfileId,
    ExchangeType ExchangeType);

public record ExchangeLoginStartDto(
    Guid ProfileId,
    ExchangeType ExchangeType);

public record ExchangeLoginStartResult(
    Guid FlowId,
    string Status,
    string Instructions);

public record ExchangeLoginFlowStatusDto(
    Guid FlowId,
    string Status,
    bool IsCompleted,
    bool IsFailed,
    string? Error);
