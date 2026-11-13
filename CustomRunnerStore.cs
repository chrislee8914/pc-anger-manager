using System.Drawing.Imaging;
using System.Text.Json;

namespace PcAngerManager;

internal sealed record CustomRunnerProfile(string Name, List<string> FrameFileNames);

/// <summary>
/// 커스텀 러너 저장소 (SPEC 5.4).
/// %APPDATA%\PcAngerManager\CustomRunners\
///   ├── profiles.json        ← 러너 이름과 프레임 파일 순서
///   └── 러너이름\frame_0.png, frame_1.png ...
/// </summary>
internal static class CustomRunnerStore
{
    public const int MaxImageSize = 32;

    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PcAngerManager", "CustomRunners");

    private static string ProfilesPath => Path.Combine(Root, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static List<CustomRunnerProfile> LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesPath))
                return JsonSerializer.Deserialize<List<CustomRunnerProfile>>(File.ReadAllText(ProfilesPath), JsonOptions) ?? [];
        }
        catch (Exception)
        {
            // 목록 파일이 깨졌으면 빈 목록
        }
        return [];
    }

    /// <summary>러너의 프레임 그림들. 없거나 못 읽으면 빈 목록</summary>
    public static List<Bitmap> LoadFrames(string name)
    {
        var profile = LoadProfiles().FirstOrDefault(p => p.Name == name);
        var frames = new List<Bitmap>();
        if (profile is null) return frames;
        foreach (var file in profile.FrameFileNames)
        {
            string path = Path.Combine(Root, name, file);
            try { frames.Add(LoadBitmap(path)); }
            catch (Exception) { /* 빠진 파일은 건너뜀 */ }
        }
        return frames;
    }

    /// <summary>파일을 잠그지 않도록 복사본으로 읽음</summary>
    public static Bitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    /// <summary>요구 사항 확인: PNG 형식, 최대 32x32px (SPEC 3.5). 문제없으면 null</summary>
    public static string? Validate(string path, out Bitmap? bitmap)
    {
        bitmap = null;
        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            if (!image.RawFormat.Equals(ImageFormat.Png)) return "PNG 형식이 아님";
            if (image.Width > MaxImageSize || image.Height > MaxImageSize)
                return $"크기 {image.Width}x{image.Height}px (최대 {MaxImageSize}x{MaxImageSize}px)";
            bitmap = new Bitmap(image);
            return null;
        }
        catch (Exception)
        {
            return "이미지를 읽을 수 없음";
        }
    }

    public static void Save(string name, IReadOnlyList<Bitmap> frames)
    {
        string folder = Path.Combine(Root, name);
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, "frame_*.png")) File.Delete(old);

        var fileNames = new List<string>();
        for (int i = 0; i < frames.Count; i++)
        {
            string file = $"frame_{i}.png";
            frames[i].Save(Path.Combine(folder, file), ImageFormat.Png);
            fileNames.Add(file);
        }

        var profiles = LoadProfiles();
        profiles.RemoveAll(p => p.Name == name);
        profiles.Add(new CustomRunnerProfile(name, fileNames));
        SaveProfiles(profiles);
    }

    public static void Delete(string name)
    {
        string folder = Path.Combine(Root, name);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        var profiles = LoadProfiles();
        profiles.RemoveAll(p => p.Name == name);
        SaveProfiles(profiles);
    }

    private static void SaveProfiles(List<CustomRunnerProfile> profiles)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(profiles, JsonOptions));
    }
}
