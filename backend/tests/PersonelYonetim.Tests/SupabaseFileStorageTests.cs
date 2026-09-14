using System.Net;
using Microsoft.Extensions.Options;
using PersonelYonetim.Infrastructure.Files;

namespace PersonelYonetim.Tests;

public class SupabaseFileStorageTests
{
    [Fact]
    public async Task Stores_and_reads_private_file_and_deletes_it()
    {
        var objects = new Dictionary<string, byte[]>();
        var handler = new TestHandler(async request =>
        {
            Assert.Equal("sb_secret_test", request.Headers.GetValues("apikey").Single());
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("project.supabase.co", request.RequestUri!.Host);
            var path = request.RequestUri.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                Assert.StartsWith("/storage/v1/object/private-docs/", path);
                objects[path] = await request.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (request.Method == HttpMethod.Get)
            {
                Assert.StartsWith("/storage/v1/object/private-docs/", path);
                return objects.TryGetValue(path, out var content)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            Assert.Equal(HttpMethod.Delete, request.Method);
            return new HttpResponseMessage(objects.Remove(path) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        });
        var storage = CreateStorage(handler);
        var pdf = "%PDF-1.7\nexample"u8.ToArray();
        var saved = await storage.SaveAsync(new MemoryStream(pdf), "document.pdf", "application/pdf", "staff/42");
        Assert.StartsWith("staff/42/", saved.RelativePath);
        Assert.Equal(pdf.Length, saved.SizeBytes);
        await using (var opened = await storage.OpenReadAsync(saved.RelativePath))
        {
            Assert.NotNull(opened);
            using var result = new MemoryStream();
            await opened.Stream.CopyToAsync(result);
            Assert.Equal(pdf, result.ToArray());
        }
        await storage.DeleteIfExistsAsync(saved.RelativePath);
        Assert.Null(await storage.OpenReadAsync(saved.RelativePath));
    }

    [Fact]
    public async Task Rejects_traversal_and_spoofed_file_before_network()
    {
        var handler = new TestHandler(_ => throw new Exception("Network request must not happen"));
        var storage = CreateStorage(handler);
        Assert.Null(await storage.OpenReadAsync("../other/file.pdf"));
        await storage.DeleteIfExistsAsync("../other/file.pdf");
        await Assert.ThrowsAsync<PersonelYonetim.Application.Common.Exceptions.ValidationException>(() =>
            storage.SaveAsync(new MemoryStream("not a pdf"u8.ToArray()), "document.pdf", "application/pdf", "staff/42"));
    }

    private static SupabaseFileStorageService CreateStorage(HttpMessageHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new SupabaseStorageOptions
            {
                Url = "https://project.supabase.co",
                SecretKey = "sb_secret_test",
                Bucket = "private-docs"
            }),
            Options.Create(new FileStorageOptions()));

    private sealed class TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }
}
