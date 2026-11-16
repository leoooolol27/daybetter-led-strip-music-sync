using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

sealed class AppSettings
{
    readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DaybetterMusicSync", "settings.txt");
    public int Language = 1;
    public readonly Dictionary<string, int> Overrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public AppSettings()
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int number;
                if (line.StartsWith("lang=") && int.TryParse(line.Substring(5), out number) && number >= 0 && number <= 2)
                    Language = number;
                int split = line.IndexOf(':');
                if (split < 1 || !int.TryParse(line.Substring(split + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number)) continue;
                try { Overrides[Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, split)))] = number; }
                catch (FormatException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var lines = new List<string> { "lang=" + Language };
            foreach (var item in Overrides)
                lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Key)) + ":" + item.Value.ToString("X8"));
            File.WriteAllLines(path + ".tmp", lines);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}

sealed class WheelDialog : Form
{
    const int Diameter = 220;
    readonly Bitmap image = new Bitmap(Diameter, Diameter);
    readonly PictureBox wheel = new PictureBox { Width = Diameter, Height = Diameter, Left = 20, Top = 15 };
    readonly TrackBar value = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, TickStyle = TickStyle.None, Left = 18, Top = 241, Width = 224, Height = 28, AutoSize = false };
    readonly Label sample = new Label { Left = 20, Top = 280, Width = 103, Height = 36, TextAlign = ContentAlignment.MiddleCenter };
    double hue;
    double saturation;
    public Color SelectedColor { get { return Hsv(hue, saturation, value.Value / 100d); } }

    public WheelDialog(Color seed, string title, string apply)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(260, 335);
        BackColor = Color.FromArgb(27, 24, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        double high = Math.Max(seed.R, Math.Max(seed.G, seed.B));
        double low = Math.Min(seed.R, Math.Min(seed.G, seed.B));
        hue = seed.GetHue();
        saturation = high == 0 ? 0 : (high - low) / high;
        value.Value = (int)Math.Round(high / 255 * 100);
        value.AccessibleName = "Value / 颜色明度";
        for (int y = 0; y < Diameter; y++)
        for (int x = 0; x < Diameter; x++)
        {
            double dx = (x - Diameter / 2.0) / (Diameter / 2.0 - 2);
            double dy = (y - Diameter / 2.0) / (Diameter / 2.0 - 2);
            double radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius <= 1) image.SetPixel(x, y, Hsv(Angle(dx, dy), radius, 1));
        }
        wheel.Image = image;
        wheel.Cursor = Cursors.Cross;
        wheel.MouseDown += (sender, args) => SelectAt(args.Location);
        wheel.MouseMove += (sender, args) => { if (args.Button == MouseButtons.Left) SelectAt(args.Location); };
        wheel.Paint += (sender, args) =>
        {
            double radians = hue * Math.PI / 180;
            float x = (float)(110 + Math.Cos(radians) * saturation * 108);
            float y = (float)(110 + Math.Sin(radians) * saturation * 108);
            args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var ring = new Pen(Color.Black, 4)) args.Graphics.DrawEllipse(ring, x - 6, y - 6, 12, 12);
            using (var ring = new Pen(Color.White, 2)) args.Graphics.DrawEllipse(ring, x - 6, y - 6, 12, 12);
        };
        value.ValueChanged += (sender, args) => UpdateSample();
        var ok = new MusicButton(apply, 109, true) { Left = 130, Top = 278, DialogResult = DialogResult.OK };
        AcceptButton = ok;
        Controls.AddRange(new Control[] { wheel, value, sample, ok });
        UpdateSample();
    }

    static double Angle(double x, double y) { return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360; }

    void SelectAt(Point point)
    {
        double x = point.X - 110;
        double y = point.Y - 110;
        hue = Angle(x, y);
        saturation = Math.Min(1, Math.Sqrt(x * x + y * y) / 108);
        UpdateSample();
        wheel.Invalidate();
    }

    void UpdateSample()
    {
        Color color = SelectedColor;
        sample.Text = string.Format("#{0:X6}", color.ToArgb() & 0xffffff);
        sample.BackColor = color;
        sample.ForeColor = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) > 142 ? Color.Black : Color.White;
    }

    internal static Color Hsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double mid = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        double min = value - chroma;
        double[][] sectors = { new[] { chroma, mid, 0d }, new[] { mid, chroma, 0d }, new[] { 0d, chroma, mid },
            new[] { 0d, mid, chroma }, new[] { mid, 0d, chroma }, new[] { chroma, 0d, mid } };
        var rgb = sectors[(int)(hue / 60) % 6];
        return Color.FromArgb((int)Math.Round((rgb[0] + min) * 255), (int)Math.Round((rgb[1] + min) * 255),
            (int)Math.Round((rgb[2] + min) * 255));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) image.Dispose();
        base.Dispose(disposing);
    }
}

