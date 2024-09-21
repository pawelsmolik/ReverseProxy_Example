using System.Security.Claims;

namespace ReverseProxy_Example
{
    internal class TokenService
    {
        internal Task<string> GetAuthTokenAsync(ClaimsPrincipal user)
        {
            // we only have tokens for bob
            if (string.Equals("Bob", user.Identity.Name))
            {
                return Task.FromResult(Guid.NewGuid().ToString());
            }
            return Task.FromResult<string>(null);
        }
    }
}
