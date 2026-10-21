namespace C_Refresh;

public record DogApiResponse(string? Message, string? Status);
public record Dog(int Id, string Name, string ImageUrl);

public record DogInput(string? Name, string? ImageUrl)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Name) && IsImageUrl(ImageUrl);

    // Only store web URLs; this application never downloads user-supplied URLs.
    public static bool IsImageUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

// Mock saved dogs live only for this process and never change the external provider.
public sealed class DogStore
{
    private readonly object gate = new();
    private readonly Dictionary<int, Dog> dogs = new();
    private int nextId;

    public Dog[] All()
    {
        lock (gate) return dogs.Values.OrderBy(dog => dog.Id).ToArray();
    }

    public Dog? Get(int id)
    {
        lock (gate) return dogs.GetValueOrDefault(id);
    }

    public Dog Create(DogInput input)
    {
        // Allocate IDs under the same lock as writes to support concurrent requests.
        lock (gate)
        {
            var dog = new Dog(++nextId, input.Name!, input.ImageUrl!);
            dogs.Add(dog.Id, dog);
            return dog;
        }
    }

    public Dog? Update(int id, DogInput input)
    {
        lock (gate)
        {
            if (!dogs.ContainsKey(id)) return null;
            return dogs[id] = new Dog(id, input.Name!, input.ImageUrl!);
        }
    }

    public bool Delete(int id)
    {
        lock (gate) return dogs.Remove(id);
    }
}
