namespace RentalAI.Api.Modules.Properties;

public sealed class PropertyPhoto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Url { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
