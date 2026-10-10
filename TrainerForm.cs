using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Keyline;

public sealed class TrainerForm : Form
{
    public TrainerForm()
    {
        Text = "Keyline — тренажёр печати";
        ClientSize = new Size(1160, 790);
        MinimumSize = new Size(1000, 790);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(14, 15, 17);
        AutoScaleMode = AutoScaleMode.Dpi;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "keyline.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        Controls.Add(new TrainerSurface { Dock = DockStyle.Fill });
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        try { DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch (DllNotFoundException) { }
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

public sealed class TrainerSurface : Control
{
    private static readonly Color Bg = Color.FromArgb(14, 15, 17);
    private static readonly Color Card = Color.FromArgb(23, 25, 28);
    private static readonly Color Line = Color.FromArgb(43, 46, 50);
    private static readonly Color Ink = Color.FromArgb(233, 236, 239);
    private static readonly Color Dim = Color.FromArgb(139, 146, 153);
    private static readonly Color Accent = Color.FromArgb(190, 231, 122);
    private static readonly Color Wrong = Color.FromArgb(244, 121, 126);
    private readonly Session session = new();
    private readonly HistoryStore history = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private readonly List<(RectangleF Rect, Action Action)> actions = [];
    private readonly Dictionary<(float, bool, bool), Font> fonts = [];
    private string language = "RU";
    private int duration = 60;
    private bool statistics;
    private int pageStart;
    private bool blink;
    private float scale = 1;
    private float width;
    private float height;
    private char nextKey;

    public TrainerSurface()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        BackColor = Bg;
        TabStop = true;
        AccessibleName = "Keyline. Поле тренировки. Печатайте показанный текст. Escape — новая сессия.";
        Reset();
        timer.Tick += (_, _) =>
        {
            blink = Environment.TickCount64 % 1000 < 650;
            if (session.Due) Finish();
            Invalidate();
        };
        timer.Start();
    }

    private void Reset() { session.Reset(language, duration); pageStart = 0; statistics = false; Focus(); Invalidate(); }
    private void Finish() { if (!session.Finished && session.Started) { history.Add(session.Finish()); Invalidate(); } }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { Reset(); e.SuppressKeyPress = true; }
        else if (e.Control || e.Alt) e.SuppressKeyPress = true;
        else if (e.KeyCode == Keys.Back && !statistics) { session.Backspace(); Invalidate(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Enter && session.Finished) { Reset(); e.SuppressKeyPress = true; }
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        e.Handled = true;
        if (statistics) return;
        if (session.Due) { Finish(); return; }
        session.Type(e.KeyChar);
        if (session.Typed.Length == session.Passage.Length) Finish();
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        var point = new PointF(e.X / scale, e.Y / scale);
        foreach (var item in actions.ToArray()) if (item.Rect.Contains(point)) { item.Action(); break; }
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = actions.Any(x => x.Rect.Contains(e.X / scale, e.Y / scale)) ? Cursors.Hand : Cursors.IBeam;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        scale = DeviceDpi / 96f;
        width = Width / scale; height = Height / scale;
        var g = e.Graphics;
        g.ScaleTransform(scale, scale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Bg); actions.Clear();
        Header(g);
        if (statistics) Statistics(g); else Practice(g);
        DrawLine(g, 36, height - 44, width - 36, height - 44);
        Write(g, "KEYLINE  /  ПРАКТИКА КАЖДЫЙ ДЕНЬ", 36, height - 29, 8, Dim, true);
        Write(g, "ESC  заново     •     BACKSPACE  исправить", width - 338, height - 29, 9, Dim);
    }
    private void Header(Graphics g)
    {
        Fill(g, new RectangleF(36, 29, 38, 38), Accent, 7);
        using var pen = new Pen(Bg, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, 48, 39, 48, 56);
        g.DrawLine(pen, 59, 39, 49, 49);
        g.DrawLine(pen, 51, 47, 61, 57);
        Write(g, "keyline", 87, 28, 22, Ink, true);
        Write(g, "ТРЕНАЖЁР ПЕЧАТИ", 89, 59, 7.5f, Dim, true);
        Button(g, new(width - 309, 33, 126, 34), "Тренировка", !statistics, () => { statistics = false; Focus(); });
        Button(g, new(width - 172, 33, 136, 34), "Статистика", statistics, () => { if (!session.Started || session.Finished) statistics = true; }, !session.Started || session.Finished);
        DrawLine(g, 36, 91, width - 36, 91);
    }
    private void Practice(Graphics g)
    {
        Write(g, session.Finished ? "Хорошая работа." : "Поймай свой ритм.", 36, 114, 26, Ink, true);
        Write(g, session.Finished ? "Результат сохранён. Продолжай практику в удобном темпе." : "Точность важнее спешки. Скорость придёт с практикой.", 38, 156, 11, Dim);
        bool ready = !session.Started || session.Finished;
        Write(g, "ЯЗЫК", 38, 205, 8, Dim, true);
        Button(g, new(100, 194, 52, 33), "RU", language == "RU", () => { language = "RU"; Reset(); }, ready);
        Button(g, new(157, 194, 52, 33), "EN", language == "EN", () => { language = "EN"; Reset(); }, ready);
        Write(g, "ВРЕМЯ", 242, 205, 8, Dim, true);
        for (int i = 0; i < 3; i++)
        {
            int seconds = new[] { 30, 60, 120 }[i];
            Button(g, new(308 + 64 * i, 194, 58, 33), seconds + " с", duration == seconds, () => { duration = seconds; Reset(); }, ready);
        }
        Button(g, new(width - 184, 194, 148, 33), "↻  Новая сессия", false, Reset);

        float cell = (width - 72) / 4;
        Stat(g, 36, 251, cell, session.Remaining.ToString("00"), "сек", "ОСТАЛОСЬ", Accent);
        Stat(g, 36 + cell, 251, cell, session.Wpm.ToString("0"), "WPM", "СКОРОСТЬ", Ink);
        Stat(g, 36 + cell * 2, 251, cell, session.Accuracy.ToString("0.0"), "%", "ТОЧНОСТЬ", Ink);
        Stat(g, 36 + cell * 3, 251, cell, session.Errors.ToString(), "", "ОШИБКИ", session.Errors > 0 ? Wrong : Ink);
        var passageRect = new RectangleF(36, 361, width - 72, 218);
        Fill(g, passageRect, Card, 10);
        Write(g, session.Finished ? "СЕССИЯ ЗАВЕРШЕНА" : "ТЕКСТ ДЛЯ НАБОРА", 60, 380, 8, Dim, true);
        string state = session.Finished ? "ГОТОВО" : session.Started ? "В ПРОЦЕССЕ" : "ОЖИДАНИЕ ВВОДА";
        Write(g, state, width - 216, 380, 8, session.Started ? Accent : Dim, true);
        if (session.Finished)
        {
            Write(g, $"{session.Correct} верных символов за {session.Elapsed:0} сек.", 60, 426, 22, Ink, true);
            Write(g, $"Точность учитывает все нажатия, включая исправленные ошибки.", 61, 470, 11, Dim);
            Button(g, new(60, 512, 183, 36), "Продолжить  ↵", true, Reset);
            Button(g, new(253, 512, 167, 36), "Мои результаты", false, () => statistics = true);
        }
        else RenderPassage(g);
        Fill(g, new RectangleF(36, 584, width - 72, 3), Line);
        float progress = (float)(session.Elapsed / duration);
        if (progress > 0) Fill(g, new RectangleF(36, 584, (width - 72) * progress, 3), Accent);
        if (history.Error is not null) Write(g, history.Error, 38, 602, 10, Wrong);
        else Write(g, session.Finished ? "ENTER — новая тренировка. Результаты хранятся локально в папке программы." : !session.Started ? "Начни печатать — таймер запустится автоматически. Проверь раскладку клавиатуры." : "Смотри на текст, держи руки на основном ряду и сохраняй ровный темп.", 38, 602, 10, Dim);
        Keyboard(g, 641);
    }
    private void RenderPassage(Graphics g)
    {
        float charWidth = 13.25f;
        int columns = Math.Max(25, (int)((width - 124) / charWidth));
        int cursor = session.Typed.Length;
        if (cursor < pageStart) pageStart = Math.Max(0, pageStart - columns * 3);
        var lines = Wrap(pageStart, columns);
        while (cursor >= lines[^1].End && lines[^1].End < session.Passage.Length)
        { pageStart = lines[^1].End; lines = Wrap(pageStart, columns); }
        nextKey = session.Passage[Math.Min(cursor, session.Passage.Length - 1)];
        for (int row = 0; row < lines.Count; row++)
        {
            var (start, end) = lines[row];
            for (int i = start; i < end; i++)
            {
                float x = 60 + (i - start) * charWidth, y = 420 + row * 42;
                bool typed = i < cursor;
                bool error = typed && session.Typed[i] != session.Passage[i];
                if (error) Fill(g, new RectangleF(x, y, charWidth, 28), Color.FromArgb(66, 34, 38), 2);
                Write(g, session.Passage[i].ToString(), x, y, 16, error ? Wrong : typed ? Ink : Dim, mono: true);
                if (i == cursor && blink && Focused) Fill(g, new RectangleF(x, y + 28, charWidth - 1, 2), Accent);
            }
        }
    }
    private List<(int Start, int End)> Wrap(int start, int columns)
    {
        var list = new List<(int, int)>();
        for (int row = 0; row < 3 && start < session.Passage.Length; row++)
        {
            int end = Math.Min(start + columns, session.Passage.Length);
            if (end < session.Passage.Length)
            {
                int space = session.Passage.LastIndexOf(' ', end - 1, end - start);
                if (space > start) end = space + 1;
            }
            list.Add((start, end)); start = end;
        }
        return list;
    }
    private void Keyboard(Graphics g, float y)
    {
        string keys = language == "RU" ? "фыва олдж" : "asdf jkl;";
        Write(g, "ОСНОВНОЙ РЯД", 38, y + 10, 8, Dim, true);
        float x = 202;
        foreach (char key in keys)
        {
            if (key == ' ') { x += 18; continue; }
            bool active = !session.Finished && nextKey == key;
            Fill(g, new(x, y, 34, 36), active ? Accent : Card, 5);
            Write(g, key.ToString().ToUpper(), x + 10, y + 8, 11, active ? Bg : Dim, true);
            if (key is 'а' or 'о' or 'f' or 'j') Fill(g, new(x + 12, y + 29, 10, 1), active ? Bg : Dim);
            x += 40;
        }
        if (width > 1090) Write(g, "МЕНЬШЕ ШУМА. БОЛЬШЕ ФОКУСА.", width - 287, y + 12, 8, Dim, true);
    }
    private void Statistics(Graphics g)
    {
        Write(g, "Твой прогресс.", 36, 114, 26, Ink, true);
        Write(g, "Последние тренировки и личные рекорды.", 38, 156, 11, Dim);
        var data = history.Results;
        float cell = (width - 72) / 4;
        Stat(g, 36, 208, cell, data.Count.ToString(), "", "ТРЕНИРОВОК", Accent);
        Stat(g, 36 + cell, 208, cell, data.Count == 0 ? "—" : data.Max(x => x.Wpm).ToString("0"), "WPM", "ЛУЧШАЯ СКОРОСТЬ", Ink);
        Stat(g, 36 + cell * 2, 208, cell, data.Count == 0 ? "—" : data.Average(x => x.Accuracy).ToString("0.0"), "%", "СРЕДНЯЯ ТОЧНОСТЬ", Ink);
        Stat(g, 36 + cell * 3, 208, cell, data.Count == 0 ? "—" : data.Average(x => x.Wpm).ToString("0"), "WPM", "СРЕДНЯЯ СКОРОСТЬ", Ink);
        Fill(g, new(36, 321, width - 72, 180), Card, 10);
        Write(g, "СКОРОСТЬ  /  ПОСЛЕДНИЕ 12 СЕССИЙ", 60, 341, 8, Dim, true);
        var chart = data.TakeLast(12).ToArray();
        if (chart.Length == 0) Write(g, "Здесь появится твой первый результат.", 60, 403, 18, Ink);
        else
        {
            double max = Math.Max(1, chart.Max(x => x.Wpm));
            float barW = (width - 144) / 12;
            for (int i = 0; i < chart.Length; i++)
            {
                float h = Math.Max(2, (float)(chart[i].Wpm / max * 76));
                Fill(g, new(62 + i * barW, 476 - h, barW - 18, h), i == chart.Length - 1 ? Accent : Color.FromArgb(77, 93, 62), 3);
                Write(g, chart[i].Wpm.ToString("0"), 62 + i * barW, 454 - h, 10, Dim);
            }
        }
        Write(g, "ДАТА", 48, 522, 8, Dim, true);
        Write(g, "РЕЖИМ", 280, 522, 8, Dim, true);
        Write(g, "СКОРОСТЬ", 485, 522, 8, Dim, true);
        Write(g, "ТОЧНОСТЬ", 665, 522, 8, Dim, true);
        Write(g, "ОШИБКИ", 835, 522, 8, Dim, true);
        int row = 0;
        foreach (var result in data.AsEnumerable().Reverse().Take(4))
        {
            float y = 553 + row++ * 38;
            DrawLine(g, 36, y - 9, width - 36, y - 9);
            Write(g, result.Date.ToString("dd.MM.yyyy  HH:mm"), 48, y, 11, Ink);
            Write(g, $"{result.Language}  /  {result.Duration} сек", 280, y, 11, Dim);
            Write(g, result.Wpm.ToString("0.0") + " WPM", 485, y, 11, Accent);
            Write(g, result.Accuracy.ToString("0.0") + "%", 665, y, 11, Ink);
            Write(g, result.Errors.ToString(), 835, y, 11, Dim);
        }
        if (history.Error is not null) Write(g, history.Error, 38, 712, 9, Wrong);
    }
    private void Stat(Graphics g, float x, float y, float w, string value, string unit, string title, Color color)
    {
        Write(g, title, x + 4, y, 8, Dim, true);
        Write(g, value, x, y + 22, 30, color, true);
        float measured = g.MeasureString(value, FontFor(30, true, false), int.MaxValue, StringFormat.GenericTypographic).Width;
        Write(g, unit, x + measured + 10, y + 44, 10, Dim);
        if (x > 36) DrawLine(g, x - 18, y + 4, x - 18, y + 73);
    }
    private void Button(Graphics g, RectangleF rect, string label, bool active, Action action, bool enabled = true)
    {
        Fill(g, rect, active ? Accent : Card, 5);
        var font = FontFor(10, true, false);
        using var brush = new SolidBrush(active ? Bg : enabled ? Ink : Dim);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(label, font, brush, rect, format);
        if (enabled) actions.Add((rect, action));
    }
    private Font FontFor(float size, bool bold, bool mono)
    {
        var key = (size, bold, mono);
        if (!fonts.TryGetValue(key, out var font)) fonts[key] = font = new Font(mono ? "Consolas" : "Segoe UI", size * 96f / 72f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        return font;
    }
    private void Write(Graphics g, string text, float x, float y, float size, Color color, bool bold = false, bool mono = false)
    {
        using var brush = new SolidBrush(color);
        g.DrawString(text, FontFor(size, bold, mono), brush, new PointF(x, y), StringFormat.GenericTypographic);
    }
    private static void DrawLine(Graphics g, float x, float y, float x2, float y2)
    { using var pen = new Pen(Line); g.DrawLine(pen, x, y, x2, y2); }
    private static void Fill(Graphics g, RectangleF rect, Color color, float radius = 0)
    {
        using var brush = new SolidBrush(color);
        if (radius == 0) { g.FillRectangle(brush, rect); return; }
        using var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); g.FillPath(brush, path);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); foreach (var font in fonts.Values) font.Dispose(); }
        base.Dispose(disposing);
    }
}