sealed class LyricTrack
{
    readonly string plain;
    readonly KeyValuePair<double, string>[] timed;
    Dictionary<int, TimedWord[]> wordTiming = new Dictionary<int, TimedWord[]>();

    LyricTrack(string plain, KeyValuePair<double, string>[] timed)
    {
        this.plain = plain;
        this.timed = timed;
    }

    [System.Runtime.Serialization.DataContract]
    sealed class Response
    {
        [System.Runtime.Serialization.DataMember(Name = "plainLyrics")]
        public string Plain { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "syncedLyrics")]
        public string Timed { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "lyricsfile")]
        public string Lyricsfile { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "trackName")]
        public string Title { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "artistName")]
        public string Artist { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "albumName")]
        public string Album { get; set; }
        [System.Runtime.Serialization.DataMember(Name = "duration")]
        public double Duration { get; set; }
    }

    static string Normalize(string metadata)
    {
        return (metadata ?? "").Trim().Replace('’', (char)39);
    }

    internal static string CleanArtist(string artist)
    {
        return Normalize(artist).Split(new[] { " — " }, 2, StringSplitOptions.None)[0];
    }

    static string MainTitle(string title)
    {
        return System.Text.RegularExpressions.Regex.Replace(Normalize(title),
            @"\s*[\(\[][^\)\]]*(?:live|remaster|acoustic|version|edit|feat\.)[^\)\]]*[\)\]]\s*$",
            "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    }

    public static async System.Threading.Tasks.Task<LyricTrack> LoadAsync(string title, string artist, string album, double duration)
    {
        title = Normalize(title);
        artist = CleanArtist(artist);
        if (title.Length == 0 || artist.Length == 0) return null;
        string query = "track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
        string signature = query + "&album_name=" + Uri.EscapeDataString(album ?? "");
        if (duration > 0) signature += "&duration=" + Math.Round(duration).ToString(CultureInfo.InvariantCulture);

        var exact = await DownloadAsync<Response>("get?" + signature);
        if (exact != null && !string.IsNullOrWhiteSpace(exact.Timed)) return Parse(exact.Plain, exact.Timed, exact.Lyricsfile);

        var results = await DownloadAsync<Response[]>("search?" + query);
        var match = FindMatch(results, title, artist, album, duration);
        string shortTitle = MainTitle(title);
        if (match == null && !string.Equals(title, shortTitle, StringComparison.OrdinalIgnoreCase))
        {
            var alternatives = await DownloadAsync<Response[]>("search?track_name=" + Uri.EscapeDataString(shortTitle) +
                "&artist_name=" + Uri.EscapeDataString(artist));
            match = FindMatch(alternatives, title, artist, album, duration);
        }
        match = match ?? exact;
        return match == null || (string.IsNullOrWhiteSpace(match.Plain) && string.IsNullOrWhiteSpace(match.Timed))
            ? null : Parse(match.Plain, match.Timed, match.Lyricsfile);
    }

    static Response FindMatch(Response[] results, string title, string artist, string album, double duration)
    {
        Response best = null;
        int bestScore = int.MinValue;
        if (results == null) return null;
        foreach (var response in results)
        {
            if (response == null || !string.Equals(MainTitle(response.Title), MainTitle(title), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(CleanArtist(response.Artist), artist, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(response.Timed) && string.IsNullOrWhiteSpace(response.Plain)) continue;
            double difference = duration <= 0 || response.Duration <= 0 ? 0 : Math.Abs(duration - response.Duration);
            if (difference > 12) continue;
            int score = string.IsNullOrWhiteSpace(response.Timed) ? 0 : 30;
            if (string.Equals(Normalize(response.Title), title, StringComparison.OrdinalIgnoreCase)) score += 25;
            if (string.Equals(Normalize(response.Album), Normalize(album), StringComparison.OrdinalIgnoreCase)) score += 12;
            score -= (int)Math.Round(difference * 3);
            if (score <= bestScore) continue;
            best = response;
            bestScore = score;
        }
        return best;
    }

    static async System.Threading.Tasks.Task<T> DownloadAsync<T>(string path) where T : class
    {
        using (var client = new System.Net.WebClient())
        {
            client.Headers[System.Net.HttpRequestHeader.UserAgent] = "DaybetterLEDStrip/1.0";
            try
            {
                var request = client.DownloadDataTaskAsync(new Uri("https://lrclib.net/api/" + path));
                using (var timeout = new System.Threading.CancellationTokenSource(7500))
                using (timeout.Token.Register(client.CancelAsync))
                {
                    byte[] bytes = await request;
                    using (var data = new MemoryStream(bytes))
                        return (T)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T)).ReadObject(data);
                }
            }
            catch (System.Net.WebException error)
            {
                var response = error.Response as System.Net.HttpWebResponse;
                if (response == null) throw;
                using (response)
                    if (response.StatusCode != System.Net.HttpStatusCode.NotFound &&
                        response.StatusCode != System.Net.HttpStatusCode.BadRequest) throw;
                return null;
            }
        }
    }

    internal static LyricTrack Parse(string plain, string lrc, string lyricsfile = null)
    {
        var rows = new List<KeyValuePair<double, string>>();
        var stamp = new System.Text.RegularExpressions.Regex(@"\[(\d+):(\d{2}(?:\.\d{1,3})?)\]");
        foreach (string line in (lrc ?? "").Split('\n'))
        {
            string words = stamp.Replace(line, "").Trim();
            foreach (System.Text.RegularExpressions.Match time in stamp.Matches(line))
            {
                double seconds = double.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture) * 60 +
                    double.Parse(time.Groups[2].Value, CultureInfo.InvariantCulture);
                rows.Add(new KeyValuePair<double, string>(seconds, words));
            }
        }
        rows.Sort((a, b) => a.Key.CompareTo(b.Key));
        var track = new LyricTrack(plain, rows.ToArray());
        track.wordTiming = ReadWordTiming(lyricsfile);
        return track;
    }

    sealed class TimedWord
    {
        public int Start = -1;
        public string Text;
    }

    static Dictionary<int, TimedWord[]> ReadWordTiming(string source)
    {
        var result = new Dictionary<int, TimedWord[]>();
        if (string.IsNullOrWhiteSpace(source) || source.Length > 1024 * 1024) return result;
        var fieldPattern = new System.Text.RegularExpressions.Regex(@"^(?<space> *)(?<item>- )?(?<key>[a-z_]+):[ ]?(?<value>.*)$");
        int lineIndent = -1, wordIndent = -1, lineStart = -1;
        bool inLines = false, inWords = false;
        var words = new List<TimedWord>();
        TimedWord current = null;
        Action save = () =>
        {
            if (lineStart >= 0 && words.Count > 0 && words.TrueForAll(w => w.Start >= 0 && w.Text != null))
                result[lineStart] = words.ToArray();
            words.Clear();
        };

        foreach (string raw in source.Split('\n'))
        {
            var field = fieldPattern.Match(raw.TrimEnd('\r'));
            if (!field.Success) continue;
            int indent = field.Groups["space"].Length;
            bool item = field.Groups["item"].Success;
            string key = field.Groups["key"].Value;
            string value = field.Groups["value"].Value;
            if (indent == 0 && !item)
            {
                if (inLines) break;
                inLines = key == "lines";
                continue;
            }
            if (!inLines) continue;
            if (item && (lineIndent < 0 || indent == lineIndent))
            {
                save();
                lineIndent = indent;
                lineStart = -1;
                wordIndent = -1;
                current = null;
                inWords = false;
            }
            else if (item && inWords && (wordIndent < 0 || indent == wordIndent))
            {
                wordIndent = indent;
                current = new TimedWord();
                words.Add(current);
            }
            int time;
            if (key == "words" && !item && indent == lineIndent + 2) inWords = true;
            else if (key == "start_ms" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out time))
            {
                if (current != null) current.Start = time;
                else if (!inWords) lineStart = time;
            }
            else if (key == "text" && current != null) current.Text = ReadWordText(value);
        }
        save();
        return result;
    }

    static string ReadWordText(string value)
    {
        if (value.Length > 1 && value[0] == (char)39 && value[value.Length - 1] == (char)39)
            return value.Substring(1, value.Length - 2).Replace("''", "'");
        if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            try
            {
                using (var input = new MemoryStream(Encoding.UTF8.GetBytes(value)))
                    return (string)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(string)).ReadObject(input);
            }
            catch (System.Runtime.Serialization.SerializationException) { return null; }
        }
        if (value.IndexOfAny(new[] { '|', '>', '&', '*', '[', ']', '{', '}' }) >= 0) return null;
        return value;
    }

    public int HighlightLength(double seconds, string displayedLine)
    {
        int index = Array.FindLastIndex(timed, item => item.Key <= seconds);
        if (index < 0) return -1;
        TimedWord[] words;
        int key = (int)Math.Round(timed[index].Key * 1000);
        if (!wordTiming.TryGetValue(key, out words) || string.Concat(Array.ConvertAll(words, w => w.Text)) != displayedLine)
            return -1;

        int count = 0;
        foreach (var word in words)
        {
            if (word.Start > (seconds - 0.36) * 1000) break;
            count += word.Text.Length;
        }
        return count;
    }

    public string Upcoming(double seconds, int count)
    {
        int index = Array.FindLastIndex(timed, line => line.Key <= seconds) + 1;
        var lines = new List<string>();
        for (; index < timed.Length && lines.Count < count; index++)
            if (!string.IsNullOrWhiteSpace(timed[index].Value)) lines.Add(timed[index].Value);
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    public string At(double seconds)
    {
        if (timed.Length == 0) return plain ?? "";
        int index = Array.FindLastIndex(timed, line => line.Key <= seconds);
        return index < 0 ? "" : timed[index].Value;
    }
}


