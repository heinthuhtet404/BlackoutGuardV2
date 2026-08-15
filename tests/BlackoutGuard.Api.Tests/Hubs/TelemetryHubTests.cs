using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using BlackoutGuard.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BlackoutGuard.Api.Tests.Hubs;

public class TelemetryHubTests : IAsyncLifetime
{
    private const string SigningKey = "telemetry-hub-test-signing-key-0123456789abcdef";

    private WebApplication _app = null!;
    private HubConnection _facilityAClient = null!;
    private HubConnection _facilityBClient = null!;

    private readonly Guid _facilityA = Guid.NewGuid();
    private readonly Guid _facilityB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey))
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapHub<TelemetryHub>("/hubs/telemetry");

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var testServer = (TestServer)server;
        var baseUrl = testServer.BaseAddress.ToString();

        _facilityAClient = CreateClient(baseUrl, _facilityA);
        _facilityBClient = CreateClient(baseUrl, _facilityB);

        await _facilityAClient.StartAsync();
        await _facilityBClient.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _facilityAClient.DisposeAsync();
        await _facilityBClient.DisposeAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastTelemetry_ReachesFacilityA_ButNotFacilityB()
    {
        var facilityAReceived = new TaskCompletionSource<TelemetryPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var facilityBReceived = new TaskCompletionSource<TelemetryPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _facilityAClient.On<TelemetryPayload>(
            TelemetryHubMethods.TelemetryUpdated,
            payload => facilityAReceived.TrySetResult(payload));
        _facilityBClient.On<TelemetryPayload>(
            TelemetryHubMethods.TelemetryUpdated,
            payload => facilityBReceived.TrySetResult(payload));

        var hubContext = _app.Services.GetRequiredService<IHubContext<TelemetryHub>>();
        var payload = new TelemetryPayload(49.5, 230.0, 120.5, true);
        await hubContext.Clients
            .Group(_facilityA.ToString())
            .SendAsync(TelemetryHubMethods.TelemetryUpdated, payload);

        var received = await facilityAReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(49.5, received.Frequency);

        var bTask = await Task.WhenAny(
            facilityBReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(facilityBReceived.Task, bTask);
    }

    [Fact]
    public async Task BroadcastAlarm_IsFacilityIsolated()
    {
        var facilityAReceived = new TaskCompletionSource<AlarmPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var facilityBReceived = new TaskCompletionSource<AlarmPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _facilityAClient.On<AlarmPayload>(
            TelemetryHubMethods.AlarmRaised,
            payload => facilityAReceived.TrySetResult(payload));
        _facilityBClient.On<AlarmPayload>(
            TelemetryHubMethods.AlarmRaised,
            payload => facilityBReceived.TrySetResult(payload));

        var hubContext = _app.Services.GetRequiredService<IHubContext<TelemetryHub>>();
        var alarm = new AlarmPayload("FREQ_CRITICAL", "Critical", "Frequency below threshold");
        await hubContext.Clients
            .Group(_facilityA.ToString())
            .SendAsync(TelemetryHubMethods.AlarmRaised, alarm);

        var received = await facilityAReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("FREQ_CRITICAL", received.Code);

        var bTask = await Task.WhenAny(
            facilityBReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(facilityBReceived.Task, bTask);
    }

    [Fact]
    public async Task BroadcastDecision_IsFacilityIsolated()
    {
        var facilityAReceived = new TaskCompletionSource<DecisionPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var facilityBReceived = new TaskCompletionSource<DecisionPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _facilityAClient.On<DecisionPayload>(
            TelemetryHubMethods.DecisionExecuted,
            payload => facilityAReceived.TrySetResult(payload));
        _facilityBClient.On<DecisionPayload>(
            TelemetryHubMethods.DecisionExecuted,
            payload => facilityBReceived.TrySetResult(payload));

        var hubContext = _app.Services.GetRequiredService<IHubContext<TelemetryHub>>();
        var decision = new DecisionPayload(3, false, "Shedding load due to low frequency");
        await hubContext.Clients
            .Group(_facilityA.ToString())
            .SendAsync(TelemetryHubMethods.DecisionExecuted, decision);

        var received = await facilityAReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, received.RelayAddress);
        Assert.False(received.Energize);

        var bTask = await Task.WhenAny(
            facilityBReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(facilityBReceived.Task, bTask);
    }

    private HubConnection CreateClient(string baseUrl, Guid facilityId)
    {
        var token = CreateToken(facilityId);
        var testServer = (TestServer)_app.Services.GetRequiredService<IServer>();

        return new HubConnectionBuilder()
            .WithUrl($"{baseUrl}hubs/telemetry", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
            })
            .Build();
    }

    private static string CreateToken(Guid facilityId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("facility_id", facilityId.ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record TelemetryPayload(
        double Frequency,
        double Voltage,
        double TotalLoadKw,
        bool GeneratorOn);

    private sealed record AlarmPayload(string Code, string Severity, string Message);

    private sealed record DecisionPayload(int RelayAddress, bool Energize, string Rationale);
}
