using System.Text.Json;
using C_Refresh;

public static class DogEndpoints
{
    public static void MapDogEndpoints(this WebApplication app)
    {
        var dogs = app.MapGroup("/api/dogs").WithTags("Dogs");

        // The provider returns an image URL, not image bytes. Saving it locally is optional.
        dogs.MapGet("/external", async (IHttpClientFactory clients, CancellationToken cancellation) =>
        {
            try
            {
                var data = await clients.CreateClient("DogApi")
                    .GetFromJsonAsync<DogApiResponse>("breeds/image/random", cancellation);
                return data is { Status: "success" } && DogInput.IsImageUrl(data.Message)
                    ? Results.Ok(data)
                    : ProviderFailure();
            }
            catch (Exception error) when (error is HttpRequestException or JsonException
                || error is OperationCanceledException && !cancellation.IsCancellationRequested)
            {
                return ProviderFailure();
            }
        });

        dogs.MapGet("/", (DogStore store) => Results.Ok(store.All()));
        dogs.MapGet("/{id:int}", (int id, DogStore store) =>
            store.Get(id) is { } dog ? Results.Ok(dog) : Results.NotFound());
        dogs.MapPost("/", (DogInput input, DogStore store) =>
        {
            if (!input.IsValid()) return InvalidInput();
            var dog = store.Create(input);
            return Results.Created($"/api/dogs/{dog.Id}", dog);
        });
        dogs.MapPut("/{id:int}", (int id, DogInput input, DogStore store) =>
        {
            if (!input.IsValid()) return InvalidInput();
            return store.Update(id, input) is { } dog ? Results.Ok(dog) : Results.NotFound();
        });
        dogs.MapDelete("/{id:int}", (int id, DogStore store) =>
            store.Delete(id) ? Results.NoContent() : Results.NotFound());
    }

    private static IResult ProviderFailure() => Results.Problem(
        "The dog image provider is unavailable or returned invalid data.", statusCode: 502);

    private static IResult InvalidInput() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["dog"] = ["Name must not be blank; ImageUrl must be an absolute HTTP or HTTPS URL."]
    });
}
