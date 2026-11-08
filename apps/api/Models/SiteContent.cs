namespace Abson.Api.Models;

public sealed class SiteContent
{
    public int Id { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? UpdatedBy { get; set; }
}
