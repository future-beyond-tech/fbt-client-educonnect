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
using Microsoft.Extensions.Options;
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

builder.Services.AddSingleton<EduConnect.Api.Infrastructure.Database.Interceptors.AuditableEntityInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(databaseConnectionString);
    options.AddInterceptors(sp.GetRequiredService<EduConnect.Api.Infrastructure.Database.Interceptors.AuditableEntityInterceptor>());
});

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

// Web Push (VAPID). Overridable via env vars so secrets stay out of appsettings.
var webPushOptions = builder.Configuration.GetSection(WebPushOptions.SectionName).Get<WebPushOptions>() ?? new WebPushOptions();
webPushOptions.PublicKey = builder.Configuration["VAPID_PUBLIC_KEY"] ?? webPushOptions.PublicKey;
webPushOptions.PrivateKey = builder.Configuration["VAPID_PRIVATE_KEY"] ?? webPushOptions.PrivateKey;
webPushOptions.Subject = builder.Configuration["VAPID_SUBJECT"] ?? webPushOptions.Subject;
builder.Services.AddSingleton<IOptions<WebPushOptions>>(Options.Create(webPushOptions));

if (webPushOptions.Enabled &&
    !string.IsNullOrWhiteSpace(webPushOptions.PublicKey) &&
    !string.IsNullOrWhiteSpace(webPushOptions.PrivateKey))
{
    builder.Services.AddScoped<IPushSender, WebPushSender>();
}
else
{
    builder.Services.AddSingleton<IPushSender, NullPushSender>();
}

var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
storageOptions.BucketName =
    builder.Configuration["S3_BUCKET_NAME"] ??
    storageOptions.BucketName;
storageOptions.Region =
    builder.Configuration["AWS_REGION"] ??
    storageOptions.Region;
storageOptions.ServiceUrl =
    builder.Configuration["S3_SERVICE_URL"] ??
    storageOptions.ServiceUrl;

builder.Services.AddSingleton<IOptions<StorageOptions>>(Options.Create(storageOptions));

// Resend transactional email (used by forgot/reset password & PIN flows).
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// S3 storage for attachments (compatible with AWS S3, Cloudflare R2, MinIO)
if (!string.IsNullOrWhiteSpace(storageOptions.ServiceUrl))
{
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
    {
        var config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = storageOptions.ServiceUrl,
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
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(storageOptions.Region)
        };
        return new Amazon.S3.AmazonS3Client(config);
    });
}
builder.Services.AddScoped<IStorageService, S3StorageService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Apply pending EF Migrations on startup
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("EF Core Migrations applied successfully.");
        
        // Always run production seeding to ensure base setup
        await EduConnect.Api.Infrastructure.Database.DatabaseSeeder.SeedProductionDataAsync(dbContext, logger);

        if (app.Environment.IsDevelopment())
        {
            await EduConnect.Api.Infrastructure.Database.DatabaseSeeder.SeedDevelopmentDataAsync(dbContext, logger);
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to initialize database on startup.");
        throw;
    }
}

app.UseSecurityHeaders();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "openapi/{documentName}.json";
    });
}

if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    app.UseSentryTracing();
}

app.UseCorrelationId();
app.UseGlobalException();
app.UseCors("AllowConfigured");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseTenantIsolation();
app.UseMustChangePasswordEnforcement();
app.UseRequestLogging();

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
