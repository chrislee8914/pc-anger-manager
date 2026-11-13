using System.Text.Json;

namespace PcAngerManager;

/// <summary>
/// 사용자 설정. %APPDATA%\PcAngerManager\settings.json 에 저장 (SPEC 5.4).
/// 기본값은 SPEC 5.4. 자동 실행 여부는 여기 말고 Windows 시작 프로그램 등록 자체로 판단.
/// </summary>
internal sealed class AppSettings
{
    public string Runner { get; set; } = "토끼";
    /// <summary>사용 중인 커스텀 러너 이름. 비어 있으면 기본 캐릭터(Runner) 사용 (SPEC 5.4)</summary>
    public string CustomRunner { get; set; } = "";
    public string Theme { get; set; } = ThemeMood;   // v0.2.0부터 기본값 (SPEC 5.4)
    public string Basis { get; set; } = SystemStatus.BasisCpu;
    public int MaxFps { get; set; } = 30;
    public string TemperatureUnit { get; set; } = "시스템";

    public const string ThemeSystem = "시스템 사용 설정";
    public const string ThemeLight = "라이트 모드";
    public const string ThemeDark = "다크 모드";
    public const string ThemeMood = "분노 게이지";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PcAngerManager", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 한글을 그대로 저장
    };

    public static readonly string[] Themes = [ThemeMood, ThemeSystem, ThemeLight, ThemeDark];

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            if (File.Exists(FilePath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception)
        {
            // 파일이 깨졌으면 기본값으로 시작
        }
        // 모르는 테마 이름(예: 옛 버전 이름)이면 기본값으로
        if (!Themes.Contains(settings.Theme)) settings.Theme = ThemeMood;
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // 저장 실패해도 앱은 계속 동작
        }
    }
}
