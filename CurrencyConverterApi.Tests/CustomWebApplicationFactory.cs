using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CurrencyConverterApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program> 
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); 

        builder.ConfigureAppConfiguration((context, conf) =>
        {
            var currentDir = Directory.GetCurrentDirectory();
            var testProjectDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
            var solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, ".."));
            var apiProjectDir = Path.Combine(solutionDir, "CurrencyConverterApi");

            if (!Directory.Exists(apiProjectDir) || !File.Exists(Path.Combine(apiProjectDir, "appsettings.json")))
            {
                var assemblyLocation = Path.GetDirectoryName(typeof(CustomWebApplicationFactory).Assembly.Location);
                if (assemblyLocation != null) {
                    testProjectDir = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", ".."));
                    solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, ".."));
                    apiProjectDir = Path.Combine(solutionDir, "CurrencyConverterApi");
                }
            }
            
            if (!Directory.Exists(apiProjectDir) || !File.Exists(Path.Combine(apiProjectDir, "appsettings.json")))
            {
                throw new FileNotFoundException($"Could not find the API project directory or appsettings.json. Current directory: {currentDir}. Attempted API project path: {apiProjectDir}. Assembly Location: {typeof(CustomWebApplicationFactory).Assembly.Location}");
            }

            conf.SetBasePath(apiProjectDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testProjectDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
        var solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, ".."));
        var apiProjectDir = Path.Combine(solutionDir, "CurrencyConverterApi");

        if (!Directory.Exists(apiProjectDir))
        {
            var assemblyLocation = Path.GetDirectoryName(typeof(CustomWebApplicationFactory).Assembly.Location);
            if (assemblyLocation != null) {
                testProjectDir = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", ".."));
                solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, ".."));
                apiProjectDir = Path.Combine(solutionDir, "CurrencyConverterApi");
            }
        }

        if (!Directory.Exists(apiProjectDir))
        {
            throw new FileNotFoundException($"Could not find the API project directory for content root. Current directory: {currentDir}. Attempted API project path: {apiProjectDir}. Assembly Location: {typeof(CustomWebApplicationFactory).Assembly.Location}");
        }
        
        builder.UseContentRoot(apiProjectDir);
        return base.CreateHost(builder);
    }
}
