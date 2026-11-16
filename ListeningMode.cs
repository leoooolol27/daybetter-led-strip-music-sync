using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

static class Program
{
    internal const string Release = "1.0.0";
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            Checks.Run(args.Length > 1 ? args[1] : null);
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ListeningWindow());
    }
}

sealed class ListeningWindow : Form
{
    readonly AppSettings settings = new AppSettings();
    readonly MusicButton about = new MusicButton("About", 118);
    readonly MusicButton language = new MusicButton("English", 118);
    LanguagePopup languagePopup;
    readonly MusicButton picker = new MusicButton("选色", 74);
    readonly MusicButton power = new MusicButton("开灯", 74);
    readonly MusicButton off = new MusicButton("关灯", 74);
    readonly MusicButton saveColor = new MusicButton("保存专辑灯色", 162);
    readonly MusicButton clearColor = new MusicButton("清除", 82);
    readonly SceneSlider outputVolume = new SceneSlider { Width = 130, Height = 38, Minimum = 0 };
    readonly MusicButton muteOutput = new MusicButton("Mute", 96);
    readonly System.Windows.Forms.Timer volumeTimer = new System.Windows.Forms.Timer { Interval = 75 };
    readonly Label volumePercent = new SceneLabel { Width = 52, Height = 38, TextAlign = ContentAlignment.MiddleLeft };
    bool refreshingVolume;
    bool outputMuted;
    readonly SceneSlider brightness = new SceneSlider { Width = 130, Height = 38 };
    readonly MusicButton applyBrightness = new MusicButton("设亮度", 102);
    readonly NotifyIcon tray = new NotifyIcon { Visible = true };
    readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
    readonly List<Action> translations = new List<Action>();
    string albumKey;
    readonly System.Diagnostics.Stopwatch rotationTime = System.Diagnostics.Stopwatch.StartNew();
    bool exiting;
    bool hiding;
    bool inPreview;
    static readonly Color Primary = Color.FromArgb(248, 246, 252);
    static readonly Color Secondary = Color.FromArgb(193, 183, 203);
    readonly MusicButton toggle = new MusicButton("开启灯带联动", 172, true);
    readonly MusicButton preview = new MusicButton("只预览封面", 158);
    readonly MusicButton testColor = new MusicButton("发送测试色", 122);
    readonly Label status = new SceneLabel { Text = "联动已关闭", Dock = DockStyle.Fill };
    readonly Label track = new SceneLabel { Text = "等待播放", Dock = DockStyle.Fill };
    readonly Label albumName = new SceneLabel { AutoEllipsis = true, Dock = DockStyle.Fill, TextOpacity = 175 };
    readonly Label swatch = new SceneLabel { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    readonly TextBox testHex = new TextBox { Text = "#FF0000", Width = 105, MaxLength = 7, CharacterCasing = CharacterCasing.Upper };
    readonly CheckBox rawRgb = new CheckBox { Text = "使用原始 RGB", AutoSize = true };
    readonly ArtworkView artwork = new ArtworkView { Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer layoutMotion = new System.Windows.Forms.Timer { Interval = 24 };
    readonly System.Diagnostics.Stopwatch layoutClock = new System.Diagnostics.Stopwatch();
    double layoutScale;
    double layoutStart;
    double layoutTarget;
    readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer { Interval = 16 };
    readonly System.Diagnostics.Stopwatch animationTime = new System.Diagnostics.Stopwatch();
    internal Color Accent = Color.FromArgb(218, 228, 244);
    readonly SceneFrame backdrop = new SceneFrame();
    Bitmap ambientFrame;
    internal Color ButtonTone = SceneSurface.ControlColor;
    readonly MusicButton lyricsToggle = new MusicButton("Lyrics", 105);
    readonly LyricsDisplay lyricsDisplay = new LyricsDisplay { Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer lyricsTimer = new System.Windows.Forms.Timer { Interval = 100 };
    double lyricPosition;
    double lyricDuration;
    DateTimeOffset lyricSnapshot;
    bool lyricPlaying;
    readonly SceneTable lyricsPanel = new SceneTable { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
    bool onlineLyrics;
    string lyricsKey;
    LyricTrack lyrics;
    Color displayedColor = Color.Empty;
    readonly ToolTip colorTip = new ToolTip { OwnerDraw = true, InitialDelay = 220, ReshowDelay = 0, AutoPopDelay = 30000 };


    CancellationTokenSource mode;
    bool testing;
    bool closing;

    public ListeningWindow()
    {
        Text = "Daybetter LED Strip · Music Sync Controller";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        tray.Icon = Icon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(924, 626);
        MinimumSize = new System.Drawing.Size(850, 648);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        ForeColor = Primary;
        BackColor = Color.FromArgb(18, 21, 27);
        DoubleBuffered = true;

        var layout = new SceneTable { Dock = DockStyle.Fill, BackColor = Color.Transparent,
            Padding = new Padding(32, 27, 32, 26), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 87));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var heading = Caption("Daybetter LED Strip", 25, Primary, true);
        var header = new SceneTable { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        header.Margin = Padding.Empty;
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        header.Controls.Add(heading, 0, 0);
        var subtitle = Caption("音乐联动控制器", 11, Secondary, false);
        header.Controls.Add(subtitle, 0, 1);
        var locales = new[] { "English", "简体中文", "繁體中文" };
        language.Text = locales[settings.Language];
        Deactivate += (sender, args) => { if (languagePopup != null) languagePopup.Close(); };
        language.Click += (sender, args) =>
        {
            if (languagePopup != null) { languagePopup.Close(); return; }
            languagePopup = new LanguagePopup(locales, settings.Language, ButtonTone, Accent, index =>
            {
                settings.Language = index;
                settings.Save();
                language.Text = locales[index];
                RefreshLanguage();
            });
            languagePopup.Disposed += (source, closed) => languagePopup = null;
            languagePopup.Open(this, language);
        };

        language.AccessibleName = null;
        language.Anchor = AnchorStyles.Right;
        language.Height = 30;
        language.Margin = new Padding(3, 3, 7, 3);
        header.Controls.Add(language, 1, 0);
        about.Anchor = AnchorStyles.Right;
        about.Height = language.Height;
        about.Margin = language.Margin;
        header.Controls.Add(about, 1, 1);
        Bind(about, "反馈", "Feedback", "回饋");
        about.Click += (sender, args) =>
        {
            using (var dialog = new AboutController(settings.Language, ButtonTone))
            {
                dialog.Icon = Icon;
                dialog.ShowDialog(this);
            }
        };
        Bind(subtitle, "音乐联动控制器", "Music Sync Controller", "音樂連動控制器");
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 2);
        artwork.Margin = new Padding(3, 0, 21, 8);
        layout.Controls.Add(artwork, 0, 1);

        var details = new SceneTable { Dock = DockStyle.Fill, BackColor = Color.Transparent, 
            ColumnCount = 1, RowCount = 8, Margin = new Padding(10, 0, 0, 0) };
        foreach (int height in new[] { 21, 29, 26, 29, 47, 23, 28 })
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var nowPlaying = Caption("当前播放", 10, Secondary, false);
        Bind(nowPlaying, "当前播放", "Now playing", "目前播放");
        details.Controls.Add(nowPlaying, 0, 0);
        StyleLabel(track, 17, Primary, true);
        details.Controls.Add(track, 0, 1);
        StyleLabel(albumName, 15, Color.White, true);
        details.Controls.Add(albumName, 0, 2);
        Action fitTrack = () =>
        {
            if (track.Width < 10) return;
            int height = TextRenderer.MeasureText(track.Text, track.Font, new System.Drawing.Size(track.Width, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
            int rowHeight = Math.Max(29, Math.Min(55, height + track.Margin.Vertical));
            if (details.RowStyles[1].Height != rowHeight) details.RowStyles[1].Height = rowHeight;
        };
        track.TextChanged += (sender, args) => fitTrack();
        details.SizeChanged += (sender, args) => fitTrack();
        swatch.Margin = new Padding(3, 0, 4, 7);
        StyleLabel(swatch, 10, Secondary, false);
        swatch.TextAlign = ContentAlignment.MiddleLeft;
        swatch.AutoSize = true;
        swatch.Dock = DockStyle.None;
        swatch.Anchor = AnchorStyles.Left;
        colorTip.Popup += (sender, args) => args.ToolTipSize = new System.Drawing.Size(146, 49);
        colorTip.Draw += (sender, args) =>
        {
            args.Graphics.Clear(Color.FromArgb(24, 28, 35));
            using (var brush = new SolidBrush(displayedColor))
                args.Graphics.FillRectangle(brush, 10, 10, 30, 29);
            args.Graphics.DrawRectangle(Pens.SlateGray, 10, 10, 30, 29);
            TextRenderer.DrawText(args.Graphics, Hex(displayedColor), Font,
                new Rectangle(49, 0, 94, 49), Color.White, TextFormatFlags.VerticalCenter);
        };
        swatch.MouseLeave += (sender, args) => colorTip.Hide(swatch);
        details.Controls.Add(swatch, 0, 3);

        var controls = new SceneFlow { Dock = DockStyle.Fill, BackColor = Color.Transparent, WrapContents = false };
        controls.Controls.Add(toggle);
        controls.Controls.Add(preview);
        details.Controls.Add(controls, 0, 4);
        var deviceLabel = Caption("蓝牙 · Daybetter P065", 10, Secondary, false);
        Bind(deviceLabel, "蓝牙 · Daybetter P065", "Bluetooth · Daybetter P065", "藍牙 · Daybetter P065");
        details.Controls.Add(deviceLabel, 0, 5);
        StyleLabel(status, 10, Primary, false);
        details.Controls.Add(status, 0, 6);
        lyricsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        lyricsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var lyricsControls = new SceneFlow { Dock = DockStyle.Fill, WrapContents = false };
        var lyricsSource = Caption("LRCLIB", 8, Secondary, false);
        lyricsSource.AutoSize = true;
        lyricsSource.Dock = DockStyle.None;
        lyricsSource.Margin = new Padding(6, 8, 0, 0);
        Bind(lyricsSource, "经 LRCLIB 查询歌名和艺人", "Track and artist sent to LRCLIB", "經 LRCLIB 查詢歌名和藝人");
        lyricsToggle.Height = 30;
        lyricsControls.Controls.Add(lyricsToggle);
        lyricsControls.Controls.Add(lyricsSource);
        lyricsPanel.Controls.Add(lyricsControls, 0, 0);
        lyricsPanel.Controls.Add(lyricsDisplay, 0, 1);
        lyricsTimer.Tick += (sender, args) => PaintLyrics();
        lyricsTimer.Start();
        lyricsToggle.Click += (sender, args) =>
        {
            onlineLyrics = !onlineLyrics;
            lyrics = null;
            lyricsKey = null;
            lyricsDisplay.SetLines("", "", false);
            RefreshLanguage();
        };
        details.Controls.Add(lyricsPanel, 0, 7);
        Resize += (sender, args) =>
        {
            lyricsDisplay.Invalidate();
            if (languagePopup != null) languagePopup.Close();
        };
        layout.Controls.Add(details, 1, 1);

        var testControls = new SceneFlow { Dock = DockStyle.Fill, BackColor = Color.Transparent, WrapContents = false };
        var testLabel = Caption("手动调色", 10, Secondary, false);
        testLabel.Dock = DockStyle.None;
        testLabel.AutoSize = true;
        testLabel.Margin = new Padding(3, 13, 12, 0);
        testHex.BackColor = SceneSurface.ControlColor;
        testHex.ForeColor = Primary;
        testHex.BorderStyle = BorderStyle.None;
        testHex.Location = new System.Drawing.Point(13, 10);
        testHex.AccessibleName = "RGB hex";
        Bind(testLabel, "手动调色", "Manual color", "手動調色");
        testControls.Controls.Add(testLabel);
        var hexField = new SceneInput { Width = 114, Height = 38, Margin = new Padding(3, 2, 7, 3) };
        testHex.Width = hexField.Width - 27;
        hexField.Controls.Add(testHex);
        testControls.Controls.Add(hexField);
        testControls.Controls.Add(picker);
        testControls.Controls.Add(testColor);
        testControls.Controls.Add(power);
        testControls.Controls.Add(off);
        rawRgb.BackColor = Color.Transparent;
        rawRgb.ForeColor = Secondary;
        rawRgb.Margin = new Padding(20, 11, 0, 0);
        testControls.Controls.Add(rawRgb);
        layout.Controls.Add(testControls, 0, 2);
        layout.SetColumnSpan(testControls, 2);
        var moreControls = new SceneFlow { Dock = DockStyle.Fill, BackColor = Color.Transparent, WrapContents = false };
        var dimLabel = Caption("亮度", 10, Secondary, false);
        dimLabel.Dock = DockStyle.None;
        dimLabel.AutoSize = true;
        dimLabel.Margin = new Padding(3, 12, 7, 0);
        Bind(dimLabel, "亮度", "Brightness", "亮度");
        brightness.AccessibleName = "Brightness";

        brightness.Margin = new Padding(0, 2, 6, 0);
        moreControls.Controls.AddRange(new Control[] { dimLabel, brightness, applyBrightness, saveColor, clearColor });
        layout.Controls.Add(moreControls, 0, 3);
        layout.SetColumnSpan(moreControls, 2);
        var audioRow = new SceneFlow { Dock = DockStyle.Fill, WrapContents = false };
        var audioLabel = Caption("", 10, Secondary, false);
        audioLabel.Dock = DockStyle.None;
        audioLabel.AutoSize = true;
        audioLabel.Margin = new Padding(3, 12, 7, 0);
        Bind(audioLabel, "系统音量", "System volume", "系統音量");
        outputVolume.AccessibleName = "System volume";
        outputVolume.Margin = new Padding(0, 2, 6, 0);
        StyleLabel(volumePercent, 10, Secondary, false);
        volumePercent.Dock = DockStyle.None;
        audioRow.Controls.AddRange(new Control[] { audioLabel, outputVolume, volumePercent, muteOutput });
        layout.Controls.Add(audioRow, 0, 4);
        layout.SetColumnSpan(audioRow, 2);
        Controls.Add(layout);
        var animatedButtons = new[] { language, about, toggle, preview, lyricsToggle, picker, testColor,
            power, off, applyBrightness, saveColor, clearColor, muteOutput };
        var normalWidths = animatedButtons.Select(button => button.Width).ToArray();
        var normalHeights = animatedButtons.Select(button => button.Height).ToArray();
        layoutMotion.Tick += (sender, args) =>
        {
            double fraction = Math.Min(1, layoutClock.Elapsed.TotalMilliseconds / 240);
            double t = 1 - Math.Pow(1 - fraction, 3);
            layoutScale = layoutStart + (layoutTarget - layoutStart) * t;

            layout.SuspendLayout();
            header.SuspendLayout();
            controls.SuspendLayout();
            lyricsControls.SuspendLayout();
            testControls.SuspendLayout();
            moreControls.SuspendLayout();
            audioRow.SuspendLayout();
            try
            {
                int gutter = 32 + (int)Math.Round(Math.Min(104, ClientSize.Width * 0.037) * layoutScale);
                layout.Padding = new Padding(gutter, 27 + (int)Math.Round(15 * layoutScale), gutter, 26);
                layout.ColumnStyles[0].Width = (float)(42 - 6 * layoutScale);
                layout.ColumnStyles[1].Width = 100 - layout.ColumnStyles[0].Width;
                layout.RowStyles[0].Height = (float)(87 + 18 * layoutScale);
                header.ColumnStyles[1].Width = (float)(142 + 34 * layoutScale);
                header.RowStyles[0].Height = header.RowStyles[1].Height = (float)(41 + 9 * layoutScale);
                layout.RowStyles[2].Height = layout.RowStyles[3].Height = (float)(51 + 8 * layoutScale);
                layout.RowStyles[4].Height = (float)(45 + 8 * layoutScale);
                details.RowStyles[4].Height = (float)(47 + 9 * layoutScale);
                lyricsPanel.RowStyles[0].Height = (float)(36 + 12 * layoutScale);
                for (int i = 0; i < animatedButtons.Length; i++)
                {
                    animatedButtons[i].Width = (int)Math.Round(normalWidths[i] * (1 + 0.14 * layoutScale));
                    animatedButtons[i].Height = normalHeights[i] + (int)Math.Round(7 * layoutScale);
                }
            }
            finally
            {
                audioRow.ResumeLayout();
                moreControls.ResumeLayout();
                testControls.ResumeLayout();
                lyricsControls.ResumeLayout();
                controls.ResumeLayout();
                header.ResumeLayout();
                layout.ResumeLayout(true);
            }
            if (fraction >= 1) layoutMotion.Stop();
        };
        Resize += (sender, args) =>
        {
            if (WindowState == FormWindowState.Minimized) return;
            double next = WindowState == FormWindowState.Maximized ? 1 : 0;
            if (layoutTarget == next) return;
            layoutStart = layoutScale;
            layoutTarget = next;
            layoutClock.Restart();
            layoutMotion.Start();
        };
        volumeTimer.Tick += (sender, args) =>
        {
            volumeTimer.Stop();
            try { WindowsOutputVolume.Set(outputVolume.Value); }
            catch (COMException) { RefreshOutputVolume(); }
        };
        outputVolume.ValueChanged += (sender, args) =>
        {
            volumePercent.Text = outputVolume.Value + "%";
            if (!refreshingVolume) volumeTimer.Start();
        };
        muteOutput.Click += (sender, args) =>
        {
            try { WindowsOutputVolume.ToggleMute(); }
            catch (COMException) { }
            RefreshOutputVolume();
        };
        Activated += (sender, args) => { if (!volumeTimer.Enabled) RefreshOutputVolume(); };
        audioRow.MouseEnter += (sender, args) => { if (!volumeTimer.Enabled) RefreshOutputVolume(); };
        Bind(picker, "选色", "Color", "選色");
        Bind(testColor, "设置灯色", "Set color", "設定燈色");
        Bind(power, "开灯", "On", "開燈");
        Bind(off, "关灯", "Off", "關燈");
        Bind(applyBrightness, "设亮度", "Apply", "設亮度");
        Bind(saveColor, "保存专辑灯色", "Save album color", "儲存專輯燈色");
        Bind(clearColor, "清除覆盖", "Reset", "清除覆寫");
        Bind(rawRgb, "原始 RGB", "Raw RGB", "原始 RGB");
        picker.Click += (sender, args) =>
        {
            Color seed;
            if (!TryColor(out seed)) seed = Color.OrangeRed;
            using (var dialog = new WheelDialog(seed, T("选择灯色", "Choose light color", "選擇燈色"),
                T("使用", "Use color", "使用")))
                if (dialog.ShowDialog(this) == DialogResult.OK) testHex.Text = Hex(dialog.SelectedColor);
        };
        saveColor.Click += (sender, args) => SetOverride(false);
        clearColor.Click += (sender, args) => SetOverride(true);
        power.Click += async (sender, args) => await SendManualAsync(0x11, 1);
        off.Click += async (sender, args) => await SendManualAsync(0x11, 0);
        applyBrightness.Click += async (sender, args) => await SendManualAsync(0x13, (byte)brightness.Value);
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += (sender, args) => ShowFromTray();
        Resize += (sender, args) => { if (WindowState == FormWindowState.Minimized && Visible) HideAnimated(); };
        RefreshLanguage();
        toggle.Click += Toggle;
        preview.Click += Toggle;
        testColor.Click += TestColor;
        animation.Interval = 32;
        animation.Tick += (sender, args) =>
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            double fraction = Math.Min(1, animationTime.Elapsed.TotalMilliseconds / 620);
            double progress = 1 - Math.Pow(1 - fraction, 3);

            float angle = (float)(rotationTime.Elapsed.TotalSeconds * 12 % 360);
            artwork.SetFrame(progress);
            RenderBackdrop(angle);
            Invalidate(true);


        };
        Shown += (sender, args) => WindowCorners.Apply(Handle);
        FormClosed += (sender, args) =>
        {
            animation.Dispose();
            layoutMotion.Dispose();
            volumeTimer.Dispose();
            lyricsTimer.Dispose();
            if (ambientFrame != null) ambientFrame.Dispose();
            backdrop.Dispose();
            tray.Dispose();
            if (languagePopup != null) languagePopup.Dispose();
            colorTip.Dispose();
            trayMenu.Dispose();
        };
        FormClosing += (sender, args) =>
        {
            if (!exiting && args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                HideAnimated();
                return;
            }
            if (mode == null) return;
            args.Cancel = true;
            closing = true;
            mode.Cancel();
        };
    }

    void RefreshOutputVolume()
    {
        refreshingVolume = true;
        try
        {
            int level;
            WindowsOutputVolume.Read(out level, out outputMuted);
            outputVolume.Enabled = muteOutput.Enabled = true;
            outputVolume.Value = level;
            volumePercent.Text = level + "%";
        }
        catch (COMException)
        {
            outputVolume.Enabled = muteOutput.Enabled = false;
            volumePercent.Text = "—";
        }
        finally { refreshingVolume = false; }
        UpdateMuteLabel();
    }

    void UpdateMuteLabel()
    {
        muteOutput.Text = outputMuted ? T("取消静音", "Unmute", "取消靜音") : T("静音", "Mute", "靜音");
    }

    static Label Caption(string value, float size, Color color, bool bold)
    {
        var label = new SceneLabel { Text = value, Dock = DockStyle.Fill };
        StyleLabel(label, size, color, bold);
        return label;
    }

    static void StyleLabel(Label label, float size, Color color, bool bold)
    {
        label.Font = UiTypeface.Create(size, bold);
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        PaintScene(this, e.Graphics);
    }

    void RenderBackdrop(float angle)
    {
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (width < 2 || height < 2) return;

        double scale = Math.Min(0.32, 480d / Math.Max(width, height));
        int smallWidth = Math.Max(2, (int)Math.Round(width * scale));
        int smallHeight = Math.Max(2, (int)Math.Round(height * scale));
        if (ambientFrame == null || ambientFrame.Width != smallWidth || ambientFrame.Height != smallHeight)
        {
            if (ambientFrame != null) ambientFrame.Dispose();
            ambientFrame = new Bitmap(smallWidth, smallHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        }
        using (var canvas = Graphics.FromImage(ambientFrame))
        {
            canvas.Clear(BackColor);
            var bounds = new Rectangle(0, 0, smallWidth, smallHeight);
            if (artwork.DrawAmbient(canvas, bounds, angle))
                using (var shade = new LinearGradientBrush(bounds, Color.FromArgb(68, 13, 16, 24),
                    Color.FromArgb(206, 13, 16, 24), 24))
                    canvas.FillRectangle(shade, bounds);
        }
        backdrop.Replace(ambientFrame);
    }

    internal void PaintScene(Control control, Graphics graphics)
    {
        int x = 0;
        int y = 0;
        for (var child = control; child != this && child != null; child = child.Parent)
        {
            x += child.Left;
            y += child.Top;
        }
        if (backdrop.Paint(graphics, -x, -y, ClientSize.Width, ClientSize.Height)) return;
        using (var brush = new SolidBrush(BackColor)) graphics.FillRectangle(brush, control.ClientRectangle);
    }

    internal static Color Blend(Color a, Color b, double progress)
    {
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * progress),
            (int)Math.Round(a.G + (b.G - a.G) * progress),
            (int)Math.Round(a.B + (b.B - a.B) * progress));
    }

    void Animate()
    {
        var palette = artwork.GetTheme();
        Accent = palette[0];
        ButtonTone = palette[1];

        animationTime.Restart();
        animation.Start();
    }

    async void Toggle(object sender, EventArgs args)
    {
        if (mode != null)
        {
            toggle.Enabled = preview.Enabled = false;
            status.Text = T("正在关闭听歌模式…", "Stopping…", "正在停止…");
            mode.Cancel();
            return;
        }

        mode = new CancellationTokenSource();
        bool previewOnly = ReferenceEquals(sender, preview);
        inPreview = previewOnly;
        preview.Enabled = previewOnly;
        toggle.Enabled = !previewOnly;
        testColor.Enabled = testHex.Enabled = false;
        RefreshLanguage();
        try { await WatchAsync(previewOnly, mode.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = Errors.Describe(error); }
        finally
        {
            bool cancelled = mode.IsCancellationRequested;
            mode.Dispose();
            mode = null;
            lyricPlaying = false;
            if (cancelled) status.Text = T("听歌模式已关闭 · 灯带保持当前状态", "Stopped · Light unchanged", "已停止 · 燈帶維持原狀");
            RefreshLanguage();
            toggle.Enabled = preview.Enabled = testColor.Enabled = testHex.Enabled = true;
            if (closing) Close();
        }
    }

    async void TestColor(object sender, EventArgs args)
    {
        int rgb;
        if (mode != null || testing) return;
        if (testHex.Text.Length != 7 || testHex.Text[0] != '#' ||
            !int.TryParse(testHex.Text.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out rgb))
        {
            status.Text = T("请输入 #RRGGBB，例如 #FF0000。", "Enter #RRGGBB, for example #FF0000.", "請輸入 #RRGGBB，例如 #FF0000。");
            return;
        }

        testing = true;
        mode = new CancellationTokenSource();
        toggle.Enabled = testColor.Enabled = testHex.Enabled = preview.Enabled = false;
        status.Text = "正在连接 P065 并测试 " + testHex.Text + "…";
        try
        {
            var color = Color.FromArgb(unchecked((int)0xff000000) | rgb);
            var target = await DaybetterTarget.DiscoverAsync(mode.Token);
            using (var lamp = await DaybetterConnection.OpenAsync(target, mode.Token))
                await lamp.ApplyAsync(color, () => Task.FromResult(true), mode.Token);
            track.Text = T("手动调色", "Manual color", "手動調色");
            albumName.Text = "";
            Animate();
            ShowColor(color, color);
            status.Text = "已发送测试色号 " + testHex.Text + " · P065";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = Errors.Describe(error); }
        finally
        {
            mode.Dispose();
            mode = null;
            testing = false;
            toggle.Enabled = testColor.Enabled = testHex.Enabled = preview.Enabled = true;
            if (closing) Close();
        }
    }

    string T(string simplified, string english, string traditional)
    {
        return settings.Language == 0 ? english : settings.Language == 2 ? traditional : simplified;
    }

    void Bind(Control control, string simplified, string english, string traditional)
    {
        translations.Add(() => control.Text = T(simplified, english, traditional));
    }

    void RefreshLanguage()
    {
        foreach (var translate in translations) translate();
        UpdateMuteLabel();
        lyricsToggle.Text = onlineLyrics ? T("关歌词", "Hide lyrics", "關歌詞") : T("歌词", "Lyrics", "歌詞");
        if (track.Text == "等待播放" || track.Text == "等待播放中" || track.Text == "Waiting for playback")
            track.Text = T("等待播放", "Waiting for playback", "等待播放中");
        if (status.Text == "联动已关闭" || status.Text == "Sync is off" || status.Text == "連動已關閉")
            status.Text = T("联动已关闭", "Sync is off", "連動已關閉");
        bool running = mode != null && !testing;
        toggle.Text = running && !inPreview ? T("停止灯带联动", "Stop music sync", "停止燈帶連動") :
            T("开启灯带联动", "Start music sync", "開啟燈帶連動");
        preview.Text = running && inPreview ? T("结束封面预览", "Stop preview", "結束封面預覽") :
            T("只预览封面", "Artwork preview", "只預覽封面");
        tray.Text = "Daybetter LED Strip";
        trayMenu.Items.Clear();
        trayMenu.Items.Add(T("显示窗口", "Show window", "顯示視窗"), null, (sender, args) => ShowFromTray());
        trayMenu.Items.Add(T("开启或停止联动", "Toggle music sync", "開啟或停止連動"), null, (sender, args) =>
        {
            if (!testing && (!inPreview || mode == null || !mode.IsCancellationRequested)) Toggle(toggle, EventArgs.Empty);
        });
        trayMenu.Items.Add(T("退出", "Quit", "結束程式"), null, (sender, args) =>
        {
            exiting = true;

            Close();
        });
    }

    void HideAnimated()
    {
        if (hiding) return;
        hiding = true;
        if (languagePopup != null) languagePopup.Close();
        Hide();
        hiding = false;
    }

    void ShowFromTray()
    {
        if (hiding || exiting) return;
        WindowState = FormWindowState.Normal;
        Show();
        Activate();
    }

    static string Hex(Color color) { return string.Format("#{0:X6}", color.ToArgb() & 0xffffff); }

    bool TryColor(out Color color)
    {
        int rgb;
        if (testHex.Text.Length == 7 && testHex.Text[0] == '#' &&
            int.TryParse(testHex.Text.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out rgb))
        {
            color = Color.FromArgb(unchecked((int)0xff000000) | rgb);
            return true;
        }
        color = Color.Empty;
        return false;
    }

    void SetOverride(bool clear)
    {
        if (albumKey == null)
        {
            status.Text = T("请先播放并读取一张专辑。", "Play an album in Apple Music first.", "請先播放並讀取一張專輯。");
            return;
        }
        Color color;
        if (clear) settings.Overrides.Remove(albumKey);
        else if (TryColor(out color)) settings.Overrides[albumKey] = color.ToArgb();
        else
        {
            status.Text = T("请输入有效的 #RRGGBB。", "Enter a valid #RRGGBB value.", "請輸入有效的 #RRGGBB。");
            return;
        }
        settings.Save();
        status.Text = clear ? T("已清除专辑灯色。", "Album color cleared.", "已清除專輯燈色。") :
            T("已保存专辑灯色 " + testHex.Text, "Saved album color " + testHex.Text, "已儲存專輯燈色 " + testHex.Text);
    }

    async Task SendManualAsync(byte command, byte value)
    {
        if (mode != null || testing)
        {
            status.Text = T("请停止联动或预览后再手动控制。", "Stop sync or preview before using manual controls.", "請停止連動或預覽後再手動控制。");
            return;
        }
        testing = true;
        mode = new CancellationTokenSource();
        toggle.Enabled = preview.Enabled = false;
        try
        {
            var target = await DaybetterTarget.DiscoverAsync(mode.Token);
            byte[] state;
            using (var lamp = await DaybetterConnection.OpenAsync(target, mode.Token))
                state = await lamp.ControlAsync(command, value, mode.Token);
            status.Text = state == null ? T("已写入 · 控制器未返回状态", "Written · No device status", "已寫入 · 控制器未回傳狀態") :
                T("设备状态 ", "Device status ", "裝置狀態 ") + BitConverter.ToString(state);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = Errors.Describe(error); }
        finally
        {
            mode.Dispose();
            mode = null;
            testing = false;
            toggle.Enabled = preview.Enabled = true;
            if (closing) Close();
        }
    }

    async Task WatchAsync(bool previewOnly, CancellationToken token)
    {
        status.Text = T("正在读取 Windows 媒体会话…", "Reading Windows media session…", "正在讀取 Windows 媒體工作階段…");
        GlobalSystemMediaTransportControlsSessionManager manager;
        try { manager = await WindowsAsync.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync(), token); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            throw new InvalidOperationException("无法读取 Windows 媒体会话。请在 Apple Music 所在的 Windows 登录账户里启动程序。", error);
        }

        string lastTrack = null;
        string lastArtwork = null;
        Color lastColor = Color.Empty;
        string appliedTrack = null;
        Color appliedColor = Color.Empty;
        DaybetterTarget target = null;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int delay = 750;
            try
            {
                var session = manager.GetSessions().FirstOrDefault(s =>
                    s.SourceAppUserModelId.StartsWith("AppleInc.AppleMusicWin_", StringComparison.OrdinalIgnoreCase));
                if (!IsPlaying(session))
                {
                    lyricPlaying = false;
                    status.Text = T("等待 Apple Music 客户端播放 · 灯带保持当前状态", "Waiting for Apple Music · Light unchanged", "等待 Apple Music 播放 · 燈帶維持原狀");
                    appliedTrack = null;
                }
                else
                {
                    var media = await WindowsAsync.Wait(session.TryGetMediaPropertiesAsync(), token);
                    if (media == null || string.IsNullOrWhiteSpace(media.Title) || media.Thumbnail == null)
                    {
                        status.Text = T("等待当前歌曲的专辑封面 · 灯带保持当前状态", "Waiting for artwork · Light unchanged", "等待專輯封面 · 燈帶維持原狀");
                    }
                    else
                    {
                        string key = TrackKey(media);
                        UpdateLyrics(session, media, key);
                        string[] artist = (media.Artist ?? "").Split(new[] { " — " }, 2, StringSplitOptions.None);
                        track.Text = media.Title + (string.IsNullOrWhiteSpace(artist[0]) ? "" : "  ·  " + artist[0]);
                        albumName.Text = !string.IsNullOrWhiteSpace(media.AlbumTitle) ? media.AlbumTitle :
                            (artist.Length == 2 ? artist[1] : "");
                        // Re-read changed tracks and retry if Windows published metadata before artwork.
                        if (lastTrack != key)
                        {
                            byte[] bytes = await ReadArtworkAsync(media.Thumbnail, token);
                            using (var sha = System.Security.Cryptography.SHA256.Create())
                            {
                                string artwork = Convert.ToBase64String(sha.ComputeHash(bytes));
                                if (artwork != lastArtwork)
                                {
                                    lastColor = await Task.Run(() => Palette.Dominant(bytes), token);
                                    this.artwork.TransitionTo(bytes);
                                    Animate();
                                    lastArtwork = artwork;
                                }
                            }
                            lastTrack = key;
                        }
                        albumKey = string.IsNullOrWhiteSpace(media.AlbumTitle) ? null :
                            (string.IsNullOrWhiteSpace(media.AlbumArtist) ? media.Artist : media.AlbumArtist) + "\u001f" + media.AlbumTitle;
                        int saved;
                        bool overridden = albumKey != null && settings.Overrides.TryGetValue(albumKey, out saved);
                        Color outputColor = overridden ? Color.FromArgb(settings.Overrides[albumKey]) :
                            (rawRgb.Checked ? lastColor : Palette.LampColor(lastColor));
                        ShowColor(lastColor, outputColor);
                        if (previewOnly) status.Text = T("正在预览封面 · 灯带保持当前状态", "Artwork preview · Light unchanged", "正在預覽封面 · 燈帶維持原狀");
                        else if (appliedTrack != key || appliedColor != outputColor)
                        {
                            status.Text = T("正在连接 P065 灯带…", "Connecting to P065…", "正在連線至 P065 燈帶…");
                            if (target == null) target = await DaybetterTarget.DiscoverAsync(token);
                            using (var lamp = await DaybetterConnection.OpenAsync(target, token))
                            {
                                // Verify again after BLE discovery so a paused or superseded track cannot start a write.
                                if (await IsCurrentAsync(session, key, token))
                                {
                                    await lamp.ApplyAsync(outputColor, () => IsCurrentAsync(session, key, token), token);
                                    appliedTrack = key;
                                    appliedColor = outputColor;
                                    status.Text = lastColor.ToArgb() == Color.Black.ToArgb()
                                        ? T("已发送熄灯指令 · P065", "Power off sent · P065", "已傳送關燈指令 · P065") : T("已发送封面颜色 · P065", "Color sent · P065", "已傳送燈色 · P065");
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (StaleTrackException) { appliedTrack = null; }
            catch (Exception error)
            {
                status.Text = Errors.Describe(error) + T("  ·  稍后重试", " · Retrying soon", " · 稍後重試");
                delay = 4500;
            }
            await Task.Delay(delay, token);
        }
    }

    async void UpdateLyrics(GlobalSystemMediaTransportControlsSession session, GlobalSystemMediaTransportControlsSessionMediaProperties media, string key)
    {
        if (!onlineLyrics || !lyricsPanel.Visible) return;
        var timeline = session.GetTimelineProperties();
        if (timeline == null) return;
        lyricPosition = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
        lyricDuration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
        lyricSnapshot = timeline.LastUpdatedTime == default(DateTimeOffset) ? DateTimeOffset.UtcNow : timeline.LastUpdatedTime;
        lyricPlaying = true;
        if (lyricsKey == key) return;

        lyrics = null;
        lyricsKey = key;
        lyricsDisplay.SetLines(T("正在查找歌词…", "Looking up lyrics…", "正在查詢歌詞…"), "", false);
        try
        {
            var result = await LyricTrack.LoadAsync(media.Title, media.Artist, media.AlbumTitle,
                (timeline.EndTime - timeline.StartTime).TotalSeconds);
            if (IsDisposed || !onlineLyrics || lyricsKey != key) return;
            lyrics = result;
            if (result == null) lyricsDisplay.SetLines(T("未找到匹配的歌词", "No matching lyrics found", "未找到符合的歌詞"), "", false);
        }
        catch (Exception)
        {
            if (!IsDisposed && onlineLyrics && lyricsKey == key)
                lyricsDisplay.SetLines(T("查询失败 · 可停止后再试", "Lookup failed · stop and retry", "查詢失敗 · 可停止後再試"), "", false);
        }
    }

    void PaintLyrics()
    {
        if (!Visible || !onlineLyrics || !lyricsPanel.Visible || lyrics == null || !lyricPlaying) return;
        double elapsed = Math.Max(0, (DateTimeOffset.UtcNow - lyricSnapshot).TotalSeconds);
        double position = lyricPosition + elapsed + 0.48;
        if (lyricDuration > 0) position = Math.Min(lyricDuration, position);
        string current = lyrics.At(position);
        string upcoming = lyrics.Upcoming(position, lyricsDisplay.UpcomingCapacity);
        lyricsDisplay.SetLines(current, upcoming, true, lyrics.HighlightLength(position, current));
    }

    static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        return session != null && session.GetPlaybackInfo() != null &&
            session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    static string TrackKey(GlobalSystemMediaTransportControlsSessionMediaProperties media)
    {
        return string.Join("\u001f", media.Title, media.Artist, media.AlbumTitle, media.TrackNumber.ToString(), media.Subtitle);
    }

    static async Task<bool> IsCurrentAsync(GlobalSystemMediaTransportControlsSession session, string key, CancellationToken token)
    {
        if (!IsPlaying(session)) return false;
        var current = await WindowsAsync.Wait(session.TryGetMediaPropertiesAsync(), token);
        return current != null && TrackKey(current) == key && IsPlaying(session);
    }

    static async Task<byte[]> ReadArtworkAsync(IRandomAccessStreamReference thumbnail, CancellationToken token)
    {
        using (var stream = await WindowsAsync.Wait(thumbnail.OpenReadAsync(), token))
        {
            if (stream.Size == 0 || stream.Size > 20 * 1024 * 1024)
                throw new InvalidDataException("专辑封面为空或超过 20 MB。");
            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                var size = (uint)stream.Size;
                uint loaded = await WindowsAsync.Wait(reader.LoadAsync(size), token);
                if (loaded != size) throw new IOException("未能完整读取专辑封面。");
                var bytes = new byte[(int)size];
                reader.ReadBytes(bytes);
                return bytes;
            }
        }
    }

    void ShowColor(Color source, Color output)
    {
        swatch.BackColor = Color.Transparent;
        if (displayedColor != output) { displayedColor = output; colorTip.SetToolTip(swatch, Hex(output)); }
        swatch.ForeColor = Accent;
        swatch.Text = output.ToArgb() == Color.Black.ToArgb() ? T("#000000  ·  熄灯", "#000000 · Off", "#000000 · 關燈") :
            string.Format(T("封面 #{0:X6}  →  灯带 #{1:X6}", "Cover #{0:X6}  →  Light #{1:X6}", "封面 #{0:X6}  →  燈帶 #{1:X6}"), source.ToArgb() & 0xffffff, output.ToArgb() & 0xffffff);
    }
}

static class SceneSurface
{
    public static readonly Color ControlColor = Color.FromArgb(38, 47, 59);
    public static void Paint(Control control, PaintEventArgs e)
    {
        var window = control.FindForm() as ListeningWindow;
        if (window != null) window.PaintScene(control, e.Graphics);
        else e.Graphics.Clear(control.Parent == null ? Color.FromArgb(18, 21, 27) : control.Parent.BackColor);
    }
}
sealed class SceneTable : TableLayoutPanel
{
    public SceneTable() { DoubleBuffered = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }
}
sealed class SceneFlow : FlowLayoutPanel
{
    public SceneFlow() { DoubleBuffered = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }
}
sealed class SceneLabel : Label
{
    public SceneLabel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    public int TextOpacity = 255;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (TextOpacity == 255)
        {
            base.OnPaint(e);
            return;
        }
        if (string.IsNullOrEmpty(Text)) return;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var ink = new SolidBrush(Color.FromArgb(TextOpacity, ForeColor)))
        using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            e.Graphics.DrawString(Text, Font, ink, ClientRectangle, format);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }
}
sealed class SceneCheck : CheckBox
{
    public SceneCheck() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }
}

sealed class SceneInput : Panel
{
    public SceneInput()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        SceneSurface.Paint(this, e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = ArtworkView.Rounded(Rectangle.Inflate(ClientRectangle, -1, -1), 18))
        using (var brush = new SolidBrush(SceneSurface.ControlColor))
            e.Graphics.FillPath(brush, path);
    }
}

sealed class SceneSlider : Control
{
    int value = 65;
    public int Minimum = 1;
    public event EventHandler ValueChanged;
    public int Value
    {
        get { return value; }
        set
        {
            int next = Math.Max(Minimum, Math.Min(100, value));
            if (this.value == next) return;
            this.value = next;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    public SceneSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Slider;
        Cursor = Cursors.Hand;
    }

    protected override bool IsInputKey(Keys key)
    {
        return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(key);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) Value -= 1;
        else if (e.KeyCode == Keys.Right) Value += 1;
        else if (e.KeyCode == Keys.Home) Value = Minimum;
        else if (e.KeyCode == Keys.End) Value = 100;
        else return;
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        Capture = true;
        SelectAt(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left) SelectAt(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) Capture = false;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    void SelectAt(int x)
    {
        Value = Minimum + (int)Math.Round((100d - Minimum) * (x - 9) / Math.Max(1, Width - 18));
    }

    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var owner = FindForm() as ListeningWindow;
        Color accent = owner == null ? Color.White : owner.Accent;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int y = Height / 2;
        int x = 9 + (int)Math.Round((Width - 18) * (Value - Minimum) / (100d - Minimum));
        using (var rail = new Pen(Color.FromArgb(89, 111, 133), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            e.Graphics.DrawLine(rail, 9, y, Width - 9, y);
        using (var fill = new Pen(accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            e.Graphics.DrawLine(fill, 9, y, x, y);
        using (var knob = new SolidBrush(accent)) e.Graphics.FillEllipse(knob, x - 6, y - 6, 12, 12);
        if (Focused && ShowFocusCues)
            using (var ring = new Pen(accent)) e.Graphics.DrawEllipse(ring, x - 9, y - 9, 18, 18);
    }
}

sealed class MusicButton : Control, IButtonControl
{
    readonly bool primary;
    bool hovering;
    bool pressed;
    double pressProgress;
    readonly System.Windows.Forms.Timer feedback = new System.Windows.Forms.Timer { Interval = 16 };

    public MusicButton(string text, int width, bool primary = false)
    {
        this.primary = primary;
        Text = text;
        Width = width;
        Height = 40;
        Margin = new Padding(3, 1, 7, 3);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        MouseEnter += (sender, args) => { hovering = true; Invalidate(); };
        MouseLeave += (sender, args) => { hovering = false; pressed = false; feedback.Start(); Invalidate(); };
        MouseDown += (sender, args) => { if (args.Button == MouseButtons.Left) { pressed = true; feedback.Start(); } };
        MouseUp += (sender, args) => { pressed = false; feedback.Start(); };
        KeyDown += (sender, args) => { if (args.KeyCode == Keys.Space) { pressed = true; feedback.Start(); } };
        KeyUp += (sender, args) =>
        {
            pressed = false;
            feedback.Start();
            if (args.KeyCode == Keys.Space) PerformClick();
        };
        feedback.Tick += (sender, args) =>
        {
            double target = pressed ? 1 : 0;
            pressProgress += (target - pressProgress) * 0.36;
            if (Math.Abs(target - pressProgress) < 0.015)
            {
                pressProgress = target;
                feedback.Stop();
            }
            Invalidate();
        };
        GotFocus += (sender, args) => Invalidate();
        LostFocus += (sender, args) => Invalidate();
    }

    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value) { Invalidate(); }

    public void PerformClick()
    {
        if (Enabled && Visible) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var dialog = FindForm();
        if (dialog != null && DialogResult != DialogResult.None) dialog.DialogResult = DialogResult;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Enter && Focused)
        {
            PerformClick();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing && feedback != null) feedback.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { SceneSurface.Paint(this, e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        int inset = 1 + (int)Math.Round(pressProgress * 2);
        var bounds = Rectangle.Inflate(ClientRectangle, -inset, -inset);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = !Enabled ? Color.FromArgb(33, 40, 50) : primary
            ? (hovering ? Color.FromArgb(228, 234, 242) : Color.FromArgb(211, 223, 238))
            : (hovering ? Color.FromArgb(56, 65, 78) : SceneSurface.ControlColor);
        var owner = FindForm() as ListeningWindow;
        if (owner != null)
            fill = Enabled ? (primary ? owner.Accent : ListeningWindow.Blend(owner.ButtonTone, owner.Accent, hovering ? 0.18 : 0.04)) :
                ListeningWindow.Blend(owner.ButtonTone, owner.Accent, 0.08);
        fill = ListeningWindow.Blend(fill, Color.Black, pressProgress * 0.22);
        using (var path = ArtworkView.Rounded(bounds, 20))
        using (var brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(Color.FromArgb(235, 185, 203), 1.5f)) e.Graphics.DrawPath(pen, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? (primary ? Color.FromArgb(15, 23, 34) : Color.White) : Color.FromArgb(158, 148, 169),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

sealed class ArtworkView : Control
{
    Bitmap previous;
    Bitmap current;
    Bitmap ambient;
    Bitmap previousAmbient;
    Bitmap scaledCover;
    Bitmap scaledPrevious;
    int coverSize;
    double progress = 1;


    public ArtworkView()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        AccessibleName = "当前专辑封面";
    }

    public void TransitionTo(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        using (var image = Image.FromStream(stream))
        {
            var next = new Bitmap(image);
            if (previous != null) previous.Dispose();
            previous = current;
            if (previousAmbient != null) previousAmbient.Dispose();
            previousAmbient = ambient;
            ambient = new Bitmap(9, 9);
            using (var g = Graphics.FromImage(ambient))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, ambient.Width, ambient.Height);
            }
            current = next;
            ClearCoverCache();
            progress = 0;
        }
        Invalidate();
    }

    public void SetFrame(double value)
    {
        progress = value;

        if (progress == 1 && previous != null)
        {
            previous.Dispose();
            previous = null;
            if (scaledPrevious != null) scaledPrevious.Dispose();
            scaledPrevious = null;
            if (previousAmbient != null) previousAmbient.Dispose();
            previousAmbient = null;
        }
        Invalidate();
    }

    void EnsureCoverCache(int size)
    {
        if (coverSize == size) return;
        ClearCoverCache();
        coverSize = size;
        scaledCover = ResizeCover(current, size);
        scaledPrevious = ResizeCover(previous, size);
    }

    static Bitmap ResizeCover(Bitmap image, int size)
    {
        if (image == null) return null;
        var scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, 0, 0, size, size);
        }
        return scaled;
    }

    void ClearCoverCache()
    {
        if (scaledCover != null) scaledCover.Dispose();
        if (scaledPrevious != null) scaledPrevious.Dispose();
        scaledCover = scaledPrevious = null;
        coverSize = 0;
    }

    public Color[] GetTheme()
    {
        var neutral = Color.FromArgb(218, 228, 244);
        if (ambient == null) return new[] { neutral, SceneSurface.ControlColor };
        double total = 0, chromaTotal = 0, r = 0, g = 0, b = 0;
        for (int y = 0; y < ambient.Height; y++)
        for (int x = 0; x < ambient.Width; x++)
        {
            var sample = ambient.GetPixel(x, y);
            int high = Math.Max(sample.R, Math.Max(sample.G, sample.B));
            int low = Math.Min(sample.R, Math.Min(sample.G, sample.B));
            chromaTotal += high - low;
            double weight = (high - low) / 255d;
            r += sample.R * weight;
            g += sample.G * weight;
            b += sample.B * weight;
            total += weight;
        }
        if (total < 0.3 || chromaTotal / (ambient.Width * ambient.Height) < 13)
            return new[] { Color.FromArgb(227, 228, 230), Color.FromArgb(55, 57, 61) };

        var accent = Color.FromArgb((int)(r / total), (int)(g / total), (int)(b / total));
        float hue = accent.GetHue();
        Color light;
        Color tone;
        if (hue < 22 || hue >= 345) { light = Color.FromArgb(243, 214, 209); tone = Color.FromArgb(74, 37, 43); }
        else if (hue < 65) { light = Color.FromArgb(242, 225, 190); tone = Color.FromArgb(77, 55, 34); }
        else if (hue < 82) { light = Color.FromArgb(229, 230, 192); tone = Color.FromArgb(65, 66, 40); }
        else if (hue < 161) { light = Color.FromArgb(231, 219, 186); tone = Color.FromArgb(67, 46, 34); }
        else if (hue < 195) { light = Color.FromArgb(198, 230, 223); tone = Color.FromArgb(30, 66, 65); }
        else if (hue < 253) { light = Color.FromArgb(213, 225, 244); tone = Color.FromArgb(33, 49, 76); }
        else if (hue < 293) { light = Color.FromArgb(226, 216, 245); tone = Color.FromArgb(57, 43, 78); }
        else { light = Color.FromArgb(243, 218, 233); tone = Color.FromArgb(70, 42, 62); }
        return new[] { ListeningWindow.Blend(light, accent, 0.08), ListeningWindow.Blend(tone, accent, 0.10) };
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        SceneSurface.Paint(this, e);
    }

    public bool DrawAmbient(Graphics graphics, Rectangle bounds, float angle)
    {
        if (ambient == null) return false;
        var state = graphics.Save();
        graphics.TranslateTransform(bounds.Width / 2f, bounds.Height / 2f);
        graphics.RotateTransform(angle);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int size = (int)Math.Ceiling(Math.Sqrt((double)bounds.Width * bounds.Width + (double)bounds.Height * bounds.Height) * 1.16);
        var target = new Rectangle(-size / 2, -size / 2, size, size);
        if (previousAmbient != null) graphics.DrawImage(previousAmbient, target);
        using (var attributes = new System.Drawing.Imaging.ImageAttributes())
        {
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)progress });
            graphics.DrawImage(ambient, target, 0, 0, ambient.Width, ambient.Height, GraphicsUnit.Pixel, attributes);
        }
        graphics.Restore(state);
        return true;
    }

    internal static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int size = Math.Min(Width, Height) - 5;
        if (size < 4) return;

        var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        if (bounds.Width < 1) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.Bilinear;
        using (var path = Rounded(bounds, 17))
        using (var fill = new LinearGradientBrush(bounds, Color.FromArgb(72, 52, 79), Color.FromArgb(34, 25, 46), 52))
        {
            g.FillPath(fill, path);
            var saved = g.Save();
            g.SetClip(path);
            EnsureCoverCache(size);
            if (scaledPrevious != null) g.DrawImageUnscaled(scaledPrevious, bounds.Location);
            if (scaledCover != null && progress == 1) g.DrawImageUnscaled(scaledCover, bounds.Location);
            else if (scaledCover != null)
            {
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)progress };
                    attributes.SetColorMatrix(matrix);
                    g.DrawImage(scaledCover, bounds, 0, 0, size, size, GraphicsUnit.Pixel, attributes);
                }
            }
            else
            {
                using (var symbol = new Font("Segoe UI Symbol", 43))
                    TextRenderer.DrawText(g, "♫", symbol, bounds,
                    Color.FromArgb(218, 183, 217), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            g.Restore(saved);
            using (var outline = new Pen(Color.FromArgb(48, Color.White))) g.DrawPath(outline, path);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (previous != null) previous.Dispose();
            if (current != null) current.Dispose();
            ClearCoverCache();
            if (ambient != null) ambient.Dispose();
            if (previousAmbient != null) previousAmbient.Dispose();
        }
        base.Dispose(disposing);
    }
}

