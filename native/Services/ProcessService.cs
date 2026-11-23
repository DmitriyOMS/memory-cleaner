using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryCleaner.Models;

namespace MemoryCleaner.Services;

public sealed class ProcessService
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "memory compression", "secure system",
        "smss", "csrss", "wininit", "winlogon", "lsass", "lsaiso", "services", "svchost",
        "dwm", "sihost", "taskhostw", "ctfmon", "explorer", "fontdrvhost", "audiodg",
        "runtimebroker", "searchhost", "startmenuexperiencehost", "shellexperiencehost",
        "searchindexer", "securityhealthservice", "securityhealthsystray", "msmpeng", "nissrv",
        "spoolsv", "dllhost", "conhost", "wmiprvse", "msiexec", "trustedinstaller",
        "dashost", "wlms", "vmcompute", "vmms", "lsm", "csidl", "applicationframehost",
        "avp", "avpui", "avpsus", "kavfs", "klwtblfs", "gwctlsrv",
        "node", "npm", "powershell", "pwsh", "cmd", "bash", "msedgewebview2",
        "kimi", "kimi-cu", "logioptionsplus_agent", "memorycleaner"
    };

    private static readonly IReadOnlyDictionary<string, string> KnownDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["svchost"] = "Системные службы Windows",
            ["memory compression"] = "Сжатие памяти Windows",
            ["browser"] = "Яндекс Браузер",
            ["thunderbird"] = "Почтовый клиент Mozilla Thunderbird",
            ["chatgpt"] = "Приложение ChatGPT",
            ["perplexity"] = "Приложение Perplexity AI",
            ["grok bot"] = "Приложение Grok",
            ["max"] = "Мессенджер MAX",
            ["codex"] = "Агент Codex CLI",
            ["bitwarden"] = "Менеджер паролей Bitwarden",
            ["avp"] = "Антивирус Kaspersky",
            ["dwm"] = "Оконный менеджер рабочего стола Windows",
            ["explorer"] = "Проводник Windows",
            ["conhost"] = "Хост консоли Windows",
            ["runtimebroker"] = "Брокер разрешений приложений Windows",
            ["msedgewebview2"] = "Веб-компонент Microsoft Edge WebView2",
            ["onedrive"] = "Синхронизация OneDrive",
            ["searchhost"] = "Поиск Windows",
            ["startmenuexperiencehost"] = "Меню «Пуск» Windows",
            ["lockapp"] = "Экран блокировки Windows",
            ["kimi"] = "Kimi Code",
            ["logioptionsplus_agent"] = "Агент Logitech Options+",
            ["blip"] = "Сервис уведомлений Blip",
            ["acrdldr"] = "Фоновая загрузка Adobe Acrobat",
            ["perplexity-rpc-server"] = "Фоновый сервер Perplexity"
        };

    private static readonly string OwnProcessName = GetOwnProcessName();

    public Task<ProcessListSnapshot> GetSnapshotAsync() => Task.Run(GetSnapshot);

    public ProcessListSnapshot GetSnapshot()
    {
        var accumulators = new Dictionary<string, ProcessAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch (Exception ex) when (IsProcessException(ex))
                {
                    continue;
                }

                if (!accumulators.TryGetValue(name, out var accumulator))
                {
                    accumulator = new ProcessAccumulator(name);
                    accumulators.Add(name, accumulator);
                }

                accumulator.ProcessCount++;
                accumulator.MemoryBytes += GetWorkingSet(process);
                accumulator.HasWindow |= HasMainWindow(process);

                if (!accumulator.HasMetadata)
                {
                    var metadata = GetMetadata(process);
                    accumulator.Description = metadata.Description;
                    accumulator.Company = metadata.Company;
                    accumulator.HasMetadata = !string.IsNullOrWhiteSpace(metadata.Description) ||
                                              !string.IsNullOrWhiteSpace(metadata.Company);
                }
            }
        }

        var applications = accumulators.Values.Select(accumulator =>
        {
            var isProtected = IsProtected(accumulator.Name);
            var description = KnownDescriptions.TryGetValue(accumulator.Name, out var knownDescription)
                ? knownDescription
                : accumulator.Description;

            if (string.IsNullOrWhiteSpace(description) && isProtected)
                description = "Системный компонент Windows";

            return new ProcessGroup
            {
                Name = accumulator.Name,
                Description = description,
                Company = accumulator.Company,
                ProcessCount = accumulator.ProcessCount,
                MemoryBytes = accumulator.MemoryBytes,
                HasWindow = accumulator.HasWindow,
                IsProtected = isProtected
            };
        }).ToArray();

        return new ProcessListSnapshot(applications, GetMemorySnapshot());
    }

    public Task<IReadOnlyList<UnloadResult>> UnloadAsync(IEnumerable<string> names)
    {
        return Task.Run<IReadOnlyList<UnloadResult>>(() => Unload(names));
    }

    public IReadOnlyList<UnloadResult> Unload(IEnumerable<string> names)
    {
        var allowedNames = names
            .Where(name => !string.IsNullOrWhiteSpace(name) && !IsProtected(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<UnloadResult>(allowedNames.Length);

        foreach (var name in allowedNames)
            results.Add(UnloadApplication(name));

        return results;
    }

    private static UnloadResult UnloadApplication(string name)
    {
        var processes = Process.GetProcessesByName(name);
        if (processes.Length == 0)
        {
            return new UnloadResult
            {
                Name = name,
                Status = UnloadStatus.NotFound
            };
        }

        var totalCount = processes.Length;
        long memoryBefore = 0;
        string? error = null;

        foreach (var process in processes)
        {
            using (process)
            {
                memoryBefore += GetWorkingSet(process);
                try
                {
                    if (process.HasExited)
                        continue;

                    if (HasMainWindow(process) && process.CloseMainWindow())
                        continue;

                    process.Kill();
                }
                catch (Exception ex) when (IsProcessException(ex))
                {
                    error ??= ex.Message;
                }
            }
        }

        WaitForExit(name, TimeSpan.FromSeconds(2));
        ForceKillRemaining(name, ref error);
        WaitForExit(name, TimeSpan.FromMilliseconds(500));

        var remainingCount = GetProcessCount(name);
        var closedCount = Math.Max(0, totalCount - remainingCount);
        var status = remainingCount == 0
            ? UnloadStatus.Completed
            : closedCount > 0
                ? UnloadStatus.Partial
                : UnloadStatus.Failed;
        var freedBytes = totalCount == 0 ? 0 : memoryBefore * closedCount / totalCount;

        return new UnloadResult
        {
            Name = name,
            Status = status,
            ClosedCount = closedCount,
            TotalCount = totalCount,
            FreedMB = freedBytes / 1024d / 1024d,
            Error = error
        };
    }

    private static void ForceKillRemaining(string name, ref string? error)
    {
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (Exception ex) when (IsProcessException(ex))
                {
                    error ??= ex.Message;
                }
            }
        }
    }

    private static void WaitForExit(string name, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (GetProcessCount(name) > 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(100);
    }

    private static int GetProcessCount(string name)
    {
        var processes = Process.GetProcessesByName(name);
        foreach (var process in processes)
            process.Dispose();
        return processes.Length;
    }

    private static bool IsProtected(string name)
    {
        return ProtectedNames.Contains(name) || OwnProcessName.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private static long GetWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch (Exception ex) when (IsProcessException(ex))
        {
            return 0;
        }
    }

    private static bool HasMainWindow(Process process)
    {
        try
        {
            return !process.HasExited && process.MainWindowHandle != IntPtr.Zero;
        }
        catch (Exception ex) when (IsProcessException(ex))
        {
            return false;
        }
    }

    private static (string Description, string Company) GetMetadata(Process process)
    {
        try
        {
            var fileInfo = process.MainModule?.FileVersionInfo;
            return (fileInfo?.FileDescription ?? string.Empty, fileInfo?.CompanyName ?? string.Empty);
        }
        catch (Exception ex) when (IsProcessException(ex))
        {
            return (string.Empty, string.Empty);
        }
    }

    private static bool IsProcessException(Exception ex)
    {
        return ex is InvalidOperationException or NotSupportedException or Win32Exception or UnauthorizedAccessException;
    }

    private static string GetOwnProcessName()
    {
        using var process = Process.GetCurrentProcess();
        return process.ProcessName;
    }

    private static MemorySnapshot GetMemorySnapshot()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref status)
            ? new MemorySnapshot(status.TotalPhysicalMemory, status.AvailablePhysicalMemory)
            : new MemorySnapshot(0, 0);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private sealed class ProcessAccumulator
    {
        public ProcessAccumulator(string name) => Name = name;

        public string Name { get; }
        public string Description { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public int ProcessCount { get; set; }
        public long MemoryBytes { get; set; }
        public bool HasWindow { get; set; }
        public bool HasMetadata { get; set; }
    }
}