static class UiTypeface
{
    static readonly string Display = FindDisplay();

    static string FindDisplay()
    {
        using (var fonts = new System.Drawing.Text.InstalledFontCollection())
        {
            foreach (var family in fonts.Families)
                if (family.Name == "Arial Rounded MT Bold" && family.IsStyleAvailable(FontStyle.Regular))
                    return family.Name;
            foreach (var family in fonts.Families)
                if (family.Name == "Segoe UI Variable Display") return family.Name;
        }
        return "Segoe UI Semibold";
    }

    public static Font Create(float size, bool display)
    {
        return new Font(display ? Display : "Segoe UI", size, FontStyle.Regular);
    }
}

sealed class LyricsDisplay : Control
{
    readonly Font compactCurrent = UiTypeface.Create(15, true);
    readonly Font compactNext = UiTypeface.Create(11, false);
    readonly Font largeCurrent = UiTypeface.Create(31, true);
    readonly Font largeNext = UiTypeface.Create(23, true);
    readonly System.Windows.Forms.Timer motion = new System.Windows.Forms.Timer { Interval = 16 };
    readonly System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
    string current = "";
    string next = "";
    string previous = "";
    int highlight = -1;
    double progress = 1;

    public LyricsDisplay()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleRole = AccessibleRole.StaticText;
        motion.Tick += (sender, args) =>
        {
            progress = Math.Min(1, elapsed.Elapsed.TotalMilliseconds / 175);
            Invalidate();
            if (progress == 1) motion.Stop();
        };
    }

    public int UpcomingCapacity
    {
        get
        {
            var window = FindForm();
            if (window == null || window.WindowState != FormWindowState.Maximized) return 1;
            int rowHeight = Math.Max(1, (int)Math.Ceiling(largeNext.GetHeight() * 2));
            return Math.Max(4, Math.Min(16, (Height - (int)largeCurrent.GetHeight() - 18) / rowHeight + 1));
        }
    }

    public void SetLines(string line, string upcoming, bool animate, int highlightLength = -1)
    {
        if (current == line && next == upcoming && highlight == highlightLength) return;
        highlight = highlightLength;
        if (current != line)
        {
            previous = current;
            current = line;
            if (animate && Visible && !string.IsNullOrEmpty(line))
            {
                progress = 0;
                elapsed.Restart();
                motion.Start();
            }
            else
            {
                progress = 1;
                motion.Stop();
            }
        }
        next = upcoming;
        AccessibleName = current;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        bool large = FindForm() != null && FindForm().WindowState == FormWindowState.Maximized;
        var currentFont = large ? largeCurrent : compactCurrent;
        var nextFont = large ? largeNext : compactNext;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float width = Math.Max(1, Width - 15);
        int travel = large ? 33 : 17;
        float inset = large ? 7 : 1;
        double eased = 1 - Math.Pow(1 - progress, 3);
        if (progress < 1 && !string.IsNullOrEmpty(previous))
            Draw(e.Graphics, previous, currentFont, inset - (float)(travel * eased), width,
                Color.FromArgb((int)(112 * (1 - eased)), 244, 245, 249));
        float currentY = inset + (float)((1 - eased) * travel);
        var currentBounds = new RectangleF(3, currentY, width, 1600);
        var active = Color.FromArgb((int)(203 + 52 * eased), 244, 245, 249);
        if (highlight < 0) Draw(e.Graphics, current, currentFont, currentY, width, active);
        else
        {
            Draw(e.Graphics, current, currentFont, currentY, width, Color.FromArgb(155, 225, 228, 235));
            if (highlight > 0 && current.Length > 0)
            {
                using (var format = new StringFormat())
                {
                    format.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, Math.Min(highlight, current.Length)) });
                    var regions = e.Graphics.MeasureCharacterRanges(current, currentFont, currentBounds, format);
                    var saved = e.Graphics.Save();
                    try
                    {
                        e.Graphics.SetClip(regions[0], System.Drawing.Drawing2D.CombineMode.Intersect);
                        Draw(e.Graphics, current, currentFont, currentY, width, active);
                    }
                    finally
                    {
                        e.Graphics.Restore(saved);
                        foreach (var region in regions) region.Dispose();
                    }
                }
            }
        }
        float currentHeight = string.IsNullOrEmpty(current) ? 0 :
            e.Graphics.MeasureString(current, currentFont, (int)width).Height;
        float nextY = currentY + currentHeight + (large ? 14 : 3);
        Draw(e.Graphics, next, nextFont, nextY, width, Color.FromArgb(181, 225, 228, 235));
    }

    static void Draw(Graphics graphics, string text, Font font, float y, float width, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        using (var brush = new SolidBrush(color))
            graphics.DrawString(text, font, brush, new RectangleF(3, y, width, 1600));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            motion.Dispose();
            compactCurrent.Dispose();
            compactNext.Dispose();
            largeCurrent.Dispose();
            largeNext.Dispose();
        }
        base.Dispose(disposing);
    }
}

