using IdentityModel.AspNetCore.OAuth2Introspection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var configuration = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json")
           .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
           .Build();

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

    const string serviceName = "ReverseProxyExample";

    builder.Logging.AddOpenTelemetry(options =>
    {
        options
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName))
            .AddConsoleExporter();
    });

    builder.Services.AddOpenTelemetry()
          .ConfigureResource(resource => resource.AddService(serviceName))
          .WithTracing(tracing => tracing
              .AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation()
              .AddConsoleExporter()
              .AddZipkinExporter(opt => opt.Endpoint = new Uri("http://localhost:9411/api/v2/spans")))
          .WithMetrics(metrics => metrics
              .AddAspNetCoreInstrumentation()
              .AddConsoleExporter());

    Log.Information("starting server.");
    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddReverseProxy()
        // Initialize the reverse proxy from the "ReverseProxy" section of configuration
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.AddAuthentication(OAuth2IntrospectionDefaults.AuthenticationScheme)
    .AddOAuth2Introspection(options =>
    {
        options.Authority = "https://localhost:7130";
        options.ClientId = "ReverseProxy";
        options.ClientSecret = "ABC123";
    });

    builder.Services.AddAuthorization(options =>
    {
        // Creates a policy called "myPolicy" that depends on having a claim "myCustomClaim" with the value "green".
        // See AccountController.Login method for where this claim is applied to the user identity
        // This policy can then be used by routes in the proxy, see "ClaimsAuthRoute" in appsettings.json
        options.AddPolicy("Angular", builder => builder
            .RequireClaim("Role", "Admin")
            .RequireAuthenticatedUser());

        // The default policy is to require authentication, but no additional claims
        // Uncommenting the following would have no effect
        // options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        // FallbackPolicy is used for routes that do not specify a policy in config
        // Make all routes that do not specify a policy to be anonymous (this is the default).
        options.FallbackPolicy = null;
        // Or make all routes that do not specify a policy require some auth:
        // options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    });

    var app = builder.Build();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
    app.MapReverseProxy();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}