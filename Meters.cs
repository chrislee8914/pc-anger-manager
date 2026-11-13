using System.Runtime.InteropServices;

namespace PcAngerManager;

/// <summary>최근 N개 값의 평균 (속도 계산용 3초 평균, SPEC 5.1)</summary>
internal sealed class RollingAverage(int window = 3)
{
    private readonly Queue<double> _values = new();
    public double Value { get; private set; }

    public void Add(double value)
    {
        _values.Enqueue(value);
        while (_values.Count > window) _values.Dequeue();
        Value = _values.Average();
    }
}

/// <summary>
/// GPU 사용률 (SPEC 5.4). "GPU 엔진" 카운터는 프로그램별·엔진별로 나뉘어 있어
/// 같은 엔진끼리 합산한 뒤, 가장 바쁜 엔진 = 최대, 전체 엔진 평균 = 평균.
/// </summary>
internal sealed class GpuMeter : IDisposable
{
    private readonly PdhCounterSet _set = new(@"\GPU Engine(*)\Utilization Percentage");

    public bool Available => _set.Available;
    public double Average { get; private set; }
    public double Max { get; private set; }

    public void Sample()
    {
        var engines = new Dictionary<string, double>();
        foreach (var (name, value) in _set.Collect())
        {
            // 이름 예: pid_1234_luid_0x..._phys_0_eng_3_engtype_3D → "luid_" 뒤가 엔진 구분
            int at = name.IndexOf("luid_", StringComparison.Ordinal);
            string engine = at >= 0 ? name[at..] : name;
            engines[engine] = engines.GetValueOrDefault(engine) + value;
        }
        if (engines.Count == 0) { Average = Max = 0; return; }
        Max = Math.Min(100, engines.Values.Max());
        Average = Math.Min(100, engines.Values.Average());
    }

    public void Dispose() => _set.Dispose();
}

/// <summary>메모리 사용량. Windows가 주는 사용률(정수)을 그대로 씀 (SPEC 5.4)</summary>
internal sealed class MemoryMeter
{
    public int LoadPercent { get; private set; }
    public ulong TotalBytes { get; private set; }
    public ulong AvailableBytes { get; private set; }
    public ulong UsedBytes => TotalBytes - AvailableBytes;

    public MemoryMeter() => Sample();

    public void Sample()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status)) return;
        LoadPercent = (int)status.dwMemoryLoad;
        TotalBytes = status.ullTotalPhys;
        AvailableBytes = status.ullAvailPhys;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}

/// <summary>네트워크 속도. 모든 어댑터 합계 (SPEC 5.4)</summary>
internal sealed class NetworkMeter : IDisposable
{
    private readonly PdhCounterSet _sent = new(@"\Network Interface(*)\Bytes Sent/sec");
    private readonly PdhCounterSet _received = new(@"\Network Interface(*)\Bytes Received/sec");

    public double UploadBytesPerSec { get; private set; }
    public double DownloadBytesPerSec { get; private set; }

    public void Sample()
    {
        UploadBytesPerSec = _sent.Collect().Sum(v => v.Value);
        DownloadBytesPerSec = _received.Collect().Sum(v => v.Value);
    }

    public void Dispose()
    {
        _sent.Dispose();
        _received.Dispose();
    }
}
