using System.Net;
using System.Net.Http.Json;
using C_Refresh;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace C_Refresh.Tests;

public class PostEndpointTests
{
    [Fact]
    public async Task CrudPersistsChangesAndReturnsExpectedStatuses()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Empty((await client.GetFromJsonAsync<Post[]>("/api/posts"))!);

        var created = await client.PostAsJsonAsync("/api/posts", new PostInput(1, "Title", "Body"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = (await created.Content.ReadFromJsonAsync<Post>())!;
        Assert.Equal($"/api/posts/{post.Id}", created.Headers.Location!.ToString());
        Assert.Equal(post, await client.GetFromJsonAsync<Post>(created.Headers.Location));
        Assert.Single((await client.GetFromJsonAsync<Post[]>("/api/posts"))!);

        var updated = await client.PutAsJsonAsync(created.Headers.Location, new PostInput(2, "Edited", "Updated body"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(new Post(post.Id, 2, "Edited", "Updated body"),
            await client.GetFromJsonAsync<Post>(created.Headers.Location));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(created.Headers.Location, new PostInput(1, "Title", "Body"))).StatusCode);
    }

    [Theory]
    [InlineData(0, "Title", "Body")]
    [InlineData(1, " ", "Body")]
    [InlineData(1, "Title", null)]
    public async Task InvalidInputReturnsBadRequest(int userId, string? title, string? body)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var input = new PostInput(userId, title, body);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/posts", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/posts/1", input)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Post[]>("/api/posts"))!);
    }

    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("http-error", HttpStatusCode.BadGateway)]
    [InlineData("invalid-json", HttpStatusCode.BadGateway)]
    [InlineData("null", HttpStatusCode.BadGateway)]
    [InlineData("timeout", HttpStatusCode.BadGateway)]
    public async Task ExternalEndpointHandlesProviderResponses(string scenario, HttpStatusCode expected)
    {
        // Replace only the outbound transport; requests still exercise the real routes and serialization.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddHttpClient("JsonPlaceholder")
                .ConfigurePrimaryHttpMessageHandler(() => new FakeProvider(scenario))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/posts/external");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal(new Post(1, 1, "Sample", "Body"), Assert.Single((await response.Content.ReadFromJsonAsync<Post[]>())!));
            Assert.Empty((await client.GetFromJsonAsync<Post[]>("/api/posts"))!);
        }
    }

    private sealed class FakeProvider(string scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://jsonplaceholder.typicode.com/posts", request.RequestUri!.ToString());
            if (scenario == "timeout") throw new TaskCanceledException("Simulated timeout");
            return Task.FromResult(new HttpResponseMessage(scenario == "http-error" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(scenario switch
                {
                    "invalid-json" => "not json",
                    "null" => "null",
                    _ => """[{"id":1,"userId":1,"title":"Sample","body":"Body"}]"""
                }, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