static class Errors
{
    public static string Describe(Exception error)
    {
        return error.InnerException == null ? error.Message : error.Message + " (" + error.InnerException.Message.Trim() + ")";
    }
}

static class WindowsAsync
{
    public static async Task<T> Wait<T>(IAsyncOperation<T> operation, CancellationToken token, int milliseconds = 10000)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Completed = (result, status) =>
        {
            try { completion.TrySetResult(result.GetResults()); }
            catch (Exception error) { completion.TrySetException(error); }
        };
        using (var timer = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var deadline = Task.Delay(milliseconds, timer.Token);
            if (await Task.WhenAny(completion.Task, deadline) == completion.Task)
            {
                timer.Cancel();
                return await completion.Task;
            }
        }
        completion.TrySetCanceled();
        try { operation.Cancel(); }
        catch (COMException) { }
        token.ThrowIfCancellationRequested();
        throw new TimeoutException("Windows 操作超时。");
    }
}

static class Palette
{
    const int HueBins = 24;
    const int Black = 24;
    const int Gray = 25;
    const int White = 26;

    public static Color LampColor(Color cover)
    {
        int peak = Math.Max(cover.R, Math.Max(cover.G, cover.B));
        int floor = Math.Min(cover.R, Math.Min(cover.G, cover.B));
        if (peak == 0) return Color.Black;
        double saturation = (double)(peak - floor) / peak;
        if (saturation < 0.28) return cover;

        // Keep the hue on the RGB color wheel while reducing the shared white component.
        double adjustedSaturation = saturation >= 0.70 ? 1 :
            Math.Min(1, saturation + (saturation - 0.28) * 0.85);
        double adjustedPeak = 255;
        double adjustedFloor = adjustedPeak * (1 - adjustedSaturation);
        double rangeScale = (adjustedPeak - adjustedFloor) / (peak - floor);
        return Color.FromArgb(
            (int)Math.Round(adjustedFloor + (cover.R - floor) * rangeScale),
            (int)Math.Round(adjustedFloor + (cover.G - floor) * rangeScale),
            (int)Math.Round(adjustedFloor + (cover.B - floor) * rangeScale));
    }

