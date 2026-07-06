using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ClientScout.Application.Search;

public class KworkBrowserLoginService : IKworkBrowserLoginService, IExchangeBrowserLoginService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KworkBrowserLoginService> _logger;
    private readonly ConcurrentDictionary<Guid, FlowState> _flows = new();

    public KworkBrowserLoginService(IServiceScopeFactory scopeFactory, ILogger<KworkBrowserLoginService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Domain.Enums.ExchangeType ExchangeType => Domain.Enums.ExchangeType.Kwork;

    public Task<Models.ExchangeLoginStartResult> StartAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken = default)
    {
        var flowId = Guid.NewGuid();
        _flows[flowId] = new FlowState(flowId, "starting", false, false, null);
        _ = Task.Run(() => RunFlowAsync(flowId, accountId, profileId), CancellationToken.None);

        return Task.FromResult(new Models.ExchangeLoginStartResult(
            flowId,
            "starting",
            "Откроется окно Kwork. Войдите в аккаунт, после успешного входа ClientScout сам сохранит сессию."));
    }

    public Models.ExchangeLoginFlowStatusDto GetStatus(Guid flowId)
    {
        return _flows.TryGetValue(flowId, out var state)
            ? new Models.ExchangeLoginFlowStatusDto(state.FlowId, state.Status, state.IsCompleted, state.IsFailed, state.Error)
            : new Models.ExchangeLoginFlowStatusDto(flowId, "not_found", false, true, "LOGIN_FLOW_NOT_FOUND");
    }

    private async Task RunFlowAsync(Guid flowId, Guid accountId, Guid profileId)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            Update(flowId, "opening_browser");
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = ["--disable-blink-features=AutomationControlled"]
            });

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "ru-RU",
                ViewportSize = new ViewportSize { Width = 1280, Height = 860 }
            });

            var page = await context.NewPageAsync();
            try
            {
                _ = page.GotoAsync("https://kwork.ru/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 0 });
            }
            catch { }

            Update(flowId, "waiting_for_login");

            var deadline = DateTimeOffset.UtcNow.AddMinutes(7);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                var cookies = await context.CookiesAsync(["https://kwork.ru"]);
                var hasUsefulCookies = cookies.Any(c => c.Name.Equals("slrem", StringComparison.OrdinalIgnoreCase));
                var urlLooksLoggedIn = !page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) &&
                                       !page.Url.Contains("/signup", StringComparison.OrdinalIgnoreCase);

                var isDomLoggedIn = false;
                try {
                    isDomLoggedIn = await page.EvaluateAsync<bool>("() => document.querySelector('.header-user') !== null || document.querySelector('.user-avatar') !== null || window.actorId !== undefined");
                } catch { }

                if ((hasUsefulCookies || isDomLoggedIn) && urlLooksLoggedIn)
                {
                    var session = string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));
                    using var scope = _scopeFactory.CreateScope();
                    var exchangeConnectionService = scope.ServiceProvider.GetRequiredService<IExchangeConnectionService>();
                    await exchangeConnectionService.ConnectAsync(accountId, new Models.ConnectExchangeDto(
                        profileId,
                        Domain.Enums.ExchangeType.Kwork,
                        session));

                    Update(flowId, "connected", true);
                    await browser.CloseAsync();
                    return;
                }
            }

            Update(flowId, "timeout", false, true, "Не дождался входа в Kwork за 7 минут.");
        }
        catch (PlaywrightException ex)
        {
            var message = ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                ? "Playwright browser не установлен. Выполните: pwsh src/ClientScout.Api/bin/Debug/net10.0/playwright.ps1 install chromium"
                : ex.Message;
            Update(flowId, "failed", false, true, message);
            _logger.LogWarning(ex, "Kwork browser login flow failed");
        }
        catch (Exception ex)
        {
            Update(flowId, "failed", false, true, ex.Message);
            _logger.LogWarning(ex, "Kwork browser login flow failed");
        }
        finally
        {
            if (browser != null)
            {
                await browser.DisposeAsync();
            }

            playwright?.Dispose();
        }
    }

    private void Update(Guid flowId, string status, bool isCompleted = false, bool isFailed = false, string? error = null)
    {
        _flows[flowId] = new FlowState(flowId, status, isCompleted, isFailed, error);
    }

    private sealed record FlowState(Guid FlowId, string Status, bool IsCompleted, bool IsFailed, string? Error);
}
