using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace CurrencyConverterApi.Infrastructure.Policies
{
    /// <summary>
    /// Defines and configures the authorization policies for the API
    /// </summary>
    public static class AuthorizationPolicies
    {
        /// <summary>
        /// Configures authorization policies for the application
        /// </summary>
        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            // Admin policy - requires user to have Admin role
            options.AddPolicy("Admin", policy => 
                policy.RequireClaim(ClaimTypes.Role, "Admin"));
            
            // User policy - requires user to have User role (or Admin which supersedes it)
            options.AddPolicy("User", policy => 
                policy.RequireAssertion(context => 
                    context.User.HasClaim(c => 
                        c.Type == ClaimTypes.Role && 
                        (c.Value == "User" || c.Value == "Admin"))));
            
            // ReadOnly policy - for endpoints that only require authentication
            options.AddPolicy("ReadOnly", policy => 
                policy.RequireAuthenticatedUser());
        }
    }
}