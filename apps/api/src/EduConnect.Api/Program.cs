using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Behaviors;
using EduConnect.Api.Common.Extensions;
using EduConnect.Api.Common.Middleware;
using EduConnect.Api.Features.Health;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Expressions;
using Serilog.Formatting.Compact;
using System.Text;
using System.Threading.RateLimiting;

EduConnect.Api.Common.Extensions.ServiceCollectionExtensions.ValidateEnvironment();

var builder = WebApplication.CreateBuilder(args);

var sentryDsn = builder.Configuration["SENTRY_DSN"];

builder.Host.UseSerilog((ctx, lc) =>
{
    var destructuringPolicy = new DestructuringPolicy();
    lc.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            formatter: new RenderedCompactJsonFormatter(),
            path: "logs/educonnect-.json",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 50_000_000)
        .Destructure.With(destructuringPolicy)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EduConnect.Api")
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName);

    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        lc.WriteTo.Sentry(o =>
        {
            o.Dsn = sentryDsn;
            o.MinimumBreadcrumbLevel = LogEventLevel.Information;
            o.MinimumEventLevel = LogEventLevel.Error;
        });
    }
});

var databaseUrl = builder.Configuration["DATABASE_URL"];
var jwtSecret = builder.Configuration["JWT_SECRET"];
var jwtIssuer = builder.Configuration["JWT_ISSUER"];
var jwtAudience = builder.Configuration["JWT_AUDIENCE"];
var databaseConnectionString = DatabaseConnectionStringResolver.Resolve(databaseUrl);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<ResetTokenService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Resend transactional email (used by forgot/reset password & PIN flows).
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// S3 storage for attachments (compatible with AWS S3, Cloudflare R2, MinIO)
var s3ServiceUrl = builder.Configuration["S3_SERVICE_URL"];
if (!string.IsNullOrWhiteSpace(s3ServiceUrl))
{
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
    {
        var config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = s3ServiceUrl,
            ForcePathStyle = true
        };
        return new Amazon.S3.AmazonS3Client(
            builder.Configuration["S3_ACCESS_KEY"] ?? "",
            builder.Configuration["S3_SECRET_KEY"] ?? "",
            config);
    });
}
else
{
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
    {
        var config = new Amazon.S3.AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                builder.Configuration["AWS_REGION"] ?? "ap-south-1")
        };
        return new Amazon.S3.AmazonS3Client(config);
    });
}
builder.Services.AddScoped<IStorageService, S3StorageService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.TracesSampleRate = 0.2;
        o.Environment = builder.Environment.EnvironmentName;
        o.SendDefaultPii = false;
    });
}

var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]?.Split(',') ?? Array.Empty<string>();
var rateLimitPerUserPerMinute = builder.Configuration.GetValue<int?>("RATE_LIMIT_API_PER_USER_PER_MINUTE") ?? 60;
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfigured", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var key = context.User.FindFirst("userId")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitPerUserPerMinute,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

var app = builder.Build();

app.UseRouting();

if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    app.UseSentryTracing();
}

app.UseCorrelationId();
app.UseRequestLogging();
app.UseGlobalException();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseTenantIsolation();

app.UseCors("AllowConfigured");

app.MapAllEndpoints();

app.MapHealthEndpoints();

await app.RunAsync();

internal class DestructuringPolicy : IDestructuringPolicy
{
    private static readonly string[] SensitiveFields =
    {
        "password", "passwordhash", "pinhash", "secret", "token", "pin", "phone",
        "jwttoken", "accesstoken", "refreshtoken", "jwt_secret", "api_key"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory factory, out LogEventPropertyValue result)
    {
        result = null!;

        if (value == null)
            return false;

        var props = value.GetType().GetProperties();
        if (props.Length == 0)
            return false;

        var redacted = new Dictionary<string, LogEventPropertyValue>();
        foreach (var prop in props)
        {
            var propName = prop.Name.ToLowerInvariant();
            if (SensitiveFields.Any(sf => propName.Contains(sf)))
            {
                redacted[prop.Name] = factory.CreatePropertyValue("[REDACTED]");
            }
            else
            {
                var propValue = prop.GetValue(value);
                redacted[prop.Name] = factory.CreatePropertyValue(propValue);
            }
        }

        result = factory.CreatePropertyValue(redacted);
        return true;
    }
}
