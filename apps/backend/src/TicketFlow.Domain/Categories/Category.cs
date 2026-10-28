namespace TicketFlow.Domain.Categories;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    private Category() { }

    public void Rename(string name, string slug) { Name = name; Slug = slug; }

    public Category(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }
}
