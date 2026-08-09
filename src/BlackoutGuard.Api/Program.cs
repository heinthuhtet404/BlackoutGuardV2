var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/api/health");

app.MapGet("/api/health", () => Results.Ok("Healthy"))
   .WithName("HealthCheck");

app.Run();
