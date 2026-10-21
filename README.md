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

`GET /api/dogs/external` fetches a random dog image URL from the free
[Dog CEO API](https://dog.ceo/dog-api/). It returns `message` (the image URL)
and `status`. Provider failures, timeouts, and invalid responses return HTTP 502.

Saved dogs have separate mock CRUD routes: `GET /api/dogs`, `GET /api/dogs/{id}`,
`POST /api/dogs`, `PUT /api/dogs/{id}`, and `DELETE /api/dogs/{id}`.
POST and PUT accept `{"name":"Buddy","imageUrl":"https://images.dog.ceo/example.jpg"}`.
Name must be nonblank and ImageUrl must be an absolute HTTP or HTTPS URL (400 otherwise).
Create returns 201 with a Location header, update returns 200, and delete returns 204.
Missing IDs return 404. Fetching an external image does not automatically save it.
Saved dogs are held in memory until restart, and writes never go to Dog CEO.

Run tests with `dotnet test tests/C-Refresh.Tests/C-Refresh.Tests.csproj`.
Integration tests mock the provider transport and require no external API access.
