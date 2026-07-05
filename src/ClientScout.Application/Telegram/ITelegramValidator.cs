using System.Threading.Tasks;
using ClientScout.Application.Sources.Models;

namespace ClientScout.Application.Telegram;

public interface ITelegramValidator
{
    Task<ValidateSourceResponseDto> ValidateChatAsync(string userId, string url);
}
