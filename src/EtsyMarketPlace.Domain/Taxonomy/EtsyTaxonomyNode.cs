namespace EtsyMarketPlace.Domain.Taxonomy;

public sealed class EtsyTaxonomyNode
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public int Level { get; set; }

    public EtsyTaxonomyNode() { }

    public EtsyTaxonomyNode(long id, string name, string path, long parentId = 0, int level = 0)
    {
        Id = id;
        Name = name;
        Path = path;
        ParentId = parentId;
        Level = level;
    }
}
