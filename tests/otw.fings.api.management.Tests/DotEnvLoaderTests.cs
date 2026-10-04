using otw.fings.api.management.Infrastructure.Configuration;

namespace otw.fings.api.management.Tests;

public sealed class DotEnvLoaderTests
{
    [Fact]
    public void LoadIfPresent_LoadsValuesAndDoesNotOverrideEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fings-dotenv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var loadedKey = $"FINGS_TEST_LOADED_{Guid.NewGuid():N}";
        var preservedKey = $"FINGS_TEST_PRESERVED_{Guid.NewGuid():N}";
        try
        {
            File.WriteAllLines(Path.Combine(directory, ".env"),
            [
                "# comment",
                $"{loadedKey}=\"server=db.example;port=3306\"",
                $"export {preservedKey}=from-file"
            ]);
            Environment.SetEnvironmentVariable(preservedKey, "from-environment");

            DotEnvLoader.LoadIfPresent(directory);

            Assert.Equal("server=db.example;port=3306", Environment.GetEnvironmentVariable(loadedKey));
            Assert.Equal("from-environment", Environment.GetEnvironmentVariable(preservedKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(loadedKey, null);
            Environment.SetEnvironmentVariable(preservedKey, null);
            Directory.Delete(directory, recursive: true);
        }
    }
}