    public static Color Dominant(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        using (var image = Image.FromStream(stream, false, true))
            return Dominant(image);
    }

    public static Color Dominant(Image image)
    {
        const int sampleSize = 96;
        using (var sample = new Bitmap(sampleSize, sampleSize))
        {
            using (var graphics = Graphics.FromImage(sample))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.DrawImage(image, 0, 0, sampleSize, sampleSize);
            }
            var bins = new int[27, 4];
            for (int y = 0; y < sampleSize; y++)
            for (int x = 0; x < sampleSize; x++)
            {
                Color pixel = sample.GetPixel(x, y);
                if (pixel.A < 128) continue;
                int high = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                int low = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                int bin;
                if (high <= 24) bin = Black;
                else if (high - low <= 0.12 * high) bin = high >= 224 ? White : Gray;
                else bin = (int)(pixel.GetHue() / 15) % HueBins;
                bins[bin, 0]++;
                bins[bin, 1] += pixel.R;
                bins[bin, 2] += pixel.G;
                bins[bin, 3] += pixel.B;
            }

            // Count neighboring hue bins together, including reds on both sides of 0 degrees.
            int[] winner = new int[4];
            int[] contrasting = null;
            double bestContrast = 0;
            bool blackDominates = false;
            for (int bin = 0; bin < bins.GetLength(0); bin++)
            {
                var totals = new int[4];
                for (int component = 0; component < totals.Length; component++)
                {
                    totals[component] = bins[bin, component];
                    if (bin < HueBins)
                    {
                        totals[component] += bins[(bin + HueBins - 1) % HueBins, component];
                        totals[component] += bins[(bin + 1) % HueBins, component];
                    }
                }
                if (bin != Black && totals[0] > 0)
                {
                    int countInGroup = totals[0];
                    double r = (double)totals[1] / countInGroup;
                    double g = (double)totals[2] / countInGroup;
                    double b = (double)totals[3] / countInGroup;
                    double peak = Math.Max(r, Math.Max(g, b));
                    double chroma = peak - Math.Min(r, Math.Min(g, b));
                    double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                    double score = Math.Sqrt(countInGroup) * (0.35 * luminance + 0.30 * peak + 0.65 * chroma);
                    if (score > bestContrast)
                    {
                        bestContrast = score;
                        contrasting = totals;
                    }
                }
                if (totals[0] <= winner[0]) continue;
                blackDominates = bin == Black;
                if (blackDominates) Array.Clear(totals, 1, 3);
                winner = totals;
            }
            if (blackDominates && contrasting != null) winner = contrasting;
            int count = winner[0];
            if (count == 0) throw new InvalidDataException("专辑封面没有可取色的像素。");
            return Color.FromArgb(winner[1] / count, winner[2] / count, winner[3] / count);
        }
    }
}

