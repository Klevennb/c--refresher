# C-Refresh

Run the .NET 10 API with `dotnet run`. Use the URL printed at startup.

`GET /api/posts/external` fetches posts from the free, keyless API at
https://jsonplaceholder.typicode.com/posts. Provider failures return HTTP 502.

Mock CRUD uses a separate in-memory store, initially empty. Data is shared across
requests and cleared when the application restarts; writes do not go to the provider.

| Method | Route | Behavior |
| --- | --- | --- |
| GET | /api/posts | List local posts |
| GET | /api/posts/{id} | Read a local post; 404 if missing |
| POST | /api/posts | Create a post; 201 with a Location header |
| PUT | /api/posts/{id} | Replace a post; 404 if missing |
| DELETE | /api/posts/{id} | Delete a post; 204, or 404 if missing |

POST and PUT accept `{"userId":1,"title":"Hello","body":"Example"}`.
UserId must be positive and both text fields must be nonblank, otherwise HTTP 400.
The original `/weatherforecast` endpoint is also available.

Run tests with `dotnet test tests/C-Refresh.Tests/C-Refresh.Tests.csproj`.
Integration tests mock the provider transport and require no external API access.