sealed class LanguagePopup : Control
{
    const int FullHeight = 118;
    const int RowHeight = 35;
    readonly string[] locales;
    readonly int selected;
    readonly Color tone;
    readonly Color accent;
    readonly Action<int> select;
    readonly Font textFont = UiTypeface.Create(10, false);
    readonly System.Windows.Forms.Timer reveal = new System.Windows.Forms.Timer { Interval = 16 };
    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    int hot = -1;
    int visibleHeight;

    public LanguagePopup(string[] locales, int selected, Color tone, Color accent, Action<int> select)
    {
        this.locales = locales;
        this.selected = selected;
        this.tone = ListeningWindow.Blend(Color.FromArgb(21, 23, 28), tone, 0.66);
        this.accent = accent;
        this.select = select;
        Width = 169;
        Height = FullHeight;
        AccessibleRole = AccessibleRole.MenuPopup;
        AccessibleName = "Language";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        reveal.Tick += (sender, args) =>
        {
            double progress = Math.Min(1, clock.Elapsed.TotalMilliseconds / 145);
            visibleHeight = (int)Math.Round(FullHeight * (1 - Math.Pow(1 - progress, 3)));
            Invalidate();
            if (progress == 1) reveal.Stop();
        };
    }

    public void Open(Form owner, Control anchor)
    {
        Point edge = owner.PointToClient(anchor.PointToScreen(new Point(anchor.Width, anchor.Height + 5)));
        Location = new Point(Math.Max(0, edge.X - Width), Math.Min(edge.Y, owner.ClientSize.Height - FullHeight));
        owner.Controls.Add(this);
        BringToFront();
        Focus();
        clock.Start();
        reveal.Start();
    }

