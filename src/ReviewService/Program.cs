using System.Text.Json.Serialization;
using Horica.ReviewService.Api;
using Horica.ReviewService.Application;
using Horica.ReviewService.Auth;
using Horica.ReviewService.Infrastructure.Messaging;
using Horica.ReviewService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddReviewPersistence(builder.Configuration);
builder.Services.AddReviewAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddReviewMessaging(builder.Configuration);

builder.Services.AddScoped<ReviewTaskService>();
builder.Services.AddScoped<ReviewRequestedHandler>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

builder.Services.AddReviewSwagger();

var app = builder.Build();

// Creates the schema (and waits for PostgreSQL) before any background worker starts.
await DatabaseInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();

var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: the process is up. Readiness: the database is reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

// Makes the entry point visible to integration tests (WebApplicationFactory<Program>).
public partial class Program
{
}
