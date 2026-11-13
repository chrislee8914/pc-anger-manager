namespace PcAngerManager;

/// <summary>
/// 모든 측정 장치를 묶고, 측정 주기와 메뉴 표시 줄을 관리 (SPEC 3.1, 5.4).
///  - CPU, 메모리: 1초마다 (가벼움)
///  - GPU: 속도 기준이 GPU면 1초마다, 아니면 5초마다
///  - 온도, 네트워크: 5초마다
/// </summary>
internal sealed class SystemStatus : IDisposable
{
    public const string BasisCpu = "CPU";
    public const string BasisGpu = "GPU";
    public const string BasisMemory = "메모리";

    private const double GB = 1024.0 * 1024 * 1024;

    public CpuMeter Cpu { get; } = new();
    public GpuMeter Gpu { get; } = new();
    public MemoryMeter Memory { get; } = new();
    public TemperatureMeter Temperature { get; } = new();
    public NetworkMeter Network { get; } = new();

    private readonly RollingAverage _gpuAverage = new();
    private readonly RollingAverage _memoryAverage = new();
    private int _tick;

    /// <summary>1초마다 호출. 5초마다 돌아오는 "느린 측정" 차례였으면 true</summary>
    public bool Sample(string basis)
    {
        bool slowTurn = _tick++ % 5 == 0;

        Cpu.Sample();
        Memory.Sample();
        _memoryAverage.Add(Memory.LoadPercent);

        if (basis == BasisGpu || slowTurn)
        {
            Gpu.Sample();
            _gpuAverage.Add(Gpu.Max);
        }
        if (slowTurn)
        {
            Temperature.Sample();
            Network.Sample();
        }
        return slowTurn;
    }

    /// <summary>달리기 속도와 툴팁에 쓰는 대표값 (3초 평균, %)</summary>
    public double BasisValue(string basis) => basis switch
    {
        BasisGpu => _gpuAverage.Value,
        BasisMemory => _memoryAverage.Value,
        _ => Cpu.Average,
    };

    /// <summary>메뉴 맨 위에 보여 줄 줄들 (SPEC 3.1 형식)</summary>
    public List<string> BuildLines(Func<double, string> formatTemperature)
    {
        var lines = new List<string>
        {
            $"CPU: {Cpu.Current:0.0}%",
            $"├── 사용자: {Cpu.UserPercent:0.0}%",
            $"├── 커널: {Cpu.KernelPercent:0.0}%",
            $"└── 사용 가능: {Cpu.IdlePercent:0.0}%",
        };

        if (Gpu.Available)
        {
            lines.Add("GPU:");
            lines.Add($"├── 평균: {Gpu.Average:0.0}%");
            lines.Add($"└── 최대: {Gpu.Max:0.0}%");
        }

        lines.Add($"메모리: {Memory.LoadPercent}%");
        lines.Add($"├── 전체: {Memory.TotalBytes / GB:0.00} GB");
        lines.Add($"├── 사용 중: {Memory.UsedBytes / GB:0.00} GB");
        lines.Add($"└── 사용 가능: {Memory.AvailableBytes / GB:0.00} GB");

        if (Temperature.Available)
        {
            lines.Add("온도:");
            lines.Add($"├── 평균: {formatTemperature(Temperature.AverageC)}");
            lines.Add($"└── 최대: {formatTemperature(Temperature.MaxC)}");
        }

        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .OrderBy(d => d.Name)
            .ToList();
        if (drives.Count > 0)
        {
            lines.Add("저장 장치:");
            for (int i = 0; i < drives.Count; i++)
            {
                var d = drives[i];
                bool last = i == drives.Count - 1;
                double used = d.TotalSize - d.TotalFreeSpace;
                string letter = d.Name.TrimEnd('\\', ':');
                string branch = last ? "└── " : "├── ";
                string indent = last ? "      " : "│     ";
                lines.Add($"{branch}{letter} 드라이브: {used * 100 / d.TotalSize:0.0}%");
                lines.Add($"{indent}├── 사용 중: {used / GB:0.00} GB");
                lines.Add($"{indent}└── 사용 가능: {d.TotalFreeSpace / GB:0.00} GB");
            }
        }

        lines.Add("네트워크:");
        lines.Add($"├── 업로드: {FormatSpeed(Network.UploadBytesPerSec)}");
        lines.Add($"└── 다운로드: {FormatSpeed(Network.DownloadBytesPerSec)}");

        return lines;
    }

    /// <summary>1024 KB/s 미만이면 KB/s 소수 1자리, 이상이면 MB/s 소수 2자리 (SPEC 3.1, 5.4)</summary>
    private static string FormatSpeed(double bytesPerSec)
    {
        double kb = bytesPerSec / 1024;
        return kb < 1024 ? $"{kb:0.0} KB/s" : $"{kb / 1024:0.00} MB/s";
    }

    public void Dispose()
    {
        Gpu.Dispose();
        Temperature.Dispose();
        Network.Dispose();
    }
}