    public void Close() { Dispose(); }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsDisposed && !Disposing) Close();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int row = e.Y < 6 || e.Y >= visibleHeight ? -1 : (e.Y - 6) / RowHeight;
        hot = row < locales.Length ? row : -1;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && hot >= 0) Choose(hot);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keys)
    {
        if (keys == Keys.Escape) { Close(); return true; }
        if (keys == Keys.Down || keys == Keys.Up)
        {
            hot = (hot < 0 ? selected : hot + (keys == Keys.Down ? 1 : -1) + locales.Length) % locales.Length;
            Invalidate();
            return true;
        }
        if (keys == Keys.Enter) { Choose(hot < 0 ? selected : hot); return true; }
        return base.ProcessCmdKey(ref msg, keys);
    }

    void Choose(int index) { select(index); Close(); }

    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (visibleHeight < 4) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var shape = ArtworkView.Rounded(new Rectangle(0, 0, Width - 1, visibleHeight - 1), 15))
        using (var fill = new SolidBrush(tone))
        using (var edge = new Pen(Color.FromArgb(95, accent)))
        {
            e.Graphics.FillPath(fill, shape);
            var saved = e.Graphics.Save();
            e.Graphics.SetClip(shape);
            for (int i = 0; i < locales.Length; i++)
            {
                var row = new Rectangle(1, 6 + i * RowHeight, Width - 2, RowHeight);
                if (i == hot)
                    using (var hover = new SolidBrush(ListeningWindow.Blend(tone, accent, 0.11)))
                        e.Graphics.FillRectangle(hover, row);
                using (var ink = new SolidBrush(i == selected ? accent : Color.WhiteSmoke))
                using (var align = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString(locales[i], textFont, ink, new RectangleF(14, row.Y, Width - 46, row.Height), align);
                    if (i == selected)
                        e.Graphics.DrawString("✓", textFont, ink, new RectangleF(Width - 30, row.Y, 20, row.Height), align);
                }
            }
            e.Graphics.Restore(saved);
            e.Graphics.DrawPath(edge, shape);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { reveal.Dispose(); textFont.Dispose(); }
        base.Dispose(disposing);
    }
}

