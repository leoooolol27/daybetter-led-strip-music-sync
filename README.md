# Daybetter LED Strip · Music Sync Controller

A portable Windows app that matches a Daybetter P065 Bluetooth LED strip to the artwork playing in the Apple Music desktop app.

[中文使用说明](README.zh-CN.md) · [Releases](https://github.com/leoooolol27/daybetter-led-strip-music-sync/releases)

## Features

- **Music sync.** The controller selects one static color per song. Pausing, closing Apple Music, or stopping sync leaves the strip in its current state.
- **Artwork color.** Similar colors are grouped to find the dominant hue. A mostly black cover can use a visible contrasting color. An almost entirely black cover sends an off command.
- **Preview and manual controls.** Preview a cover and its color without sending Bluetooth commands. Choose a manual color using the wheel or a hex code. The app can also request power and brightness changes.
- **Per-album overrides.** Save a color that looks right on your strip, then reuse it whenever that album plays. Reset returns to artwork-based selection.
- **Lyrics.** Optional LRCLIB lookup follows Apple Music playback. Compatible word timestamps enable word highlighting, with line highlighting when only line timing is available.
- **Desktop interface.** Rotating blurred artwork, adaptive window layout, tray controls, Windows volume and mute, and English, 简体中文 and 繁體中文.

## Start here

1. On a 64-bit Windows 11 PC, download the portable `DaybetterLEDStrip-1.0.0.exe` from [Releases](https://github.com/leoooolol27/daybetter-led-strip-music-sync/releases) and run it.
2. Enable Bluetooth and power on one nearby Daybetter P065. If the phone app is holding the connection, disconnect it or close that app.
3. Play a song in the Apple Music **desktop app** in the same Windows session. Choose **Start music sync**. Choose **Artwork preview** if you want to inspect the target without changing the strip.
4. Hover over the color code to see a swatch. Use **Raw RGB** to compare the cover color with the default saturation-enhanced output.
5. For manual control, stop sync or preview before selecting **Set color**. To override an album color, preview or play the album, enter the desired hex, and click **Save album color**. Use **Reset** to remove it.
6. Select **Lyrics** to opt in to online lookup. Maximizing the window shows more upcoming lines. **System volume** adjusts the default Windows audio output and can affect other apps.
7. Close or minimize to use the notification area. Double-click the tray icon to reopen, or use its menu to toggle sync or quit. Keep the PC awake for background operation.

## P065 color limitations

Testing focused on **P065 firmware 1.2.4**. The test strip changed in **visible steps** even with the official phone app color wheel, and direct RGB commands showed similar color differences. Nearby orange values, for example, may look nearly identical or appear yellowish. The observations indicate a limit in the tested controller or strip response. The exact hardware cause and number of color steps have not been measured.

The swatch represents the RGB value requested by the software. Actual appearance depends on the strip and viewing conditions. **Some songs look almost unchanged** because their covers have similar dominant colors, the chosen color is dark or low-saturation, or different RGB values land in the same visible hardware step. Songs from the same album can intentionally reuse the same color. The animated window and the physical strip update independently.

For more visible changes, compare **Raw RGB**, try nearby hex values manually, or save a per-album override. Power and brightness behavior can vary by firmware. Bluetooth write success alone does not verify the physical result. Other Daybetter models need separate compatibility testing.

## Data and troubleshooting

- **No song or artwork:** Confirm Apple Music is actually playing in the same desktop session and try restarting preview.
- **No P065 found:** Check power and Windows Bluetooth, move the controller closer, and disconnect the phone app. The app expects one nearby P065.
- **Color on screen but strip unchanged:** Make sure music sync is on. Artwork preview displays colors without sending commands. Try a manual primary color to check the connection.
- **Missing or delayed lyrics:** LRCLIB coverage and timing vary by recording and release. The app sends the track title, artist, and available album and duration when you enable lyric lookup. The timeline comes from Apple Music through Windows. The Apple Music play queue is not exposed.
- **Local settings:** Language and album colors are stored in your Windows user profile.

## Build and feedback

Run `build.cmd` on 64-bit Windows 11. It uses the bundled .NET Framework compiler and WinRT metadata and produces `DaybetterLEDStrip.exe`. `MakeIcon.ps1` recreates the icon.

Use **Feedback → Copy bug report** and paste the completed report into GitHub Issues. Include your Windows version, controller model and firmware, and reproduction steps. For color issues, include the requested hex value and the actual color you saw.

**Leo Yang** · UC Davis · Junior, Computer Engineering · [427leoyang@gmail.com](mailto:427leoyang@gmail.com)

[Daybetter BLE protocol](https://github.com/grimsteel/daybetter-led-strip#protocol) · [Protocol variants](https://github.com/DoudGeorges/daybetter-leds/blob/main/PROTOCOL.md) · [LRCLIB API](https://lrclib.net/docs)
