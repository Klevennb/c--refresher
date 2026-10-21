using System.Text.Json;
using C_Refresh;

public static class PostEndpoints
{
    public static void MapPostEndpoints(this WebApplication app)
    {
        var posts = app.MapGroup("/api/posts").WithTags("Posts");

        // Fetch sample data without importing it into the local mock store.
        posts.MapGet("/external", async (IHttpClientFactory clients, CancellationToken cancellation) =>
        {
            try
            {
                var data = await clients.CreateClient("JsonPlaceholder")
                    .GetFromJsonAsync<Post[]>("posts", cancellation);
                return data is null
                    ? Results.Problem("The posts provider returned an empty response.", statusCode: 502)
                    : Results.Ok(data);
            }
            catch (Exception error) when (error is HttpRequestException or JsonException
                || error is OperationCanceledException && !cancellation.IsCancellationRequested)
            {
                return Results.Problem("The posts provider is unavailable or returned invalid data.", statusCode: 502);
            }
        });

        posts.MapGet("/", (PostStore store) => Results.Ok(store.All()));
        posts.MapGet("/{id:int}", (int id, PostStore store) =>
            store.Get(id) is { } post ? Results.Ok(post) : Results.NotFound());
        posts.MapPost("/", (PostInput input, PostStore store) =>
        {
            if (!input.IsValid()) return InvalidInput();
            var post = store.Create(input);
            return Results.Created($"/api/posts/{post.Id}", post);
        });
        posts.MapPut("/{id:int}", (int id, PostInput input, PostStore store) =>
        {
            if (!input.IsValid()) return InvalidInput();
            return store.Update(id, input) is { } post ? Results.Ok(post) : Results.NotFound();
        });
        posts.MapDelete("/{id:int}", (int id, PostStore store) =>
            store.Delete(id) ? Results.NoContent() : Results.NotFound());
    }

    private static IResult InvalidInput() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["post"] = ["UserId must be positive; Title and Body must not be blank."]
    });
}
