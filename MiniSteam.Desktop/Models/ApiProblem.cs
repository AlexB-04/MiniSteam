namespace MiniSteam.Desktop.Models;

public sealed class ApiProblem
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? TraceId { get; set; }
    public string? Code { get; set; }
}
