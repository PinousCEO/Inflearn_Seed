param(
    [string]$GeneratedIcon,
    [string]$GeneratedFeature
)

Add-Type -AssemblyName System.Drawing

$outDir = if ($PSScriptRoot) { $PSScriptRoot } else { Join-Path (Get-Location) 'StoreAssets' }
$titleDir = Join-Path (Split-Path -Parent $outDir) 'Assets\05_Resources\UI\Title'

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.SetResolution(72, 72)
    return $bmp
}

function Draw-Cover($g, $image, [int]$w, [int]$h) {
    $scale = [Math]::Max($w / $image.Width, $h / $image.Height)
    $dw = [int][Math]::Ceiling($image.Width * $scale)
    $dh = [int][Math]::Ceiling($image.Height * $scale)
    $x = [int](($w - $dw) / 2)
    $y = [int](($h - $dh) / 2)
    $g.DrawImage($image, $x, $y, $dw, $dh)
}

function Save-Resized([string]$src, [string]$dst, [int]$w, [int]$h) {
    $source = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Canvas $w $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    Draw-Cover $g $source $w $h
    $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $source.Dispose()
}

Save-Resized $GeneratedIcon (Join-Path $outDir 'app-icon-512.png') 512 512
Save-Resized $GeneratedFeature (Join-Path $outDir 'feature-graphic-1024x500.png') 1024 500

$screens = @(
    @{ File='TinyLegends_Title_KeyArt.png'; Head='작지만 강한 영웅들'; Sub='귀여운 전설이 지금 시작됩니다' },
    @{ File='TinyLegends_Scene01_VillageDefense.png'; Head='끝없이 성장하는 전투'; Sub='접속하지 않아도 영웅은 강해집니다' },
    @{ File='TinyLegends_Scene02_TreasureVault.png'; Head='보물로 완성하는 빌드'; Sub='장비를 모으고 최고의 조합을 찾아보세요' },
    @{ File='TinyLegends_Scene03_IceTitan.png'; Head='거대한 보스에 도전'; Sub='스킬을 조합해 강적을 돌파하세요' },
    @{ File='TinyLegends_Scene05_Skyship.png'; Head='새로운 세계를 탐험'; Sub='매일 펼쳐지는 다채로운 모험' }
)

$logo = [System.Drawing.Image]::FromFile((Join-Path $titleDir 'TinyLegends_Logo.png'))
$fontHead = New-Object System.Drawing.Font('Malgun Gothic', 55, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontSub = New-Object System.Drawing.Font('Malgun Gothic', 27, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$center = New-Object System.Drawing.StringFormat
$center.Alignment = [System.Drawing.StringAlignment]::Center

for ($i=0; $i -lt $screens.Count; $i++) {
    $scene = [System.Drawing.Image]::FromFile((Join-Path $titleDir $screens[$i].File))
    $bmp = New-Canvas 1080 1920
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    Draw-Cover $g $scene 1080 1920

    $topBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0,0,1080,480)),
        ([System.Drawing.Color]::FromArgb(235,7,20,42)),
        ([System.Drawing.Color]::FromArgb(0,7,20,42)),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($topBrush, 0, 0, 1080, 480)
    $bottomBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0,1610,1080,310)),
        ([System.Drawing.Color]::FromArgb(0,7,20,42)),
        ([System.Drawing.Color]::FromArgb(225,7,20,42)),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($bottomBrush, 0, 1610, 1080, 310)

    $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(190,0,0,0))
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,255,216,96))
    $rectHead = New-Object System.Drawing.RectangleF(30,95,1020,90)
    $rectSub = New-Object System.Drawing.RectangleF(30,195,1020,60)
    $shadowHead = New-Object System.Drawing.RectangleF(34,99,1020,90)
    $g.DrawString($screens[$i].Head, $fontHead, $shadow, $shadowHead, $center)
    $g.DrawString($screens[$i].Head, $fontHead, $white, $rectHead, $center)
    $g.DrawString($screens[$i].Sub, $fontSub, $gold, $rectSub, $center)

    $logoW=330; $logoH=[int]($logo.Height * $logoW / $logo.Width)
    $g.DrawImage($logo, [int]((1080-$logoW)/2), 1745, $logoW, $logoH)
    $penOuter = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,241,184,57), 10)
    $penInner = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180,13,37,62), 4)
    $g.DrawRectangle($penOuter, 10, 10, 1060, 1900)
    $g.DrawRectangle($penInner, 24, 24, 1032, 1872)

    $dst = Join-Path $outDir ('screenshot-{0:D2}.png' -f ($i+1))
    $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
    $penInner.Dispose(); $penOuter.Dispose(); $gold.Dispose(); $white.Dispose(); $shadow.Dispose()
    $bottomBrush.Dispose(); $topBrush.Dispose(); $g.Dispose(); $bmp.Dispose(); $scene.Dispose()
}

$fontHead.Dispose(); $fontSub.Dispose(); $center.Dispose(); $logo.Dispose()
