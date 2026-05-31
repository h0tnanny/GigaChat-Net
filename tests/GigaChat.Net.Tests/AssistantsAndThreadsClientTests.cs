using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class AssistantsAndThreadsClientTests
{
    [Fact]
    public void AssistantsEndpointsUsePythonContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("assistants", "get_assistants.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistants.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_modify.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_delete.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_files_delete.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var assistants = client.GetAssistants("111");
        var created = client.CreateAssistant(new CreateAssistantRequest
        {
            Model = "GigaChat-Pro",
            Name = "assistant",
            Instructions = "help",
            FileIds = ["222"],
            Metadata = new Dictionary<string, object?> { ["test"] = 234 }
        });
        var updated = client.UpdateAssistant(new UpdateAssistantRequest
        {
            AssistantId = "111",
            Name = "updated"
        });
        var deleted = client.DeleteAssistant("111");
        var fileDeleted = client.DeleteAssistantFile("111", "222");

        Assert.Equal(2, assistants.Data.Count);
        Assert.Equal("111", created.AssistantId);
        Assert.Equal("111", updated.AssistantId);
        Assert.True(deleted.Deleted);
        Assert.True(fileDeleted.Deleted);

        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Get, "/assistants?assistant_id=111"),
            request =>
            {
                AssertRequest(request, HttpMethod.Post, "/assistants");
                using var body = JsonDocument.Parse(request.Body!);
                Assert.Equal("GigaChat-Pro", body.RootElement.GetProperty("model").GetString());
                Assert.Equal("222", body.RootElement.GetProperty("file_ids")[0].GetString());
            },
            request => AssertRequest(request, HttpMethod.Post, "/assistants/modify"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants/delete"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants/files/delete"));
    }

    [Fact]
    public async Task AssistantsAsyncEndpointsUsePythonContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("assistants", "get_assistants.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistants.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_modify.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_delete.json"));
        handler.QueueJson(TestData.Fixture("assistants", "post_assistant_files_delete.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        await client.GetAssistantsAsync();
        await client.CreateAssistantAsync(new CreateAssistantRequest { Model = "GigaChat-Pro", Name = "assistant" });
        await client.UpdateAssistantAsync(new UpdateAssistantRequest { AssistantId = "111", Name = "assistant" });
        await client.DeleteAssistantAsync("111");
        await client.DeleteAssistantFileAsync("111", "222");

        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Get, "/assistants"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants/modify"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants/delete"),
            request => AssertRequest(request, HttpMethod.Post, "/assistants/files/delete"));
    }

    [Fact]
    public void ThreadsEndpointsUsePythonContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("threads", "get_threads.json"));
        handler.QueueJson(TestData.Fixture("threads", "get_threads.json"));
        handler.QueueJson("""
            {
              "id": "thread-1",
              "model": "GigaChat-Pro",
              "created_at": 1,
              "updated_at": 1,
              "run_lock": false,
              "status": "ready"
            }
            """);
        handler.QueueJson(TestData.Fixture("threads", "post_threads_retrieve.json"));
        handler.QueueText("", "application/json");
        handler.QueueJson(TestData.Fixture("threads", "get_threads_messages.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_threads_messages.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_threads_messages.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_threads_run.json"));
        handler.QueueJson(TestData.Fixture("threads", "get_threads_run.json"));
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_run.stream"), "text/event-stream");
        handler.QueueJson(TestData.Fixture("threads", "post_thread_messages_run.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_thread_messages_rerun.json"));
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_run.stream"), "text/event-stream");
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_rerun.stream"), "text/event-stream");
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var threads = client.GetThreads(["a", "b"], 2, 3);
        var listed = client.ListThreads();
        var threadId = client.CreateThread();
        var retrieved = client.RetrieveThreads(["thread-1"]);
        var deleted = client.DeleteThread("thread-1");
        var messages = client.GetThreadMessages("thread-1", 1, 2);
        var addedOne = client.AddThreadMessage("thread-1", "hello");
        var addedMany = client.AddThreadMessages(null, [new Messages { Role = MessagesRole.User, Content = "hello" }]);
        var run = client.RunThread("thread-1", "assistant-1", new ThreadRunOptions { MaxTokens = 10 });
        var runResult = client.GetThreadRun("thread-1");
        var runStream = client.RunThreadStream("thread-1").ToList();
        var completion = client.RunThreadMessages([new Messages { Role = MessagesRole.User, Content = "hello" }], model: "GigaChat-Pro");
        var rerun = client.RerunThreadMessages("thread-1");
        var messagesStream = client.RunThreadMessagesStream([new Messages { Role = MessagesRole.User, Content = "hello" }]).ToList();
        var rerunStream = client.RerunThreadMessagesStream("thread-1").ToList();

        Assert.Equal(3, threads.Items.Count);
        Assert.Equal(3, listed.Items.Count);
        Assert.Equal("thread-1", threadId);
        Assert.Single(retrieved.Items);
        Assert.True(deleted);
        Assert.Equal("fc4a58e8-1410-4684-b473-066eabeca000", messages.ThreadId);
        Assert.Single(addedOne.Messages);
        Assert.Single(addedMany.Messages);
        Assert.Equal(ThreadStatus.InProgress, run.Status);
        Assert.Equal(ThreadStatus.Ready, runResult.Status);
        Assert.Equal(2, runStream.Count);
        Assert.Equal("threads.messages.run", completion.Object);
        Assert.Equal("threads.messages.run", rerun.Object);
        Assert.Equal(2, messagesStream.Count);
        Assert.NotEmpty(rerunStream);

        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Get, "/threads?assistants_ids=a&assistants_ids=b&limit=2&before=3"),
            request => AssertRequest(request, HttpMethod.Get, "/threads"),
            request => AssertRequest(request, HttpMethod.Post, "/threads"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/retrieve"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/delete"),
            request => AssertRequest(request, HttpMethod.Get, "/threads/messages?thread_id=thread-1&limit=1&before=2"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages"),
            request =>
            {
                AssertRequest(request, HttpMethod.Post, "/threads/run");
                using var body = JsonDocument.Parse(request.Body!);
                Assert.Equal("thread-1", body.RootElement.GetProperty("thread_id").GetString());
                Assert.Equal("assistant-1", body.RootElement.GetProperty("assistant_id").GetString());
                Assert.Equal(10, body.RootElement.GetProperty("max_tokens").GetInt32());
            },
            request => AssertRequest(request, HttpMethod.Get, "/threads/run?thread_id=thread-1"),
            request =>
            {
                AssertRequest(request, HttpMethod.Post, "/threads/run");
                using var body = JsonDocument.Parse(request.Body!);
                Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
            },
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/run"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/rerun"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/run"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/rerun"));
    }

    [Fact]
    public async Task ThreadsAsyncEndpointsUsePythonContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("threads", "get_threads.json"));
        handler.QueueJson(TestData.Fixture("threads", "get_threads.json"));
        handler.QueueJson("""
            {
              "id": "thread-1",
              "model": "GigaChat-Pro",
              "created_at": 1,
              "updated_at": 1,
              "run_lock": false,
              "status": "ready"
            }
            """);
        handler.QueueJson(TestData.Fixture("threads", "post_threads_retrieve.json"));
        handler.QueueText("", "application/json");
        handler.QueueJson(TestData.Fixture("threads", "get_threads_messages.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_threads_messages.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_threads_run.json"));
        handler.QueueJson(TestData.Fixture("threads", "get_threads_run.json"));
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_run.stream"), "text/event-stream");
        handler.QueueJson(TestData.Fixture("threads", "post_thread_messages_run.json"));
        handler.QueueJson(TestData.Fixture("threads", "post_thread_messages_rerun.json"));
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_run.stream"), "text/event-stream");
        handler.QueueText(TestData.Fixture("threads", "post_thread_messages_rerun.stream"), "text/event-stream");
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        await client.GetThreadsAsync();
        await client.ListThreadsAsync();
        await client.CreateThreadAsync();
        await client.RetrieveThreadsAsync(["thread-1"]);
        await client.DeleteThreadAsync("thread-1");
        await client.GetThreadMessagesAsync("thread-1");
        await client.AddThreadMessagesAsync("thread-1", [new Messages { Role = MessagesRole.User, Content = "hello" }]);
        await client.RunThreadAsync("thread-1");
        await client.GetThreadRunAsync("thread-1");
        var chunks = new List<ThreadCompletionChunk>();
        await foreach (var chunk in client.RunThreadStreamAsync("thread-1"))
            chunks.Add(chunk);
        await client.RunThreadMessagesAsync([new Messages { Role = MessagesRole.User, Content = "hello" }]);
        await client.RerunThreadMessagesAsync("thread-1");
        await foreach (var chunk in client.RunThreadMessagesStreamAsync([new Messages { Role = MessagesRole.User, Content = "hello" }]))
            chunks.Add(chunk);
        await foreach (var chunk in client.RerunThreadMessagesStreamAsync("thread-1"))
            chunks.Add(chunk);

        Assert.True(chunks.Count > 4);
        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Get, "/threads"),
            request => AssertRequest(request, HttpMethod.Get, "/threads"),
            request => AssertRequest(request, HttpMethod.Post, "/threads"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/retrieve"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/delete"),
            request => AssertRequest(request, HttpMethod.Get, "/threads/messages?thread_id=thread-1"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/run"),
            request => AssertRequest(request, HttpMethod.Get, "/threads/run?thread_id=thread-1"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/run"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/run"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/rerun"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/run"),
            request => AssertRequest(request, HttpMethod.Post, "/threads/messages/rerun"));
    }

    private static void AssertRequest(RecordedRequest request, HttpMethod method, string pathAndQuery)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal($"/api/v1{pathAndQuery}", request.PathAndQuery);
    }
}