sealed class DaybetterTarget
{

    public ulong Address;
    public BluetoothAddressType AddressType;

    public static async Task<DaybetterTarget> DiscoverAsync(CancellationToken token)
    {
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        var found = new System.Collections.Concurrent.ConcurrentDictionary<ulong, DaybetterTarget>();
        TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> received = (sender, advertisement) =>
        {

            if (advertisement.Advertisement.ServiceUuids.Contains(DaybetterConnection.Uuid("c065")) ||
                advertisement.Advertisement.LocalName.StartsWith("P065_", StringComparison.OrdinalIgnoreCase))
                found.TryAdd(advertisement.BluetoothAddress, new DaybetterTarget { Address = advertisement.BluetoothAddress, AddressType = advertisement.BluetoothAddressType });
        };
        watcher.Received += received;
        try
        {
            watcher.Start();
            await Task.Delay(3400, token);
            var matches = found.Values.ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) throw new IOException("Multiple P065 controllers found. Leave one controller powered on.");
            throw new IOException("No P065 found. Check Bluetooth, strip power, and close the phone app.");
        }
        finally
        {
            watcher.Stop();
            watcher.Received -= received;
        }
    }
}

sealed class StaleTrackException : Exception { }

sealed class DaybetterConnection : IDisposable
{
    readonly BluetoothLEDevice device;
    readonly GattDeviceService service;
    readonly GattCharacteristic write;
    readonly GattCharacteristic notify;
    readonly object responseLock = new object();
    TaskCompletionSource<byte[]> response;
    byte expectedCommand;
    bool authenticated;
    static readonly byte[] Chip = Encoding.ASCII.GetBytes("CCHIP");