static class WindowCorners
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(IntPtr window)
    {
        int rounded = 2;
        DwmSetWindowAttribute(window, 33, ref rounded, sizeof(int));
    }
}

sealed class SceneFrame : IDisposable
{
    IntPtr dc;
    IntPtr stock;
    IntPtr bitmap;
    int width;
    int height;

    public void Replace(Bitmap frame)
    {
        if (dc == IntPtr.Zero)
        {
            dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return;
        }
        IntPtr next = frame.GetHbitmap();
        IntPtr previous = SelectObject(dc, next);
        if (previous == IntPtr.Zero || previous == new IntPtr(-1))
        {
            DeleteObject(next);
            return;
        }
        if (bitmap == IntPtr.Zero) stock = previous;
        else DeleteObject(bitmap);
        bitmap = next;
        width = frame.Width;
        height = frame.Height;
    }

    public bool Paint(Graphics graphics, int x, int y, int targetWidth, int targetHeight)
    {
        if (bitmap == IntPtr.Zero || targetWidth <= 0 || targetHeight <= 0) return false;
        IntPtr target = graphics.GetHdc();
        try
        {
            SetStretchBltMode(target, 3);
            return StretchBlt(target, x, y, targetWidth, targetHeight, dc, 0, 0, width, height, 0x00cc0020);
        }
        finally { graphics.ReleaseHdc(target); }
    }

