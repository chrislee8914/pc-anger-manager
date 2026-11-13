namespace PcAngerManager;

/// <summary>
/// 사용률 → 초당 프레임 수 (SPEC 5.1).
/// </summary>
internal static class SpeedRule
{
    /// <summary>0~3단계. 25% 단위로 나눔</summary>
    public static int Level(double usage) => usage switch
    {
        < 25 => 0,
        < 50 => 1,
        < 75 => 2,
        _ => 3,
    };

    public static int Fps(double usage, int maxFps)
    {
        int fps = Level(usage) switch
        {
            0 => 5,
            1 => 10,
            2 => 20,
            _ => maxFps,
        };
        return Math.Min(fps, maxFps);
    }
}
