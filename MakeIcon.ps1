Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scale = [single]($size / 64.0)
    $g.ScaleTransform($scale,$scale)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(2,2,28,28,180,90)
    $path.AddArc(34,2,28,28,270,90)
    $path.AddArc(34,34,28,28,0,90)
    $path.AddArc(2,34,28,28,90,90)
    $path.CloseFigure()
    $bg = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(2,2,60,60), [Drawing.Color]::FromArgb(31,45,66), [Drawing.Color]::FromArgb(13,18,32), [single]65)
    $g.FillPath($bg,$path)
    $wave = [Drawing.Drawing2D.GraphicsPath]::new()
    $wave.AddBezier(34,43,34,35,15,36,14,47)
    $wave.AddBezier(14,47,13,54,29,56,33,48)
    $wave.AddBezier(33,48,35,44,34,40,34,35)
    $wave.AddLine(34,35,34,13)
    $wave.AddBezier(34,13,39,19,51,17,53,28)
    $wave.AddBezier(53,28,54,33,50,36,46,35)
    $ribbon = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(12,12,43,43), [Drawing.Color]::FromArgb(88,228,204), [Drawing.Color]::FromArgb(166,133,255), [single]330)
    $stroke = [Drawing.Pen]::new($ribbon,[single]5.5)
    $stroke.StartCap = $stroke.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $stroke.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($stroke,$wave)
    if ($size -ge 24) {
        $wave.Flatten($null,[single]0.12)
        $points = $wave.PathPoints
        $dot = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(244,252,255))
        $distance = 0.0
        $mark = 3.7
        for ($k=1; $k -lt $points.Length; $k++) {
            $start = $points[$k-1]
            $end = $points[$k]
            $dx = $end.X - $start.X
            $dy = $end.Y - $start.Y
            $segment = [Math]::Sqrt($dx*$dx+$dy*$dy)
            if ($segment -le 0) { continue }
            while ($mark -le $distance + $segment) {
                $part = ($mark - $distance) / $segment
                $x = [single]($start.X + $part*$dx - 0.76)
                $y = [single]($start.Y + $part*$dy - 0.76)
                $g.FillEllipse($dot,$x,$y,[single]1.52,[single]1.52)
                $mark += 4.5
            }
            $distance += $segment
        }
        $dot.Dispose()
    }
    if ($size -eq 256) {
        $bitmap.Save((Join-Path $PSScriptRoot 'Daybetter-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
    }
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += @{ Size=$size; Bytes=$stream.ToArray() }
    foreach ($obj in @($stream,$stroke,$ribbon,$wave,$bg,$path,$g,$bitmap)) { $obj.Dispose() }
}
$out = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $PSScriptRoot 'Daybetter.ico')))
$out.Write([uint16]0); $out.Write([uint16]1); $out.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $out.Write([byte]($frame.Size % 256)); $out.Write([byte]($frame.Size % 256))
    $out.Write([byte]0); $out.Write([byte]0); $out.Write([uint16]1); $out.Write([uint16]32)
    $out.Write([uint32]$frame.Bytes.Length); $out.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $out.Write([byte[]]$frame.Bytes) }
$out.Dispose()

