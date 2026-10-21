namespace C_Refresh;

public record Post(int Id, int UserId, string Title, string Body);

public record PostInput(int UserId, string? Title, string? Body)
{
    public bool IsValid() => UserId > 0 && !string.IsNullOrWhiteSpace(Title)
        && !string.IsNullOrWhiteSpace(Body);
}

// A singleton provides mock persistence across requests; restarting clears all posts.
public sealed class PostStore
{
    private readonly object gate = new();
    private readonly Dictionary<int, Post> posts = new();
    private int nextId;

    public Post[] All()
    {
        lock (gate) return posts.Values.OrderBy(post => post.Id).ToArray();
    }

    public Post? Get(int id)
    {
        lock (gate) return posts.GetValueOrDefault(id);
    }

    public Post Create(PostInput input)
    {
        // Guard both ID allocation and writes so concurrent requests cannot collide.
        lock (gate)
        {
            var post = new Post(++nextId, input.UserId, input.Title!, input.Body!);
            posts.Add(post.Id, post);
            return post;
        }
    }

    public Post? Update(int id, PostInput input)
    {
        lock (gate)
        {
            if (!posts.ContainsKey(id)) return null;
            return posts[id] = new Post(id, input.UserId, input.Title!, input.Body!);
        }
    }

    public bool Delete(int id)
    {
        lock (gate) return posts.Remove(id);
    }
}
