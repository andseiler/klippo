using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PiiGateway.Tests.Integration.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5433;Database=piigateway_test;Username=piigateway;Password=changeme_dev_only",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["FileStorage:BasePath"] = Path.Combine(Path.GetTempPath(), "piigateway_test_uploads_" + Guid.NewGuid().ToString("N")[..8]),
                ["FileStorage:MaxFileSizeMb"] = "50",
                ["Encryption:Key"] = "dGhpcyBpcyBhIDMyIGJ5dGUga2V5ISEhMTIzNDU2Nzg=",
            });
        });
    }
}
