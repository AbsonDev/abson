namespace Abson.Api.Models;

public sealed class CareerOpsSnapshot
{
    public int Id { get; set; } = 1;
    public string SnapshotJson { get; set; } = "{}";
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = "career-ops";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
