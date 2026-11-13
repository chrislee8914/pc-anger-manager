using System.Runtime.InteropServices;

namespace PcAngerManager;

/// <summary>
/// 시스템 전체 CPU 사용률 측정. Sample()을 1초마다 부르면 최근 3초 평균도 계산함 (SPEC 5.1).
/// Windows가 누적해 둔 "쉰 시간 / 일한 시간"을 직전 값과 비교하는 방식이라 매우 가벼움.
/// </summary>
internal sealed class CpuMeter
{
    private ulong _lastIdle, _lastKernel, _lastUser;
    private readonly RollingAverage _average = new();

    /// <summary>직전 1초 전체 사용률 (%) = 사용자 + 커널</summary>
    public double Current { get; private set; }
    public double UserPercent { get; private set; }
    public double KernelPercent { get; private set; }
    public double IdlePercent { get; private set; }

    /// <summary>최근 3초 평균 사용률 (%)</summary>
    public double Average => _average.Value;

    public CpuMeter()
    {
        GetSystemTimes(out _lastIdle, out _lastKernel, out _lastUser);
    }

    public void Sample()
    {
        GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

        // Windows의 kernel 값에는 쉬는 시간(idle)이 포함되어 있음
        double idleDelta = idle - _lastIdle;
        double kernelDelta = kernel - _lastKernel;
        double userDelta = user - _lastUser;
        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;

        double total = kernelDelta + userDelta;
        if (total <= 0) return;

        UserPercent = userDelta * 100 / total;
        KernelPercent = (kernelDelta - idleDelta) * 100 / total;
        IdlePercent = idleDelta * 100 / total;
        Current = UserPercent + KernelPercent;
        _average.Add(Current);
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);
}
