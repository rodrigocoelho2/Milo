using BodyOps.Library.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BodyOps.UnitTest.Integration;

/// <summary>The whole WebApi on its own throw-away database and data folder.</summary>
internal sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly bool _allowRegistration;
    private readonly TempFolder _data = TestSupport.NewFolder();
    private bool _disposed;

    public TestApp(bool allowRegistration = true) => _allowRegistration = allowRegistration;

    public TestDatabase Database { get; } = new();

    /// <summary>A client with its own cookie jar and the header the API requires on writes.</summary>
    public HttpClient NewClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(ApiHeaders.App, "1");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:BodyOps", Database.ConnectionString);
        builder.UseSetting("BodyOps:DataDirectory", _data.Path);
        builder.UseSetting("BodyOps:AllowRegistration", _allowRegistration ? "true" : "false");
        builder.UseSetting("BodyOps:AuthAttemptsPerMinute", "1000");
        builder.UseSetting("BodyOps:RemindersEnabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // WebApplicationFactory calls this from both Dispose and DisposeAsync.
        if (disposing && !_disposed)
        {
            _disposed = true;
            Database.Dispose();
            _data.Dispose();
        }
    }
}
