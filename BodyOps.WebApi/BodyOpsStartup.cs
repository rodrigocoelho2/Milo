using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Json;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features;
using BodyOps.WebApi.Features.Accounts.Repositories;
using BodyOps.WebApi.Features.Accounts.Services;
using BodyOps.WebApi.Features.Backups.Services;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Exercises.Services;
using BodyOps.WebApi.Features.Foods.Repositories;
using BodyOps.WebApi.Features.Foods.Services;
using BodyOps.WebApi.Features.Health.Services;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Push.Repositories;
using BodyOps.WebApi.Features.Push.Services;
using BodyOps.WebApi.Features.Watch.Repositories;
using BodyOps.WebApi.Features.Watch.Services;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Repositories;
using BodyOps.WebApi.Features.Weights.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;

namespace BodyOps.WebApi;

/// <summary>
/// Service registration and request pipeline of the WebApi.
/// </summary>
internal static class BodyOpsStartup
{
    /// <summary>
    /// Registers every service. Services and repositories are singletons; each repository is registered before its service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBodyOps(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var section = configuration.GetSection(BodyOpsOptions.SectionName);
        var settings = section.Get<BodyOpsOptions>() ?? new BodyOpsOptions();
        settings.ResolvePaths(environment.ContentRootPath);
        Directory.CreateDirectory(settings.DataDirectory);

        services.AddOptions<BodyOpsOptions>()
            .Bind(section)
            .PostConfigure(options => options.ResolvePaths(environment.ContentRootPath));

        services.AddControllers(static options => options.Filters.Add<AppHeaderFilter>())
            .ConfigureApplicationPartManager(static manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(static options => JsonSetup.Apply(options.JsonSerializerOptions));

        services.AddBodyOpsAuthentication(settings);
        services.AddProblemDetails();
        services.AddResponseCompression(static options => options.EnableForHttps = true);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(static options =>
        {
            options.EnableAnnotations();
            var xml = Path.Combine(AppContext.BaseDirectory, "BodyOps.WebApi.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }
        });

        // Infrastructure
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IGlobalizationHelper, GlobalizationHelper>();

        // Accounts
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<IUserAdminService, UserAdminService>();

        // Plans
        services.AddSingleton<IPlanRepository, PlanRepository>();
        services.AddSingleton<IPlanService, PlanService>();

        // Days
        services.AddSingleton<IDayRepository, DayRepository>();
        services.AddSingleton<IDayService, DayService>();

        // Exercises
        services.AddSingleton<IExerciseService, ExerciseService>();

        // Foods
        services.AddMemoryCache();
        services.AddSingleton<IFoodRepository, FoodRepository>();
        services.AddSingleton<IPackagedFoodSource, OpenFoodFactsSource>();
        services.AddSingleton<IFoodService, FoodService>();

        // Settings
        services.AddSingleton<ISettingRepository, SettingRepository>();
        services.AddSingleton<ISettingService, SettingService>();

        // Weights
        services.AddSingleton<IWeightRepository, WeightRepository>();
        services.AddSingleton<IWeightService, WeightService>();

        // Backups
        services.AddSingleton<IBackupService, BackupService>();

        // Health
        services.AddSingleton<IHealthService, HealthService>();

        // Push reminders
        services.AddSingleton<IPushRepository, PushRepository>();
        services.AddSingleton<IPushSender, PushSender>();
        services.AddSingleton<IPushService, PushService>();
        services.AddSingleton<ReminderWorker>();
        services.AddHostedService(static provider => provider.GetRequiredService<ReminderWorker>());

        // Apple Watch & Health (iOS Shortcut)
        services.AddSingleton<IWatchRepository, WatchRepository>();
        services.AddSingleton<IWatchService, WatchService>();

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseBodyOps(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BodyOpsOptions>>().Value;
        if (settings.BehindProxy)
        {
            var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            app.UseForwardedHeaders(forwarded);
        }

        app.UseExceptionHandler();
        app.UseResponseCompression();
        app.UseDefaultFiles();

        // .webmanifest is not in ASP.NET's default list of file types.
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            // Pages, scripts and styles are revalidated on every load (cheap 304s), so a new deploy shows up on the next launch.
            // Fonts and icons are kept a week: give an icon a new file name whenever its picture changes.
            OnPrepareResponse = static context =>
            {
                var name = context.File.Name;
                context.Context.Response.Headers.CacheControl =
                    name.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? "public, max-age=604800"
                        : "no-cache";
            },
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();

        // The page handles its own routes (/today, /week, /plan …): anything that isn't the API or a file gets the page.
        app.MapFallbackToFile("index.html");

        return app;
    }
}