    DaybetterConnection(BluetoothLEDevice device, GattDeviceService service, GattCharacteristic write, GattCharacteristic notify)
    {
        this.device = device;
        this.service = service;
        this.write = write;
        this.notify = notify;
    }

    public static Guid Uuid(string id) { return Guid.Parse("0000" + id + "-0000-1000-8000-00805f9b34fb"); }

    public static async Task<DaybetterConnection> OpenAsync(DaybetterTarget target, CancellationToken token)
    {
        var device = await WindowsAsync.Wait(BluetoothLEDevice.FromBluetoothAddressAsync(target.Address, target.AddressType), token);
        if (device == null) throw new IOException("Windows could not open P065. Close the Daybetter app on your phone and retry.");
        GattDeviceService service = null;
        DaybetterConnection connection = null;
        try
        {
            var services = await WindowsAsync.Wait(device.GetGattServicesForUuidAsync(Uuid("e065"), BluetoothCacheMode.Uncached), token);
            service = services.Services.FirstOrDefault();
            foreach (var duplicate in services.Services.Skip(1)) duplicate.Dispose();
            if (services.Status != GattCommunicationStatus.Success || service == null)
                throw new IOException("无法读取 P065 的 E065 蓝牙服务 (" + services.Status + ")。");
            var characteristics = await WindowsAsync.Wait(service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached), token);
            var writer = characteristics.Characteristics.FirstOrDefault(c => c.Uuid == Uuid("a065"));
            var notifier = characteristics.Characteristics.FirstOrDefault(c => c.Uuid == Uuid("f065"));
            if (writer == null || notifier == null)
                throw new IOException("P065 未提供预期的 A065/F065 蓝牙接口。");
            connection = new DaybetterConnection(device, service, writer, notifier);
            await connection.AuthenticateAsync(token);
            return connection;
        }
        catch
        {
            if (connection != null) connection.Dispose();
            else
            {
                if (service != null) service.Dispose();
                device.Dispose();
            }
            throw;
        }
    }

    async Task AuthenticateAsync(CancellationToken token)
    {
        notify.ValueChanged += Receive;
        var notificationMode = notify.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
            : GattClientCharacteristicConfigurationDescriptorValue.Indicate;
        var status = await WindowsAsync.Wait(notify.WriteClientCharacteristicConfigurationDescriptorAsync(notificationMode), token);
        if (status != GattCommunicationStatus.Success)
            throw new IOException("无法订阅 P065 蓝牙回复 (" + status + ")。");

        byte[] challenge = await ExchangeAsync(0x00, Chip, token, 1500);
        if (challenge == null) return;
        if (challenge.Length != 7 || !challenge.Take(5).SequenceEqual(Chip))
            throw new IOException("P065 返回了未知的蓝牙握手格式。");
        byte[] reply = Chip.Concat(new[] { (byte)(challenge[5] ^ 36), (byte)(challenge[6] ^ 76) }).ToArray();
        byte[] accepted = await ExchangeAsync(0x01, reply, token, 2500);
        if (accepted == null || accepted.Length != 6 || !accepted.Take(5).SequenceEqual(Chip) || accepted[5] != 0)
            throw new IOException("P065 蓝牙握手未通过。");
        authenticated = true;
    }

    async Task<byte[]> ExchangeAsync(byte command, byte[] payload, CancellationToken token, int milliseconds)
    {
        var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (responseLock)
        {
            expectedCommand = command;
            response = pending;
        }
        try
        {
            await WriteAsync(command, payload, token);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                if (await Task.WhenAny(pending.Task, Task.Delay(milliseconds, deadline.Token)) == pending.Task)
                {
                    deadline.Cancel();
                    return await pending.Task;
                }
            }
            token.ThrowIfCancellationRequested();
            return null;
        }
        finally
        {
            lock (responseLock) response = null;
        }
    }

    void Receive(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
        {
            byte[] frame = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(frame);
            if (frame.Length < 5 || frame[0] != 0xa1 || frame[2] != frame.Length - 2) return;
            int crc = Crc(frame, frame.Length - 2);
            if ((frame[frame.Length - 2] | (frame[frame.Length - 1] << 8)) != crc) return;
            lock (responseLock)
            {
                if (response != null && expectedCommand == frame[1])
                    response.TrySetResult(frame.Skip(3).Take(frame.Length - 5).ToArray());
            }
        }
    }

    public async Task ApplyAsync(Color color, Func<Task<bool>> stillPlaying, CancellationToken token)
    {
        if (!await stillPlaying()) throw new StaleTrackException();
        if (color.ToArgb() == Color.Black.ToArgb())
        {
            await SendControlAsync(0x11, new byte[] { 0 }, token);
            return;
        }

        // Apply power first because the controller can reload its previous color on power-up.
        await SendControlAsync(0x11, new byte[] { 1 }, token);
        if (!await stillPlaying()) throw new StaleTrackException();
        if (authenticated)
        {
            await SendControlAsync(0x12, new byte[] { 1, 0 }, token);
            if (!await stillPlaying()) throw new StaleTrackException();
        }
        var rgb = authenticated ? new[] { color.R, color.G, color.B, (byte)0 } : new[] { color.R, color.G, color.B };
        await SendControlAsync(0x15, rgb, token);
    }

    public async Task<byte[]> ControlAsync(byte command, byte value, CancellationToken token)
    {
        var before = await ReadStateAsync(token);
        bool known = IsKnownState(before);
        if (command == 0x11 && known && before[0] == value) return before;

        await SendControlAsync(command, new[] { value }, token);
        await Task.Delay(180, token);
        var after = await ReadStateAsync(token);
        if (command == 0x11 && known && IsKnownState(after) && after[0] != value)
        {
            await SendControlAsync(command, new[] { (byte)(1 - value) }, token);
            await Task.Delay(180, token);
            after = await ReadStateAsync(token);
        }
        if (command == 0x13 && known && before[0] == 1 && IsKnownState(after) && before[3] == after[3] && after[3] != value)
        {
            await SendControlAsync(0x12, new byte[] { 1, 0 }, token);
            await SendControlAsync(command, new[] { value }, token);
            await Task.Delay(180, token);
            after = await ReadStateAsync(token);
        }
        return after;
    }

    async Task<byte[]> ReadStateAsync(CancellationToken token)
    {
        byte[] state = await ExchangeAsync(0x10, new byte[] { 1 }, token, 450);
        if (state != null && state.Length >= 4) return state;
        return await ExchangeAsync(0x10, new byte[] { 0 }, token, 450);
    }

    static bool IsKnownState(byte[] state)
    {
        return state != null && state.Length == 14 && state[0] <= 1 && state[3] <= 100;
    }

    async Task SendControlAsync(byte command, byte[] payload, CancellationToken token)
    {
        var acknowledged = await ExchangeAsync(command, payload, token, 900);
        if ((!authenticated && (acknowledged == null || acknowledged.Length == 0)) || (acknowledged != null && acknowledged.Length == 1 && acknowledged[0] != 1))
            throw new IOException("P065 未确认蓝牙指令 0x" + command.ToString("X2") + "。");
    }

    async Task WriteAsync(byte command, byte[] payload, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var option = write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)
            ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse;
        using (var writer = new DataWriter())
        {
            writer.WriteBytes(Frame(command, payload));
            var status = await WindowsAsync.Wait(write.WriteValueAsync(writer.DetachBuffer(), option), token);
            if (status != GattCommunicationStatus.Success)
                throw new IOException("P065 蓝牙写入失败 (" + status + ")。");
        }
    }

    internal static byte[] Frame(byte command, byte[] payload)
    {
        var frame = new byte[payload.Length + 5];
        frame[0] = 0xa0;
        frame[1] = command;
        frame[2] = (byte)(payload.Length + 3);
        Array.Copy(payload, 0, frame, 3, payload.Length);
        int crc = Crc(frame, frame.Length - 2);
        frame[frame.Length - 2] = (byte)crc;
        frame[frame.Length - 1] = (byte)(crc >> 8);
        return frame;
    }

    static int Crc(byte[] bytes, int length)
    {
        int crc = 0xffff;
        for (int i = 0; i < length; i++)
        {
            crc ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xa001 : 0);
        }
        return crc;
    }

    public void Dispose()
    {
        notify.ValueChanged -= Receive;
        service.Dispose();
        device.Dispose();
    }
}

