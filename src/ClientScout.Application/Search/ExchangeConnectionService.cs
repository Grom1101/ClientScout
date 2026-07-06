using System.Text;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Search;

public class ExchangeConnectionService : IExchangeConnectionService
{
    private readonly IAppDbContext _dbContext;
    private readonly IExchangeProviderRegistry _providerRegistry;

    public ExchangeConnectionService(IAppDbContext dbContext, IExchangeProviderRegistry providerRegistry)
    {
        _dbContext = dbContext;
        _providerRegistry = providerRegistry;
    }

    public async Task<List<ExchangeConnectionDto>> GetConnectionsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(profileId, accountId, cancellationToken);

        var connections = await _dbContext.ExchangeConnections
            .Where(c => c.ProfileId == profileId)
            .OrderBy(c => c.ExchangeType)
            .ToListAsync(cancellationToken);

        foreach (var provider in _providerRegistry.GetProviders())
        {
            if (connections.All(connection => connection.ExchangeType != provider.ExchangeType))
            {
                connections.Add(new ExchangeConnection
                {
                    Id = Guid.Empty,
                    ProfileId = profileId,
                    ExchangeType = provider.ExchangeType,
                    IsConnected = false
                });
            }
        }

        return connections.Select(Map).ToList();
    }

    public async Task<ExchangeLoginStartResult> StartLoginAsync(Guid accountId, ExchangeLoginStartDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(dto.ProfileId, accountId, cancellationToken);
        var provider = _providerRegistry.GetRequired(dto.ExchangeType);

        var instruction = provider.SupportsBrowserLogin
            ? $"Для {provider.DisplayName} используется browser login-flow через кнопку подключения приложения."
            : $"{provider.DisplayName} пока не поддерживает автоматическое подключение.";

        return new ExchangeLoginStartResult(Guid.Empty, "unsupported_fallback", instruction);
    }

    public async Task<ExchangeConnectionDto> ConnectAsync(Guid accountId, ConnectExchangeDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(dto.ProfileId, accountId, cancellationToken);
        var provider = _providerRegistry.GetRequired(dto.ExchangeType);
        if (!provider.SupportsManualSession)
        {
            throw new ArgumentException("UNSUPPORTED_EXCHANGE");
        }

        if (string.IsNullOrWhiteSpace(dto.Session))
        {
            throw new ArgumentException("SESSION_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        var connection = await _dbContext.ExchangeConnections
            .FirstOrDefaultAsync(c => c.ProfileId == dto.ProfileId && c.ExchangeType == dto.ExchangeType, cancellationToken);

        if (connection == null)
        {
            connection = new ExchangeConnection
            {
                Id = Guid.NewGuid(),
                ProfileId = dto.ProfileId,
                ExchangeType = dto.ExchangeType,
                CreatedAt = now
            };
            _dbContext.ExchangeConnections.Add(connection);
        }

        connection.EncryptedSession = Convert.ToBase64String(Encoding.UTF8.GetBytes(dto.Session.Trim()));
        connection.IsConnected = true;
        connection.RequiresReconnect = false;
        connection.LastError = null;
        connection.UpdatedAt = now;

        await EnsureExchangeSourceActiveAsync(provider, dto.ProfileId, now, cancellationToken);
        await StopSearchAsync(dto.ProfileId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(connection);
    }

    public async Task<ExchangeConnectionDto> DisconnectAsync(Guid accountId, DisconnectExchangeDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureProfileAccessAsync(dto.ProfileId, accountId, cancellationToken);
        var provider = _providerRegistry.GetRequired(dto.ExchangeType);

        var now = DateTimeOffset.UtcNow;
        var connection = await _dbContext.ExchangeConnections
            .FirstOrDefaultAsync(c => c.ProfileId == dto.ProfileId && c.ExchangeType == dto.ExchangeType, cancellationToken);

        if (connection == null)
        {
            connection = new ExchangeConnection
            {
                Id = Guid.NewGuid(),
                ProfileId = dto.ProfileId,
                ExchangeType = dto.ExchangeType,
                CreatedAt = now
            };
            _dbContext.ExchangeConnections.Add(connection);
        }

        connection.IsConnected = false;
        connection.RequiresReconnect = false;
        connection.LastError = null;
        connection.UpdatedAt = now;

        await MarkExchangeSourceInactiveAsync(provider, dto.ProfileId, cancellationToken);
        await StopSearchAsync(dto.ProfileId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(connection);
    }

    public async Task MarkRequiresReconnectAsync(Guid profileId, ExchangeType exchangeType, string error, CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.ExchangeConnections
            .FirstOrDefaultAsync(c => c.ProfileId == profileId && c.ExchangeType == exchangeType, cancellationToken);

        if (connection == null)
        {
            return;
        }

        connection.IsConnected = false;
        connection.RequiresReconnect = true;
        connection.LastError = error;
        connection.UpdatedAt = DateTimeOffset.UtcNow;

        if (TryGetProvider(exchangeType, out var provider))
        {
            var source = await _dbContext.Sources
                .FirstOrDefaultAsync(
                    s => s.ProfileId == profileId && s.Type == provider.SourceType && s.Url == provider.SourceUrl,
                    cancellationToken);

            if (source != null)
            {
                source.Status = SourceStatus.Error;
                source.LastError = error;
            }
        }

        await StopSearchAsync(profileId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task StopSearchAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SearchSettings
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        if (settings != null && settings.IsEnabled)
        {
            settings.IsEnabled = false;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task EnsureProfileAccessAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private ExchangeConnectionDto Map(ExchangeConnection connection)
    {
        var provider = _providerRegistry.GetRequired(connection.ExchangeType);
        var status = connection.RequiresReconnect
            ? ExchangeConnectionStatus.RequiresReconnect
            : connection.IsConnected
                ? ExchangeConnectionStatus.Connected
                : ExchangeConnectionStatus.NotConnected;

        return new ExchangeConnectionDto(
            connection.Id,
            connection.ProfileId,
            connection.ExchangeType,
            provider.Key,
            provider.DisplayName,
            status,
            connection.IsConnected,
            connection.RequiresReconnect,
            provider.SupportsBrowserLogin,
            provider.SupportsManualSession,
            provider.IsAvailable,
            connection.LastCheckedAt,
            connection.LastError,
            connection.UpdatedAt);
    }

    private async Task EnsureExchangeSourceActiveAsync(IExchangeProvider provider, Guid profileId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources
            .FirstOrDefaultAsync(
                s => s.ProfileId == profileId && s.Type == provider.SourceType && s.Url == provider.SourceUrl,
                cancellationToken);

        if (source == null)
        {
            _dbContext.Sources.Add(new Source
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                Type = provider.SourceType,
                Name = provider.SourceName,
                Url = provider.SourceUrl,
                Credentials = provider.SourceCredentials,
                Status = SourceStatus.Active,
                CreatedAt = now
            });
            return;
        }

        source.Status = SourceStatus.Active;
        source.LastError = null;
    }

    private async Task MarkExchangeSourceInactiveAsync(IExchangeProvider provider, Guid profileId, CancellationToken cancellationToken)
    {
        var source = await _dbContext.Sources
            .FirstOrDefaultAsync(
                s => s.ProfileId == profileId && s.Type == provider.SourceType && s.Url == provider.SourceUrl,
                cancellationToken);

        if (source == null)
        {
            return;
        }

        source.Status = SourceStatus.Pending;
        source.LastError = null;
    }

    private bool TryGetProvider(ExchangeType exchangeType, out IExchangeProvider provider)
    {
        try
        {
            provider = _providerRegistry.GetRequired(exchangeType);
            return true;
        }
        catch (ArgumentException)
        {
            provider = null!;
            return false;
        }
    }
}
