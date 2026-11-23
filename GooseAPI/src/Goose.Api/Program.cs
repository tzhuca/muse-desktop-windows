using Goose.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GooseDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Missing ConnectionStrings:GooseDb in configuration.");
}

builder.Services.AddDbContext<GooseDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Goose API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "GooseApi",
    health = "/api/health",
    docs = "/swagger",
    endpoints = new[] { "/api/conversations", "/api/settings" },
}));

app.MapControllers();

// Create or upgrade GooseDb on startup. Integration tests turn this off and
// build their own database from the model with EnsureCreated().
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<GooseDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("GooseDb schema is up to date");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "GooseDb migration failed; the API starts but data endpoints will fail");
    }
}

app.Run();
