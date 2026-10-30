using System;
using Microsoft.AspNetCore.Identity;

namespace Delivery.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
}
