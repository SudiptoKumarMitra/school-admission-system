using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Http;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Options are resolved lazily from IConfiguration so test hosts can override settings.
builder.Services.AddSingleton(sp => AppOptionsLoader.Load(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddDbContext<AdmissionDbContext>((sp, options) =>
    options.UseNpgsql(
        sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is not set.")));

builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<HoldService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter()));

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<AppOptions>((cors, app) =>
    cors.AddDefaultPolicy(policy =>
    {
        if (app.CorsAllowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(app.CorsAllowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    }));

var app = builder.Build();

// Fail fast on invalid configuration (for example a bad HOLD_DURATION_SECONDS).
_ = app.Services.GetRequiredService<AppOptions>();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    logger.LogError(exception, "Unhandled exception");

    var error = new ApiError(
        StatusCodes.Status500InternalServerError,
        ErrorCodes.InternalError,
        "Server error",
        "Something went wrong on the server. Please try again.");
    await error.ToResult().ExecuteAsync(context);
}));

app.UseCors();

app.MapHealthEndpoints();
app.MapClassEndpoints();

app.MapFallback(() => ProblemResults.NotFound(ErrorCodes.NotFound, "The requested resource was not found.").ToResult());

await DatabaseStartup.WaitAndMigrateAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Run();

public partial class Program;
