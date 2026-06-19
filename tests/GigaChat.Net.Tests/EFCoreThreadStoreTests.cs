#if NET8_0_OR_GREATER

using GigaChat.Net.EFCore;
using GigaChat.Net.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class EFCoreThreadStoreTests
{
    private static IDbContextFactory<GigaChatDbContext> CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<GigaChatDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider()
                       .GetRequiredService<IDbContextFactory<GigaChatDbContext>>();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_MessageHistory()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        var thread = new GigaChatAgentThread
        {
            ThreadId = "t1",
            History =
            [
                new(AuthorRole.System, "You are helpful"),
                new(AuthorRole.User, "Hello"),
                new(AuthorRole.Assistant, "Hi there")
            ]
        };

        await store.SaveAsync(thread);
        var loaded = await store.LoadAsync("t1");

        Assert.NotNull(loaded);
        Assert.Equal("t1", loaded.ThreadId);
        Assert.Equal(3, loaded.History.Count);
        Assert.Equal("system", loaded.History[0].Role.Label);
        Assert.Equal("You are helpful", loaded.History[0].Content);
        Assert.Equal("Hello", loaded.History[1].Content);
        Assert.Equal("Hi there", loaded.History[2].Content);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_PendingToolCall()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        var pending = new GigaChatPendingToolCall
        {
            PluginName = "MyPlugin",
            FunctionName = "DoSomething",
            Arguments = new Dictionary<string, object?> { ["x"] = 42L }
        };
        var thread = new GigaChatAgentThread
        {
            ThreadId = "t2",
            PendingToolCall = pending
        };

        await store.SaveAsync(thread);
        var loaded = await store.LoadAsync("t2");

        Assert.NotNull(loaded?.PendingToolCall);
        Assert.Equal("MyPlugin", loaded.PendingToolCall.PluginName);
        Assert.Equal("DoSomething", loaded.PendingToolCall.FunctionName);
    }

    [Fact]
    public async Task SaveAndLoad_NoPendingToolCall_IsNull()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        await store.SaveAsync(new GigaChatAgentThread { ThreadId = "t3" });
        var loaded = await store.LoadAsync("t3");

        Assert.Null(loaded?.PendingToolCall);
    }

    [Fact]
    public async Task Load_UnknownThread_ReturnsNull()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        var result = await store.LoadAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesThread()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t4",
            History = [new(AuthorRole.User, "hello")]
        });
        await store.DeleteAsync("t4");
        Assert.Null(await store.LoadAsync("t4"));
    }

    [Fact]
    public async Task Delete_NonExistent_DoesNotThrow()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        await store.DeleteAsync("does-not-exist");
    }

    [Fact]
    public async Task Save_Twice_UpdatesExistingRecord()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t5",
            History = [new(AuthorRole.User, "first")]
        });
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t5",
            History = [new(AuthorRole.User, "first"), new(AuthorRole.Assistant, "second")]
        });

        var loaded = await store.LoadAsync("t5");
        Assert.Equal(2, loaded!.History.Count);
        Assert.Equal("second", loaded.History[1].Content);
    }

    [Fact]
    public async Task Save_ClearsPendingToolCall_OnSecondSave()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t6",
            PendingToolCall = new GigaChatPendingToolCall
            {
                PluginName = "P", FunctionName = "F",
                Arguments = new Dictionary<string, object?>()
            }
        });

        // Resume clears PendingToolCall
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t6",
            History = [new(AuthorRole.Assistant, "done")],
            PendingToolCall = null
        });

        var loaded = await store.LoadAsync("t6");
        Assert.Null(loaded?.PendingToolCall);
        Assert.Single(loaded!.History);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_AgentSteps()
    {
        var store = new EFCoreGigaChatAgentThreadStore<GigaChatDbContext>(CreateFactory());
        var steps = new List<GigaChatAgentStep>
        {
            new GigaChatToolCallStep
            {
                RequestIndex = 0, LatencyMs = 100,
                ToolName = "my_tool",
                Arguments = new Dictionary<string, object?> { ["key"] = "val" }
            },
            new GigaChatToolResultStep
            {
                RequestIndex = 0, LatencyMs = 50,
                ToolName = "my_tool", Result = "tool output"
            },
            new GigaChatAssistantMessageStep
            {
                RequestIndex = 0, LatencyMs = 200,
                Content = "final answer"
            }
        };

        await store.SaveAsync(new GigaChatAgentThread { ThreadId = "t7", Steps = steps });
        var loaded = await store.LoadAsync("t7");

        Assert.Equal(3, loaded!.Steps.Count);
        var call = Assert.IsType<GigaChatToolCallStep>(loaded.Steps[0]);
        Assert.Equal("my_tool", call.ToolName);
        var result = Assert.IsType<GigaChatToolResultStep>(loaded.Steps[1]);
        Assert.Equal("tool output", result.Result);
        var msg = Assert.IsType<GigaChatAssistantMessageStep>(loaded.Steps[2]);
        Assert.Equal("final answer", msg.Content);
    }
}

#endif
