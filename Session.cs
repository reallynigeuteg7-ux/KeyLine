using System.Diagnostics;
using System.Text.Json;

namespace Keyline;

public sealed record SessionResult(DateTime Date, string Language, int Duration, double Wpm, double Accuracy, int Errors);

public sealed class Session
{
    private readonly Stopwatch watch = new();
    public string Passage { get; private set; } = "";
    public string Typed { get; private set; } = "";
    public string Language { get; private set; } = "RU";
    public int Duration { get; private set; } = 60;
    public int Attempts { get; private set; }
    public int Errors { get; private set; }
    public bool Started { get; private set; }
    public bool Finished { get; private set; }
    public double Elapsed => Math.Min(watch.Elapsed.TotalSeconds, Duration);
    public int Remaining => Math.Max(0, (int)Math.Ceiling(Duration - Elapsed));
    public double Accuracy => Attempts == 0 ? 100 : (Attempts - Errors) * 100.0 / Attempts;
    public int Correct => Typed.Where((c, i) => c == Passage[i]).Count();
    public double Wpm => Elapsed < 1 ? 0 : Correct * 12.0 / Elapsed;
    public bool Due => Started && !Finished && watch.Elapsed.TotalSeconds >= Duration;

    public void Reset(string language, int duration)
    {
        watch.Reset(); Language = language; Duration = duration;
        Typed = ""; Attempts = Errors = 0; Started = Finished = false;
        Passage = Passages.Create(language);
    }

    public void Type(char c)
    {
        if (Finished || Due || char.IsControl(c) || Typed.Length >= Passage.Length) return;
        if (!Started) { Started = true; watch.Start(); }
        Attempts++;
        if (c != Passage[Typed.Length]) Errors++;
        Typed += c;
    }

    public void Backspace()
    {
        if (!Finished && !Due && Typed.Length > 0) Typed = Typed[..^1];
    }

    public SessionResult Finish()
    {
        watch.Stop(); Finished = true;
        return new(DateTime.Now, Language, Duration, Math.Round(Wpm, 1), Math.Round(Accuracy, 1), Errors);
    }
}

public static class Passages
{
    private static readonly string[] Ru =
    [
        "каждое утро начинается с простых вещей. свет в окне, чашка чая и несколько минут тишины помогают собраться с мыслями.",
        "хорошая работа требует внимания к деталям. не спеши, держи ровный ритм и постепенно увеличивай скорость.",
        "за окном тихо шумит город. люди идут по своим делам, а солнечный свет медленно движется по стене.",
        "новый навык приходит с практикой. маленькие шаги каждый день со временем превращаются в большой результат.",
        "на столе лежат книга и блокнот. иногда достаточно записать одну мысль, чтобы увидеть решение сложной задачи.",
        "вечером мы вышли на прогулку в парк. прохладный воздух и мягкий свет фонарей сделали этот день немного лучше."
    ];
    private static readonly string[] En =
    [
        "every morning begins with simple things. a quiet room, a cup of tea and a few minutes to gather your thoughts.",
        "good work takes patience and attention. keep a steady rhythm and let your speed grow with practice.",
        "the city moves quietly outside the window. people walk to work while the light travels across the wall.",
        "learning a new skill takes time. small steps every day can turn into something remarkable.",
        "there is a book and a notebook on the desk. writing down one idea can help you solve a difficult problem.",
        "we went for a walk in the park that evening. the cool air and soft light made the day feel a little better."
    ];
    public static string Create(string language)
    {
        var source = language == "RU" ? Ru : En;
        return string.Join(" ", Enumerable.Range(0, 12).SelectMany(_ => source.OrderBy(_ => Random.Shared.Next())));
    }
}

public sealed class HistoryStore
{
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "Data", "history.json");
    public List<SessionResult> Results { get; private set; } = [];
    public string? Error { get; private set; }
    public HistoryStore()
    {
        try { if (File.Exists(path)) Results = JsonSerializer.Deserialize<List<SessionResult>>(File.ReadAllText(path)) ?? []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { Error = "Не удалось прочитать историю. Результаты доступны в этой сессии."; }
    }
    public void Add(SessionResult result)
    {
        Results.Add(result);
        Results = Results.TakeLast(200).ToList();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path, true);
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Error = "Не удалось сохранить историю. Проверьте доступ к папке Data."; }
    }
}
