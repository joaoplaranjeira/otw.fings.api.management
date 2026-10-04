using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Configuration;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Middleware;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

DotEnvLoader.LoadIfPresent();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection(OpenAiSettings.SectionName));
builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection(OtpSettings.SectionName));
builder.Services.AddOptions<HouseholdInvitationSettings>()
    .Bind(builder.Configuration.GetSection(HouseholdInvitationSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
        NameClaimType = "name",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required. Configure it through an environment variable or user-secrets.");
}
builder.Services.AddDbContext<FingsDbContext>(options => options.UseMySQL(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OTW Fings API - Management",
        Version = "v1",
        Description = "API de gestão financeira pessoal e familiar"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT obtido em POST /api/otp/validate",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<IHouseholdInvitationRepository, HouseholdInvitationRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IHouseholdInvitationService, HouseholdInvitationService>();
builder.Services.AddScoped<IReceiptParser, OpenAiReceiptParser>();
builder.Services.AddScoped<IReceiptParseReviewService, ReceiptParseReviewService>();
builder.Services.AddScoped<IRecurringExpenseMaterializer, RecurringExpenseMaterializer>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddHttpClient<IEmailService, LoopsEmailService>();
builder.Services.AddHostedService<RecurringExpenseWorker>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<FingsDbContext>().Database.MigrateAsync();
    return;
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

public partial class Program;
