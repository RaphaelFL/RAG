using Microsoft.Extensions.Configuration;

namespace Chatbot.Api.Bootstrap;

public static class OptionalLocalConfigurationLoader
{
    public static void AddOptionalLocalConfiguration(this WebApplicationBuilder builder)
    {
        var environmentName = builder.Environment.EnvironmentName;

        foreach (var directory in EnumerateConfigurationDirectories(builder.Environment.ContentRootPath))
        {
            AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, "appsettings.local.json"));
            AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, $"appsettings.{environmentName}.local.json"));
            AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, "secrets.json"));
        }
    }

    private static IEnumerable<string> EnumerateConfigurationDirectories(string contentRootPath)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new DirectoryInfo(contentRootPath);

        for (var depth = 0; current is not null && depth < 4; depth += 1)
        {
            if (visited.Add(current.FullName))
            {
                yield return current.FullName;
            }

            current = current.Parent;
        }
    }

    private static void AddJsonFileIfExists(ConfigurationManager configuration, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
    }
}