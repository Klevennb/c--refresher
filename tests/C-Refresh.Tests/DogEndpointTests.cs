using System.Net;
using System.Net.Http.Json;
using C_Refresh;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace C_Refresh.Tests;

public class DogEndpointTests
{
    [Fact]
    public async Task SavedDogsSupportFullCrudLifecycle()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Empty((await client.GetFromJsonAsync<Dog[]>("/api/dogs"))!);
        var created = await client.PostAsJsonAsync("/api/dogs", new DogInput("Buddy", "https://images.dog.ceo/buddy.jpg"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dog = (await created.Content.ReadFromJsonAsync<Dog>())!;
        Assert.Equal($"/api/dogs/{dog.Id}", created.Headers.Location!.ToString());
        Assert.Equal(dog, await client.GetFromJsonAsync<Dog>(created.Headers.Location));
        Assert.Single((await client.GetFromJsonAsync<Dog[]>("/api/dogs"))!);

        var edited = new DogInput("Max", "https://images.dog.ceo/max.jpg");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, edited)).StatusCode);
        Assert.Equal(new Dog(dog.Id, edited.Name!, edited.ImageUrl!), await client.GetFromJsonAsync<Dog>(created.Headers.Location));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(created.Headers.Location, edited)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Dog[]>("/api/dogs"))!);
    }

    [Theory]
    [InlineData(null, "https://example.com/dog.jpg")]
    [InlineData(" ", "https://example.com/dog.jpg")]
    [InlineData("Buddy", null)]
    [InlineData("Buddy", "/dog.jpg")]
    [InlineData("Buddy", "file:///dog.jpg")]
    public async Task InvalidInputsReturnBadRequest(string? name, string? imageUrl)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var input = new DogInput(name, imageUrl);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/dogs", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/dogs/1", input)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Dog[]>("/api/dogs"))!);
    }

    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("http-error", HttpStatusCode.BadGateway)]
    [InlineData("invalid-json", HttpStatusCode.BadGateway)]
    [InlineData("null", HttpStatusCode.BadGateway)]
    [InlineData("missing-fields", HttpStatusCode.BadGateway)]
    [InlineData("error-status", HttpStatusCode.BadGateway)]
    [InlineData("invalid-url", HttpStatusCode.BadGateway)]
    [InlineData("timeout", HttpStatusCode.BadGateway)]
    [InlineData("network-error", HttpStatusCode.BadGateway)]
    public async Task ExternalEndpointHandlesProviderResponses(string scenario, HttpStatusCode expected)
    {
        // Mock outbound HTTP only, keeping routing and JSON serialization under test.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddHttpClient("DogApi")
                .ConfigurePrimaryHttpMessageHandler(() => new FakeProvider(scenario))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/dogs/external");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
            Assert.Equal(new DogApiResponse("https://images.dog.ceo/sample.jpg", "success"),
                await response.Content.ReadFromJsonAsync<DogApiResponse>());
        Assert.Empty((await client.GetFromJsonAsync<Dog[]>("/api/dogs"))!);
    }

    private sealed class FakeProvider(string scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://dog.ceo/api/breeds/image/random", request.RequestUri!.ToString());
            if (scenario == "timeout") throw new TaskCanceledException("Simulated timeout");
            if (scenario == "network-error") throw new HttpRequestException("Simulated connection failure");
            return Task.FromResult(new HttpResponseMessage(scenario == "http-error" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(scenario switch
                {
                    "invalid-json" => "not json",
                    "null" => "null",
                    "missing-fields" => "{}",
                    "error-status" => """{"status":"error","message":"Unavailable"}""",
                    "invalid-url" => """{"status":"success","message":"not-a-url"}""",
                    _ => """{"status":"success","message":"https://images.dog.ceo/sample.jpg"}"""
                }, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
