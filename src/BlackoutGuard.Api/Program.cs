using System.Text;
using BlackoutGuard.Api.Engine;
using BlackoutGuard.Api.Hubs;
using BlackoutGuard.Api.Middleware;
using BlackoutGuard.Api.Services;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;
using BlackoutGuard.Application.UseCases.Rules;
using BlackoutGuard.Application.UseCases.Schedules;
using BlackoutGuard.Application.UseCases.Zones;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=blackoutguard_v2;Username=postgres;Password=postgres";

builder.Services.AddDbContext<BlackoutGuardDbContext>(options =>
    options.UseNpgsql(connectionString)
           .AddInterceptors(new FacilityIdDbInterceptor()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ✅ SignalR အတွက် CORS ကို ပြင်ပါ
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "dev-only-signing-key-change-in-production-0123456789";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BlackoutGuard";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BlackoutGuard.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // ✅ SignalR အတွက် JWT ကို Query String ကနေ ဖတ်ပါ
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/telemetry"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
builder.Services.AddScoped<ILoadRepository, LoadRepository>();
builder.Services.AddScoped<IFacilityRepository, FacilityRepository>();
builder.Services.AddScoped<IDecisionAuditLogRepository, DecisionAuditLogRepository>();
builder.Services.AddScoped<IRuleRepository, RuleRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditExportRepository, AuditExportRepository>();
builder.Services.AddScoped<IDbTransactionFactory, DbTransactionFactory>();
builder.Services.AddScoped<IExecutionStrategy, ExecutionStrategy>();

builder.Services.AddScoped<ListZonesUseCase>();
builder.Services.AddScoped<CreateZoneUseCase>();
builder.Services.AddScoped<UpdateZoneUseCase>();
builder.Services.AddScoped<DeleteZoneUseCase>();
builder.Services.AddScoped<ListLoadsUseCase>();
builder.Services.AddScoped<CreateLoadUseCase>();
builder.Services.AddScoped<UpdateLoadUseCase>();
builder.Services.AddScoped<DeleteLoadUseCase>();
builder.Services.AddScoped<ScoreCriticalityUseCase>();
builder.Services.AddScoped<ListRulesUseCase>();
builder.Services.AddScoped<UpdateRuleUseCase>();
builder.Services.AddScoped<ListSchedulesUseCase>();
builder.Services.AddScoped<CreateScheduleUseCase>();
builder.Services.AddScoped<DeleteScheduleUseCase>();

builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<ITelemetryBroadcaster, SignalRTelemetryBroadcaster>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlackoutGuardDbContext>();

    await EnsureDatabaseCreatedAsync(connectionString);
    await db.Database.MigrateAsync();

    await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
    await connection.OpenAsync();
    await RlsScriptRunner.ApplyAsync(connection);

    await DataSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.UseAuthentication();
app.UseMiddleware<FacilityContextMiddleware>();
app.UseAuthorization();

app.UseMiddleware<FacilityIdMiddleware>();

app.MapControllers();

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.MapGet("/api/health", () => Results.Ok("Healthy"))
   .WithName("HealthCheck");

app.Run();

static async Task EnsureDatabaseCreatedAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var databaseName = builder.Database;
    builder.Database = "postgres";

    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    await using var checkCommand = new NpgsqlCommand(
        "SELECT 1 FROM pg_database WHERE datname = @name",
        connection);
    checkCommand.Parameters.AddWithValue("name", databaseName);

    var exists = await checkCommand.ExecuteScalarAsync() is not null;
    if (exists)
        return;

    await using var createCommand = new NpgsqlCommand(
        $"CREATE DATABASE \"{databaseName}\"",
        connection);
    await createCommand.ExecuteNonQueryAsync();
}