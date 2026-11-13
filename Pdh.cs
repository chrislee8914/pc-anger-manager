using System.Runtime.InteropServices;

namespace PcAngerManager;

/// <summary>
/// Windows 성능 카운터(PDH) 읽기 도구. 이름에 (*)가 들어간 카운터의 모든 항목을 한 번에 읽음.
/// 영어 카운터 이름을 쓰므로 한국어 Windows에서도 동작. 무거운 WMI 대신 사용.
/// "초당 ~" 같은 비율 카운터는 직전 Collect()와의 차이로 계산되므로 주기적으로 불러야 함.
/// </summary>
internal sealed class PdhCounterSet : IDisposable
{
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_FMT_NOSCALE = 0x00001000;
    private const int PDH_MORE_DATA = unchecked((int)0x800007D2);

    private IntPtr _query;
    private IntPtr _counter;

    public bool Available { get; }

    public PdhCounterSet(string englishPath)
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
        if (PdhAddEnglishCounter(_query, englishPath, IntPtr.Zero, out _counter) != 0) return;
        PdhCollectQueryData(_query); // 비율 카운터용 첫 기준점
        Available = true;
    }

    /// <summary>새 값을 모으고 (항목 이름, 값) 목록을 돌려줌. 실패하면 빈 목록.</summary>
    public List<(string Name, double Value)> Collect()
    {
        var result = new List<(string, double)>();
        if (!Available || PdhCollectQueryData(_query) != 0) return result;

        const uint format = PDH_FMT_DOUBLE | PDH_FMT_NOSCALE;
        uint size = 0;
        if (PdhGetFormattedCounterArray(_counter, format, ref size, out uint count, IntPtr.Zero) != PDH_MORE_DATA || size == 0)
            return result;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArray(_counter, format, ref size, out count, buffer) != 0) return result;

            // 항목 하나 = 이름 포인터 + 상태(4) + 빈칸(4) + 값(8)
            int itemSize = IntPtr.Size + 16;
            for (int i = 0; i < count; i++)
            {
                IntPtr item = buffer + i * itemSize;
                int status = Marshal.ReadInt32(item, IntPtr.Size);
                if (status != 0 && status != 1) continue; // 0 = 정상, 1 = 새 데이터
                string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? "";
                double value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, IntPtr.Size + 8));
                result.Add((name, value));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return result;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
        _query = IntPtr.Zero;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);
}
