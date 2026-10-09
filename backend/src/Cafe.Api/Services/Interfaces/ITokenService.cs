using Cafe.Api.Models.Entities;
namespace Cafe.Api.Services.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}