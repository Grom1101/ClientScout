using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;

namespace ClientScout.Application.Auth;

public class TelegramAuthService
{
    private readonly string _botToken;

    public TelegramAuthService(IConfiguration config)
    {
        _botToken = config["TELEGRAM_BOT_TOKEN"] ?? throw new ArgumentNullException("TELEGRAM_BOT_TOKEN is missing");
    }

    public bool ValidateInitData(string initData)
    {
        var query = HttpUtility.ParseQueryString(initData);
        var hash = query["hash"];
        if (string.IsNullOrEmpty(hash)) return false;

        var dataCheckString = string.Join("\n", query.AllKeys
            .Where(k => k != "hash")
            .OrderBy(k => k)
            .Select(k => $"{k}={query[k]}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmac.ComputeHash(Encoding.UTF8.GetBytes(_botToken));

        using var hmacData = new HMACSHA256(secretKey);
        var computedHash = BitConverter.ToString(hmacData.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).Replace("-", "").ToLower();

        return computedHash == hash;
    }
}