    public void Dispose()
    {
        if (dc == IntPtr.Zero) return;
        if (bitmap != IntPtr.Zero)
        {
            SelectObject(dc, stock);
            DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
        }
        DeleteDC(dc);
        dc = IntPtr.Zero;
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr source);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr obj);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern int SetStretchBltMode(IntPtr dc, int mode);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool StretchBlt(IntPtr dest, int x, int y, int width, int height,
        IntPtr source, int sx, int sy, int sourceWidth, int sourceHeight, int operation);
}




sealed class AboutController : Form
{
    const string Email = "427leoyang@gmail.com";
    readonly int language;

    public AboutController(int language, Color tone)
    {
        this.language = language;
        Text = T("反馈", "Feedback", "回饋");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(465, 358);
        BackColor = ListeningWindow.Blend(Color.FromArgb(22, 25, 32), tone, 0.28);
        ForeColor = Color.WhiteSmoke;
        Font = UiTypeface.Create(10, false);
        var title = new Label { Left = 24, Top = 22, Width = 417, Height = 34,
            Font = UiTypeface.Create(20, true), Text = "Daybetter LED Strip" };
        var credit = new Label { Left = 27, Top = 67, Width = 406, Height = 22,
            Text = T("作者", "Author", "作者") + "   Leo Yang    ·    v" + Program.Release };
        var education = new Label { Left = 27, Top = 94, Width = 410, Height = 44,
            Text = T("加州大学戴维斯分校 · 大三 · 计算机工程", "UC Davis · Junior · Computer Engineering", "加州大學戴維斯分校 · 大三 · 電腦工程") };
        var contact = new TextBox { Left = 27, Top = 142, Width = 320, ReadOnly = true,
            Text = Email, BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = Color.WhiteSmoke };
        var guidance = new Label { Left = 27, Top = 177, Width = 410, Height = 44,
            ForeColor = Color.FromArgb(208, 211, 221),
            Text = T("复制报告模板，填写后贴到 GitHub Issues 或邮件。", "Copy the report template, fill it in, then paste it into GitHub Issues or an email.", "複製回報範本，填寫後貼到 GitHub Issues 或電子郵件。") };
        var copyReport = new MusicButton(T("复制报告模板", "Copy bug report", "複製回報範本"), 166, true) { Left = 26, Top = 221 };
        var copyEmail = new MusicButton(T("复制邮箱", "Copy email", "複製電子郵件"), 145) { Left = 202, Top = 221 };
        var notice = new Label { Left = 27, Top = 326, Width = 412, Height = 23 };
        var donate = new MusicButton(T("支持作者", "Support the author", "支持作者"), 150) { Left = 26, Top = 272 };
        var donationHelp = new Label { Left = 188, Top = 280, Width = 244, Height = 39,
            Text = T("复制作者邮箱，联系获取捐助方式。", "Copies the author's email for donation enquiries.", "複製作者電子郵件，聯絡取得捐助方式。") };
        donate.Click += (sender, args) => Copy(Email, notice);
        copyReport.Click += (sender, args) => Copy(BugReport(), notice);
        copyEmail.Click += (sender, args) => Copy(Email, notice);
        Controls.AddRange(new Control[] { title, credit, education, contact, guidance, copyReport, copyEmail, donate, donationHelp, notice });
    }

