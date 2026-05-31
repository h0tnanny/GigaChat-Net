using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat.Tests;

public class GigaChatProviderTests
{
    [Fact]
    public async Task FileHelpersDelegateToClient()
    {
        var (client, fake) = FakeGigaChatClient.Create();
        var provider = new GigaChatProvider(client);
        var uploaded = new UploadedFile
        {
            Id = "file-1",
            Object = "file",
            Bytes = 3,
            CreatedAt = 1,
            Filename = "a.txt",
            Purpose = "general"
        };

        fake.UploadFileAsyncHandler = (_, fileName, purpose, _) =>
        {
            Assert.Equal("a.txt", fileName);
            Assert.Equal("general", purpose);
            return Task.FromResult(uploaded);
        };
        fake.GetFilesAsyncHandler = _ => Task.FromResult(new UploadedFiles { Data = [uploaded] });
        fake.GetFileAsyncHandler = (id, _) =>
        {
            Assert.Equal("file-1", id);
            return Task.FromResult(uploaded);
        };
        fake.DeleteFileAsyncHandler = (id, _) => Task.FromResult(new DeletedFile
        {
            Id = id,
            Deleted = true
        });
        fake.GetImageAsyncHandler = (id, _) => Task.FromResult(new Image { Content = $"image:{id}" });

        await using var stream = new MemoryStream([1, 2, 3]);

        Assert.Equal(uploaded, await provider.UploadFileAsync(stream, "a.txt"));
        Assert.Single((await provider.GetFilesAsync()).Data);
        Assert.Equal(uploaded, await provider.GetFileAsync("file-1"));
        Assert.True((await provider.DeleteFileAsync("file-1")).Deleted);
        Assert.Equal("image:file-1", (await provider.GetImageAsync("file-1")).Content);
    }
}
