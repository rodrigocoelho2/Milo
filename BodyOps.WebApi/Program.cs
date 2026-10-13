using BodyOps.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBodyOps(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseBodyOps();
await app.RunAsync();

/// <summary>
/// The entry point; public so the integration tests can host the app with WebApplicationFactory.
/// </summary>
public partial class Program
{
}
