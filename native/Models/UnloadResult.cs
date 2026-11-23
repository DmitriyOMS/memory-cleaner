namespace MemoryCleaner.Models;

public enum UnloadStatus
{
    Completed,
    Partial,
    Failed,
    NotFound
}

public sealed record UnloadResult
{
    public required string Name { get; init; }
    public UnloadStatus Status { get; init; }
    public int ClosedCount { get; init; }
    public int TotalCount { get; init; }
    public double FreedMB { get; init; }
    public string? Error { get; init; }

    public string StatusText => Status switch
    {
        UnloadStatus.Completed => $"выгружена, освобождено примерно {FreedMB:N0} МБ",
        UnloadStatus.Partial => $"выгружена частично: {ClosedCount} из {TotalCount} процессов",
        UnloadStatus.NotFound => "уже не запущена",
        _ => string.IsNullOrWhiteSpace(Error)
            ? "не удалось завершить процессы"
            : $"не удалось завершить процессы: {Error}"
    };
}
