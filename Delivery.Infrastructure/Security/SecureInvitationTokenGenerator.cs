using System.Security.Cryptography;
using Delivery.Application.Abstractions.Security;

namespace Delivery.Infrastructure.Security;

public sealed class SecureInvitationTokenGenerator
    : IInvitationTokenGenerator
{
    public string Generate()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
    }
}
