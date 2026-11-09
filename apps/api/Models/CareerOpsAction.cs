namespace Abson.Api.Models;

public sealed class CareerOpsAction
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
}
