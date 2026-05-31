using GigaChat.Net;

namespace GigaChat.Net.Tests;

public class SettingsTests
{
    [Fact]
    public void DefaultValuesMatchPythonSettings()
    {
        WithClearedEnvironment(() =>
        {
            var settings = new Settings();

            Assert.Equal("https://gigachat.devices.sberbank.ru/api/v1", settings.BaseUrl);
            Assert.Equal("https://ngw.devices.sberbank.ru:9443/api/v2/oauth", settings.AuthUrl);
            Assert.Equal("GIGACHAT_API_PERS", settings.Scope);
            Assert.Equal(30.0, settings.Timeout);
            Assert.True(settings.VerifySslCerts);
            Assert.False(settings.AllowModelOverrideFromHeader);
            Assert.Equal(0, settings.MaxRetries);
            Assert.Equal(0.5, settings.RetryBackoffFactor);
            Assert.Equal([429, 500, 502, 503, 504], settings.RetryOnStatusCodes);
            Assert.Equal(60000, settings.TokenExpiryBufferMs);
        });
    }

    [Fact]
    public void EnvironmentVariablesAreReadAndParsed()
    {
        WithClearedEnvironment(() =>
        {
            Environment.SetEnvironmentVariable("GIGACHAT_CREDENTIALS", "env-credentials");
            Environment.SetEnvironmentVariable("GIGACHAT_MODEL", "env-model");
            Environment.SetEnvironmentVariable("GIGACHAT_MAX_RETRIES", "10");
            Environment.SetEnvironmentVariable("GIGACHAT_RETRY_ON_STATUS_CODES", "408,429,503");
            Environment.SetEnvironmentVariable("GIGACHAT_TOKEN_EXPIRY_BUFFER_MS", "5000");
            Environment.SetEnvironmentVariable("GIGACHAT_FLAGS", "flag-a, flag-b");
            Environment.SetEnvironmentVariable("GIGACHAT_VERIFY_SSL_CERTS", "no");
            Environment.SetEnvironmentVariable("GIGACHAT_ALLOW_MODEL_OVERRIDE_FROM_HEADER", "yes");
            Environment.SetEnvironmentVariable("GIGACHAT_TIMEOUT", "12.5");

            var settings = new Settings();

            Assert.Equal("env-credentials", settings.Credentials);
            Assert.Equal("env-model", settings.Model);
            Assert.Equal(10, settings.MaxRetries);
            Assert.Equal([408, 429, 503], settings.RetryOnStatusCodes);
            Assert.Equal(5000, settings.TokenExpiryBufferMs);
            Assert.Equal(["flag-a", "flag-b"], settings.Flags);
            Assert.False(settings.VerifySslCerts);
            Assert.True(settings.AllowModelOverrideFromHeader);
            Assert.Equal(12.5, settings.Timeout);
        });
    }

    private static void WithClearedEnvironment(Action test)
    {
        var keys = new[]
        {
            "GIGACHAT_BASE_URL",
            "GIGACHAT_AUTH_URL",
            "GIGACHAT_CREDENTIALS",
            "GIGACHAT_SCOPE",
            "GIGACHAT_ACCESS_TOKEN",
            "GIGACHAT_MODEL",
            "GIGACHAT_ALLOW_MODEL_OVERRIDE_FROM_HEADER",
            "GIGACHAT_PROFANITY_CHECK",
            "GIGACHAT_USER",
            "GIGACHAT_PASSWORD",
            "GIGACHAT_TIMEOUT",
            "GIGACHAT_VERIFY_SSL_CERTS",
            "GIGACHAT_MAX_CONNECTIONS",
            "GIGACHAT_MAX_RETRIES",
            "GIGACHAT_RETRY_BACKOFF_FACTOR",
            "GIGACHAT_RETRY_ON_STATUS_CODES",
            "GIGACHAT_TOKEN_EXPIRY_BUFFER_MS",
            "GIGACHAT_FLAGS"
        };
        var oldValues = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var key in keys)
                Environment.SetEnvironmentVariable(key, null);
            test();
        }
        finally
        {
            foreach (var (key, value) in oldValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
