using System.Diagnostics;

namespace MailLoadTester;

public sealed record AttachmentSource(string FileName, string FullPath, byte[]? PreloadedContent, long Length);

public sealed record AttachmentPlan(
    IReadOnlyList<AttachmentSource> Sources,
    long TotalBytes,
    bool Preloaded,
    string Description);

public static class AttachmentPlanner
{
    public const long DefaultMaxAttachmentBytes = 512L * 1024 * 1024;
    private const long MaxPreloadBytes = DefaultMaxAttachmentBytes;
    private const long MinPreloadBudget = 64L * 1024 * 1024;
    private const long RandomAttachmentSafetyHardCap = 1L * 1024 * 1024 * 1024;
    private const double RandomAttachmentSafetyFraction = 0.15;

    public sealed record RandomAttachmentSafetyEstimate(
        bool IsSafe,
        long EstimatedBytes,
        long AvailableBytes,
        long BudgetBytes,
        string Explanation);

    public static long GetAvailableMemoryBytes() => GetAvailableMemory();

    public static void EnsurePreloadSafe(long bytes, string description = "přednačtený obsah")
    {
        if (bytes <= 0) return;
        const long hardCap = 512L * 1024 * 1024;
        if (bytes > hardCap)
            throw new InvalidOperationException($"{description}: {FormatBytes(bytes)} překračuje hard cap {FormatBytes(hardCap)}.");
        var available = GetAvailableMemory();
        var budget = Math.Max(MinPreloadBudget, (long)(available * 0.25));
        budget = Math.Min(budget, MaxPreloadBytes);
        if (bytes > budget)
            throw new InvalidOperationException($"{description}: {FormatBytes(bytes)} překračuje bezpečnostní rozpočet {FormatBytes(budget)} (dostupné ~{FormatBytes(available)}).");
    }

    public static RandomAttachmentSafetyEstimate EstimateRandomAttachments(
        int requestedSizeMb, int maxAttachments, int maxConcurrency)
    {
        maxAttachments = Math.Clamp(maxAttachments, 1, 5);
        maxConcurrency = Math.Max(1, maxConcurrency);
        var available = GetAvailableMemory();
        var budget = Math.Min(RandomAttachmentSafetyHardCap, (long)(available * RandomAttachmentSafetyFraction));
        budget = Math.Max(8L * 1024 * 1024, budget);
        int sizeMb = requestedSizeMb <= 0
            ? Math.Max(1, (int)Math.Min(128, budget / (maxAttachments * (long)maxConcurrency * 1024L * 1024L * 3)))
            : requestedSizeMb;
        sizeMb = Math.Clamp(sizeMb, 1, 128);
        const double transientFactor = 2.37;
        var estimated = (long)(sizeMb * 1024L * 1024L * maxAttachments * maxConcurrency * transientFactor);
        var safe = estimated <= budget;
        var explanation =
            $"Odhad transient RAM: ~{FormatBytes(estimated)} (size={sizeMb}MB × att={maxAttachments} × workers={maxConcurrency} × {transientFactor:0.00}). " +
            $"Budget: {FormatBytes(budget)} z dostupných ~{FormatBytes(available)}.";
        return new RandomAttachmentSafetyEstimate(safe, estimated, available, budget, explanation);
    }

    public static int ResolveRandomAttachmentSizeMb(int requestedSizeMb, int maxAttachments, int maxConcurrency)
    {
        var est = EstimateRandomAttachments(requestedSizeMb, maxAttachments, maxConcurrency);
        if (requestedSizeMb > 0) return Math.Clamp(requestedSizeMb, 1, 128);
        maxAttachments = Math.Clamp(maxAttachments, 1, 5);
        maxConcurrency = Math.Max(1, maxConcurrency);
        var budget = est.BudgetBytes;
        var per = budget / (Math.Max(1, maxAttachments) * (long)maxConcurrency * 3);
        return Math.Clamp((int)(per / (1024 * 1024)), 1, 128);
    }

    public static string ExplainRandomAttachmentSafety(int requestedSizeMb, int maxAttachments, int maxConcurrency)
        => EstimateRandomAttachments(requestedSizeMb, maxAttachments, maxConcurrency).Explanation;

    public static AttachmentPlan Prepare(IReadOnlyList<string> paths, int maxConcurrency = 1)
    {
        if (paths is null || paths.Count == 0)
            return new AttachmentPlan(Array.Empty<AttachmentSource>(), 0, true, "žádné přílohy");

        var sources = new List<AttachmentSource>();
        long total = 0;
        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException($"Příloha neexistuje: {path}", path);
            if (info.Length > int.MaxValue)
                throw new InvalidOperationException($"Příloha je příliš velká: {path}");
            total = checked(total + info.Length);
            sources.Add(new AttachmentSource(info.Name, info.FullName, null, info.Length));
        }

        var available = GetAvailableMemory();
        var budget = Math.Max(MinPreloadBudget, (long)(available * 0.25));
        budget = Math.Min(budget, MaxPreloadBytes);
        var canPreload = total <= budget && total <= MaxPreloadBytes;

        if (canPreload)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                var bytes = File.ReadAllBytes(s.FullPath);
                sources[i] = s with { PreloadedContent = bytes };
            }
            return new AttachmentPlan(sources, total, true, $"{sources.Count} příloh v RAM ({FormatBytes(total)})");
        }

        return new AttachmentPlan(sources, total, false, $"{sources.Count} příloh z disku ({FormatBytes(total)})");
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }

    private static long GetAvailableMemory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var status = new MEMORYSTATUSEX { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref status))
                    return (long)status.AvailablePhysicalMemory;
            }
        }
        catch { }

        try
        {
            var info = GC.GetGCMemoryInfo();
            if (info.TotalAvailableMemoryBytes > 0)
                return Math.Max(64L * 1024 * 1024, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
        }
        catch { }

        return 2L * 1024 * 1024 * 1024;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
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

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
