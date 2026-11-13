namespace PcAngerManager;

/// <summary>
/// "열 영역(Thermal Zone)" 온도 측정 (SPEC 5.4). 관리자 권한 불필요.
/// </summary>
internal sealed class TemperatureMeter : IDisposable
{
    // 값 단위: 0.1 켈빈 (예: 3432 → 343.2K → 70.05°C)
    private readonly PdhCounterSet _set = new(@"\Thermal Zone Information(*)\High Precision Temperature");

    public bool Available { get; private set; }
    public double AverageC { get; private set; }
    public double MaxC { get; private set; }

    public TemperatureMeter() => Sample();

    public void Sample()
    {
        var values = _set.Collect().Select(v => v.Value / 10.0 - 273.15).ToList();
        Available = values.Count > 0;
        if (!Available) return;
        AverageC = values.Average();
        MaxC = values.Max();
    }

    public void Dispose() => _set.Dispose();
}
