namespace MemoryCleaner.Models;

public sealed class ProcessGroup : ObservableObject
{
    private bool _isSelected;

    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public int ProcessCount { get; init; }
    public long MemoryBytes { get; init; }
    public bool HasWindow { get; init; }
    public bool IsProtected { get; init; }

    public double MemoryMB => MemoryBytes / 1024d / 1024d;
    public bool CanUnload => !IsProtected;
    public string DisplayDescription => string.IsNullOrWhiteSpace(Description) ? "—" : Description;
    public string DisplayCompany => string.IsNullOrWhiteSpace(Company) ? "—" : Company;
    public string SafetyLabel => IsProtected ? "системный" : HasWindow ? "можно выгрузить" : "фоновый";
    public string WindowLabel => HasWindow ? "Есть окно приложения" : "Без главного окна";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsProtected)
                value = false;
            SetProperty(ref _isSelected, value);
        }
    }
}

public sealed record ProcessListSnapshot(
    IReadOnlyList<ProcessGroup> Applications,
    MemorySnapshot Memory);

public sealed record MemorySnapshot(ulong TotalBytes, ulong AvailableBytes)
{
    public double TotalGB => TotalBytes / 1024d / 1024d / 1024d;
    public double AvailableGB => AvailableBytes / 1024d / 1024d / 1024d;
    public double UsedGB => Math.Max(0, TotalGB - AvailableGB);
}
