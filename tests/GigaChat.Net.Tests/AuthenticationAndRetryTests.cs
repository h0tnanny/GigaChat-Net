using System.Net;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class AuthenticationAndRetryTests
{
    [Fact]
    public void OAuthCredentialsAreNotDoubleEncodedAndTokenIsUsed()
    {
        var apiHandler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        authHandler.QueueJson("""{"access_token":"oauth-token","expires_at":4102444800000}""");
        apiHandler.QueueJson(TestData.Fixture("models.json"));
        using var client = new GigaChatClient(
            new Settings { Credentials = "already-base64", Scope = "GIGACHAT_API_PERS" },
            apiHandler,
            authHandler);

        client.GetModels();

        Assert.Equal("Basic already-base64", authHandler.Requests[0].Authorization);
        Assert.Equal("scope=GIGACHAT_API_PERS", authHandler.Requests[0].Body);
        Assert.Equal("Bearer oauth-token", apiHandler.Requests[0].Authorization);
    }

    [Fact]
    public void PasswordAuthUsesTokenEndpointAndBasicUserPassword()
    {
        var apiHandler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        authHandler.QueueJson("""{"tok":"password-token","exp":4102444800000}""");
        apiHandler.QueueJson(TestData.Fixture("models.json"));
        using var client = new GigaChatClient(
            new Settings { User = "user", Password = "pass" },
            apiHandler,
            authHandler);

        client.GetModels();

        Assert.EndsWith("/token", authHandler.Requests[0].PathAndQuery);
        Assert.Equal($"Basic {Convert.ToBase64String("user:pass"u8.ToArray())}", authHandler.Requests[0].Authorization);
        Assert.Equal("Bearer password-token", apiHandler.Requests[0].Authorization);
    }

    [Fact]
    public void ManualAccessTokenSkipsAuthRequest()
    {
        var apiHandler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        apiHandler.QueueJson(TestData.Fixture("models.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "manual" }, apiHandler, authHandler);

        client.GetModels();

        Assert.Empty(authHandler.Requests);
        Assert.Equal("Bearer manual", apiHandler.Requests[0].Authorization);
    }

    [Fact]
    public async Task GetTokenAsyncReturnsManualAccessToken()
    {
        using var client = new GigaChatClient(new Settings { AccessToken = "manual" }, new RecordingHandler());

        var token = await client.GetTokenAsync();

        Assert.NotNull(token);
        Assert.Equal("manual", token.Token);
        Assert.Equal(0, token.ExpiresAt);
    }

    [Fact]
    public void AuthErrorsMapToTypedExceptionsBeforeApiRequest()
    {
        var apiHandler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        authHandler.QueueJson("""{"message":"bad credentials"}""", HttpStatusCode.BadRequest);
        using var client = new GigaChatClient(new Settings { Credentials = "bad" }, apiHandler, authHandler);

        Assert.Throws<BadRequestError>(() => client.GetModels());
        Assert.Empty(apiHandler.Requests);
    }

    [Fact]
    public void RetriesTransientStatusThenSucceeds()
    {
        var delays = new List<TimeSpan>();
        var handler = new RecordingHandler();
        handler.QueueJson("""{"error":"temporary"}""", HttpStatusCode.InternalServerError);
        handler.QueueJson(TestData.Fixture("models.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", MaxRetries = 1 },
            handler,
            delay: delays.Add,
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var models = client.GetModels();

        Assert.Single(models.Data);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(TimeSpan.FromSeconds(0.5), Assert.Single(delays));
    }

    [Fact]
    public void DoesNotRetryWhenStatusIsNotConfigured()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""{"message":"bad"}""", HttpStatusCode.BadRequest);
        using var client = new GigaChatClient(new Settings { AccessToken = "token", MaxRetries = 3 }, handler);

        Assert.Throws<BadRequestError>(() => client.GetModels());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void ResponseStatusesMapToTypedExceptions()
    {
        AssertException<BadRequestError>(HttpStatusCode.BadRequest);
        AssertException<AuthenticationError>(HttpStatusCode.Unauthorized);
        AssertException<ForbiddenError>(HttpStatusCode.Forbidden);
        AssertException<NotFoundError>(HttpStatusCode.NotFound);
        AssertException<RequestEntityTooLargeError>(HttpStatusCode.RequestEntityTooLarge);
        AssertException<UnprocessableEntityError>(HttpStatusCode.UnprocessableEntity);
        AssertException<RateLimitError>(HttpStatusCode.TooManyRequests);
        AssertException<ServerError>(HttpStatusCode.BadGateway);
    }

    private static void AssertException<TException>(HttpStatusCode statusCode)
        where TException : ResponseError
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""{"message":"error"}""", statusCode);
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);

        var exception = Assert.ThrowsAny<ResponseError>(() => client.GetModels());
        Assert.IsType<TException>(exception);
        Assert.NotNull(exception.Headers);
        Assert.Contains(((int)statusCode).ToString(), exception.ToString());
    }
}
