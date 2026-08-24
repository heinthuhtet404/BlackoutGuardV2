using System.Text;
using BlackoutGuard.Api.Engine;
using BlackoutGuard.Api.Hubs;
using BlackoutGuard.Api.Middleware;
using BlackoutGuard.Api.Services;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;
using BlackoutGuard.Application.UseCases.Rules;
using BlackoutGuard.Application.UseCases.Schedules;
using BlackoutGuard.Application.UseCases.Users;
using BlackoutGuard.Application.UseCases.Zones;
using BlackoutGuard.Domain.BusinessRules;
using BlackoutGuard.Domain.Services;
using BlackoutGuard.Infrastructure.Engine;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Repositories;
using BlackoutGuard.Infrastructure.Simulation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=blackoutguard_v2;Username=postgres;Password=postgres";

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<BlackoutGuardDbContext>(options =>
        options.UseNpgsql(connectionString)
               .AddInterceptors(new FacilityIdDbInterceptor()));
}

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
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Repositories
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

// Password Hasher
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

// Domain Services & Engine
builder.Services.AddSingleton<IDecisionStrategy, PriorityBasedLoadSheddingStrategy>();
builder.Services.AddSingleton<IAlarmGenerator, AlarmRuleEngine>();

// Hosted Services
builder.Services.AddSingleton<PendingConfigChangeQueue>();
builder.Services.AddHostedService<EngineBackgroundService>();
builder.Services.AddHostedService<ScheduleEvaluationBackgroundService>();

// Use Cases
builder.Services.AddScoped<ListZonesUseCase>();
builder.Services.AddScoped<CreateZoneUseCase>();
builder.Services.AddScoped<UpdateZoneUseCase>();
builder.Services.AddScoped<DeleteZoneUseCase>();
builder.Services.AddScoped<GetZoneUseCase>();
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
builder.Services.AddScoped<ListUsersUseCase>();
builder.Services.AddScoped<CreateUserUseCase>();
builder.Services.AddScoped<UpdateUserUseCase>();
builder.Services.AddScoped<DeleteUserUseCase>();

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<SimulatorDataSource>();
builder.Services.AddSingleton<IDataSource>(sp => sp.GetRequiredService<SimulatorDataSource>());

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITelemetryBroadcaster, SignalRTelemetryBroadcaster>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlackoutGuardDbContext>();

    if (db.Database.IsRelational())
    {
        await EnsureDatabaseCreatedAsync(connectionString);
        await db.Database.MigrateAsync();

        if (db.Database.GetDbConnection() is NpgsqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            await RlsScriptRunner.ApplyAsync(connection);
        }
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    await DataSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FacilityContextMiddleware>();
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

    // CS8604 Warning Fix: databaseName Null ဖြစ်နိုင်ခြေရှိသဖြင့် fallback value ဖြည့်ဆည်းခြင်း
    checkCommand.Parameters.AddWithValue("name", databaseName ?? string.Empty);

    var exists = await checkCommand.ExecuteScalarAsync() is not null;
    if (exists)
        return;

    await using var createCommand = new NpgsqlCommand(
        $"CREATE DATABASE \"{databaseName}\"",
        connection);
    await createCommand.ExecuteNonQueryAsync();
}

public partial class Program { }