static class Checks
{
    public static void Run(string screenshot)
    {
        if (Palette.LampColor(Color.FromArgb(160, 41, 46)).ToArgb() != Color.FromArgb(255, 0, 11).ToArgb())
            throw new Exception("Red color wheel check failed");
        foreach (Color color in new[] { Color.Black, Color.White, Color.Gray, Color.Red, Color.Lime, Color.Blue })
            if (Palette.LampColor(color).ToArgb() != color.ToArgb()) throw new Exception("Exact color check failed: " + color);
        if (WheelDialog.Hsv(0, 1, 1).ToArgb() != Color.Red.ToArgb() || WheelDialog.Hsv(120, 1, 1).ToArgb() != Color.Lime.ToArgb()) throw new Exception("Color wheel check failed");
        var lines = LyricTrack.Parse(null, "[00:01.25]first\n[00:05.50][00:09.00]chorus");
        if (lines.At(1) != "" || lines.At(2) != "first" || lines.At(10) != "chorus" || lines.Upcoming(2, 1) != "chorus")
            throw new Exception("LRC timing check failed");
        var karaoke = LyricTrack.Parse(null, "[00:01.00]Hello world",
            "version: '1.0'\nlines:\n  - text: Hello world\n    start_ms: 1000\n    words:\n      - text: 'Hello '\n        start_ms: 1000\n      - text: world\n        start_ms: 1500\n");
        if (karaoke.HighlightLength(1.2, "Hello world") != 0 ||
            karaoke.HighlightLength(1.5, "Hello world") != 6 ||
            karaoke.HighlightLength(2, "Hello world") != 11 ||
            karaoke.HighlightLength(2, "Another recording") != -1)
            throw new Exception("Word timing or recording fallback check failed");
        CheckPacket(0x15, new byte[] { 255, 0, 0 }, "A0-15-06-FF-00-00-25-C0");
        CheckPacket(0x15, new byte[] { 255, 0, 0, 0 }, "A0-15-07-FF-00-00-00-3C-1B");
        CheckPacket(0x11, new byte[] { 0 }, "A0-11-04-00-70-E1");
        CheckPacket(0, Encoding.ASCII.GetBytes("CCHIP"), "A0-00-08-43-43-48-49-50-AC-CA");
        using (var image = new Bitmap(100, 100))
        {
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(12, 13, 15));
                graphics.FillRectangle(Brushes.Red, 70, 0, 30, 100);
            }
            var red = Palette.LampColor(Palette.Dominant(image));
            if (red.R < 220 || red.G > 40 || red.B > 40) throw new Exception("Black and red cover check failed");
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.Black);
            if (Palette.Dominant(image).ToArgb() != Color.Black.ToArgb()) throw new Exception("Solid black cover check failed");
        }
        if (screenshot != null)
        {
            using (var image = new Bitmap(screenshot))
            using (var cover = image.Clone(new Rectangle(321, 254, 798, 798), image.PixelFormat))
            {
                var color = Palette.Dominant(cover);
                Console.WriteLine("Reference cover: #{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
                if (color.GetHue() < 175 || color.GetHue() > 225 || color.GetSaturation() < 0.35)
                    throw new Exception("Expected a cyan-blue reference cover");
            }
        }
        Console.WriteLine("All checks passed");
    }

    static void CheckPacket(byte command, byte[] payload, string expected)
    {
        if (BitConverter.ToString(DaybetterConnection.Frame(command, payload)) != expected)
            throw new Exception("BLE frame mismatch for " + command);
    }
}

