    string T(string simplified, string english, string traditional)
    {
        return language == 0 ? english : language == 2 ? traditional : simplified;
    }

    string BugReport()
    {
        return "## " + T("问题描述", "What happened", "問題描述") + "\r\n\r\n## " +
            T("复现步骤", "Steps to reproduce", "重現步驟") + "\r\n1. \r\n2. \r\n\r\n## " +
            T("预期结果", "Expected result", "預期結果") + "\r\n\r\n## " +
            T("运行环境", "Environment", "執行環境") + "\r\n- Daybetter LED Strip v" + Program.Release +
            "\r\n- Windows " + Environment.OSVersion.Version + "\r\n- Daybetter model / firmware: \r\n";
    }

    void Copy(string text, Label status)
    {
        try
        {
            Clipboard.SetText(text);
            status.Text = T("已复制。", "Copied to clipboard.", "已複製。");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            status.Text = T("剪贴板忙，请再试一次。", "Clipboard is busy. Try again.", "剪貼簿忙碌，請再試一次。");
        }
    }
}




static class WindowsOutputVolume
{
    public static void Read(out int level, out bool muted)
    {
        int readLevel = 0;
        bool readMuted = false;
        WithEndpoint(endpoint =>
        {
            float scalar;
            endpoint.GetMasterVolumeLevelScalar(out scalar);
            endpoint.GetMute(out readMuted);
            readLevel = (int)Math.Round(scalar * 100);
        });
        level = readLevel;
        muted = readMuted;
    }

    public static void Set(int level)
    {
        WithEndpoint(endpoint => endpoint.SetMasterVolumeLevelScalar(level / 100f, IntPtr.Zero));
    }

    public static void ToggleMute()
    {
        WithEndpoint(endpoint =>
        {
            bool muted;
            endpoint.GetMute(out muted);
            endpoint.SetMute(!muted, IntPtr.Zero);
        });
    }

    static void WithEndpoint(Action<IEndpointVolume> action)
    {
        var enumerator = (IDeviceEnumerator)new DeviceEnumerator();
        IAudioDevice device = null;
        object endpoint = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(0, 1, out device);
            Guid interfaceId = typeof(IEndpointVolume).GUID;
            device.Activate(ref interfaceId, 23, IntPtr.Zero, out endpoint);
            action((IEndpointVolume)endpoint);
        }
        finally
        {
            if (endpoint != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(endpoint);
            if (device != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(device);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(enumerator);
        }
    }

    [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class DeviceEnumerator { }

    [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
        System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    interface IDeviceEnumerator
    {
        void EnumAudioEndpoints(int flow, uint stateMask, out IntPtr collection);
        void GetDefaultAudioEndpoint(int flow, int role, out IAudioDevice device);
    }

    [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("D666063F-1587-4E43-81F1-B948E807363F"),
        System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioDevice
    {
        void Activate(ref Guid interfaceId, uint context, IntPtr activationParameters,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] out object endpoint);
    }

    [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
        System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    interface IEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr callback);
        void UnregisterControlChangeNotify(IntPtr callback);
        void GetChannelCount(out uint channels);
        void SetMasterVolumeLevel(float decibels, IntPtr eventContext);
        void SetMasterVolumeLevelScalar(float volume, IntPtr eventContext);
        void GetMasterVolumeLevel(out float decibels);
        void GetMasterVolumeLevelScalar(out float volume);
        void SetChannelVolumeLevel(uint channel, float decibels, IntPtr eventContext);
        void SetChannelVolumeLevelScalar(uint channel, float volume, IntPtr eventContext);
        void GetChannelVolumeLevel(uint channel, out float decibels);
        void GetChannelVolumeLevelScalar(uint channel, out float volume);
        void SetMute([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool mute, IntPtr eventContext);
        void GetMute([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] out bool mute);
    }
}

