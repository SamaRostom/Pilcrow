using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Pilcrow.Controllers;

[Authorize]
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId
    {
        get
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new UnauthorizedAccessException("No user id in token.");
        }
    }
}