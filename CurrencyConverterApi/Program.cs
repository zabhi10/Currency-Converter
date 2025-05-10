using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using CurrencyConverterApi.Infrastructure.Configuration;
using CurrencyConverterApi.Infrastructure.Policies;
using CurrencyConverterApi.Services;
using CurrencyConverterApi.Services.Factory;
using CurrencyConverterApi.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Text.Json;
using FluentValidation.AspNetCore;
using System.Reflection;
using FluentValidation;


var builder = WebApplication.CreateBuilder(args);

// Add environment-specific configuration files
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Bind the configuration
var currencyApiSettings = new CurrencyApiSettings();
builder.Configuration.GetSection("CurrencyApi").Bind(currencyApiSettings);
builder.Services.Configure<CurrencyApiSettings>(builder.Configuration.GetSection("CurrencyApi"));

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add controllers and API versioning
builder.Services.AddControllers(); // Keep this line as is

// Register FluentValidation services
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add API documentation
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Currency Converter API",
        Version = "v1",
        Description = "A REST API for currency conversion and exchange rates",
    });
    
    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
    
    // Add JWT authentication support
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n" +
                     "Enter your token in the text input below.\r\n\r\n" +
                     "Example: \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });
    
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add memory cache
builder.Services.AddMemoryCache();

// Configure HttpClient for Frankfurter API with resilience policies
builder.Services.AddHttpClient("Frankfurter", (sp, client) => 
{
    var settings = sp.GetRequiredService<IOptions<CurrencyApiSettings>>().Value;
    client.BaseAddress = new Uri(settings.FrankfurterApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
})
.AddPolicyHandler((sp, _) => 
{
    var settings = sp.GetRequiredService<IOptions<CurrencyApiSettings>>().Value;
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            settings.Retry.MaxRetryAttempts, 
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
})
.AddPolicyHandler((sp, _) => 
{
    var settings = sp.GetRequiredService<IOptions<CurrencyApiSettings>>().Value;
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            settings.Retry.CircuitBreakerThreshold, 
            TimeSpan.FromMinutes(settings.Retry.CircuitBreakerDurationMinutes));
});

// Register services
builder.Services.AddSingleton<ICurrencyProvider, FrankfurterCurrencyProvider>();
builder.Services.AddSingleton<ICurrencyService, CachedCurrencyService>();
builder.Services.AddSingleton<CurrencyProviderFactory>();

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var settings = builder.Configuration.GetSection("CurrencyApi:Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(settings["Issuer"]),
            ValidateAudience = !string.IsNullOrEmpty(settings["Audience"]),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings["Issuer"],
            ValidAudience = settings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(settings["SecretKey"] ?? "default_key_for_development_only"))
        };
        
        // Add diagnostic events for JWT token processing
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context => 
            {
                Log.Debug("JWT Auth: Token received");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var clientId = context.Principal?.FindFirst("client_id")?.Value ?? 
                               context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                Log.Information("JWT Auth: Token successfully validated for client {ClientId}", clientId);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT Auth: Authentication failed: {Exception}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Information("JWT Auth: Challenge issued for request to {Path}", context.Request.Path);

                // Handle the challenge to return a custom ProblemDetails response
                context.HandleResponse(); // Prevents the default challenge handling
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Authentication failed. Valid token is required to access this resource.",
                    Instance = context.Request.Path
                };

                // Log the details of the unauthorized access attempt
                var clientId = context.HttpContext.User.FindFirstValue("client_id") ?? 
                               context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? 
                               "unknown";
                Log.Warning("Unauthorized access attempt to {Path} by client {ClientId}. Reason: {Reason}", 
                            context.Request.Path, clientId, context.ErrorDescription ?? "No token or invalid token");

                var jsonPayload = JsonSerializer.Serialize(problemDetails);
                return context.Response.WriteAsync(jsonPayload);
            }
        };
    });

// Configure authorization policies using our policy class
builder.Services.AddAuthorization(AuthorizationPolicies.ConfigurePolicies);

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    var settings = builder.Configuration.GetSection("CurrencyApi:RateLimit");
    
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(ctx => 
        RateLimitPartition.GetTokenBucketLimiter(
            ctx.Connection.RemoteIpAddress ?? IPAddress.Loopback, // Fallback to Loopback IP if RemoteIpAddress is null
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = settings.GetValue<int>("TokenLimit", 100),
                ReplenishmentPeriod = TimeSpan.FromMinutes(settings.GetValue<int>("ReplenishmentPeriodMinutes", 1)),
                TokensPerPeriod = settings.GetValue<int>("TokensPerPeriod", 100),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
    
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.");
    };
});

// Configure OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddSource("CurrencyConverterApi")
            .AddSource("CurrencyConverterApi.FrankfurterProvider")
            .AddSource("CurrencyConverterApi.CacheService")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CurrencyConverterApi"))
            .AddHttpClientInstrumentation(options =>
            {
                // Track outgoing HTTP requests but with minimal verbosity
                options.RecordException = true;
                options.FilterHttpRequestMessage = (request) => 
                    request.RequestUri != null && 
                    !request.RequestUri.AbsolutePath.Contains("/swagger/");
            })
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                
                // Filter out swagger and other non-essential paths to reduce noise
                options.Filter = (context) => 
                    !context.Request.Path.StartsWithSegments("/swagger") &&
                    !context.Request.Path.StartsWithSegments("/favicon.ico");
                
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    // Only add essential information that won't duplicate what Serilog captures
                    activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress);
                    if (request.HttpContext.User.Identity?.IsAuthenticated == true)
                    {
                        var clientId = request.HttpContext.User.FindFirstValue("client_id") ??
                                      request.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!string.IsNullOrEmpty(clientId))
                        {
                            activity.SetTag("user.client_id", clientId);
                        }
                    }
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response_code", response.StatusCode);
                };
            })
    );

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency Converter API v1");
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.EnableDeepLinking();
        c.DisplayRequestDuration();
        
        // Add useful information about JWT token usage
        c.OAuthUsePkce();
        c.OAuthScopeSeparator(" ");
        c.DefaultModelExpandDepth(2);
        c.DefaultModelsExpandDepth(-1); // Hide the schemas section
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseGlobalExceptionMiddleware(); // Added global exception middleware here

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseSerilogRequestLogging(options =>
{
    // Configure to avoid duplicate logs by setting message template
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms [Client: {ClientId}]";
    
    options.GetLevel = (httpContext, elapsed, ex) => 
    {
        if (httpContext.Request.Path.StartsWithSegments("/swagger"))
            return Serilog.Events.LogEventLevel.Debug;
            
        return Serilog.Events.LogEventLevel.Information;
    };
    
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        // Add essential properties but avoid duplicating what's already captured
        diagnosticContext.Set("RequestHost", httpContext.Request.Host);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress);
        
        // Add authenticated user info when available
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var clientId = httpContext.User.FindFirstValue("client_id") ??
                          httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          "anonymous";
            diagnosticContext.Set("ClientId", clientId);
            
            // Add roles information for access tracking
            var roles = string.Join(",", httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value));
            if (!string.IsNullOrEmpty(roles))
            {
                diagnosticContext.Set("UserRoles", roles);
            }
        }
        else
        {
            diagnosticContext.Set("ClientId", "anonymous");
        }
    };
});

app.MapControllers();

await app.RunAsync();