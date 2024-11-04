using Microsoft.IdentityModel.Tokens;
using MinimalAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MinimalAPI.Contracts
{
    public interface ITokenService
    {
        string BuildToken(string key, string issuer, string audience, Person user);
    }
    public class TokenService : ITokenService
    {
        private TimeSpan ExpiryDuration = new TimeSpan(0, 30, 0);
        public string BuildToken(string key, string issuer, string audience, Person user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Uname),
                new Claim("Password", user.Password),
                new Claim(ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString())
            };
            
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
            var tokenDescriptor = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.Now.Add(ExpiryDuration), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}
