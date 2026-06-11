# =================================================================================================
# IMAGE CROP TOOL - WPF Version (FINAL FIXED)
# =================================================================================================

#region Initialization
# -------------------------------------------------------------------------------------------------
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing, System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

$null = [Win32]::ShowWindow([Win32]::GetConsoleWindow(), 0)

# Suppress "Invalid SOS parameters" warnings from corrupt JPEG metadata
try {
    $currentDomain = [System.AppDomain]::CurrentDomain
    if ($currentDomain -ne $null) {
        $currentDomain.FirstChanceException.Add({
            if ($_.Exception.Message -match "Invalid SOS parameters for sequential JPEG") {
                $_.Exception.Data["Suppressed"] = $true
            }
        })
    }
} catch { }

# Global variables
$script:cancel = $false
$script:previewImage = $null
$script:realCropRect = $null
$script:isDragging = $false
$script:resizeMode = ""
$script:isUpdatingBoxes = $false
$script:snapThreshold = 8
$script:snapActive = $false
$script:currentOrientation = 1
$script:currentFileName = "filename.jpg"
$script:scaleFactor = 1
$script:lastLoadedSource = ""
$script:currentZoom = 1.0
$script:resizeHandles = @()
$script:handleSize = 8
$script:controlsCreated = $false
$script:isPanning = $false
$script:panStartPoint = $null
$script:panModeActive = $false
$script:overlayLeft   = 0.0
$script:overlayTop    = 0.0
$script:overlayWidth  = 0.0
$script:overlayHeight = 0.0
$script:loggedResizeError = $false
$script:resizeHandlesDisabled = $false

function ConvertTo-SolidColorBrush($colorName) {
    switch ($colorName) {
        "LightGreen" { return New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::LightGreen) }
        "LightYellow" { return New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::LightYellow) }
        "LightCoral" { return New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::LightCoral) }
        "LightBlue" { return New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::LightBlue) }
        "LightGray" { return New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::LightGray) }
        default { return [System.Windows.Media.Brushes]::White }
    }
}

# FIXED: Simple number extraction without any array issues
function Get-NumericValue($textbox) {
    if (-not $textbox -or -not $textbox.Text) { return 0 }
    $text = [string]$textbox.Text
    $digits = ""
    foreach ($ch in $text.ToCharArray()) {
        if ($ch -ge '0' -and $ch -le '9') { $digits += $ch }
    }
    if ($digits.Length -eq 0) { return 0 }
    return [int]$digits
}
#endregion

#region Helper Functions
# -------------------------------------------------------------------------------------------------
function Invoke-ProcessEvents {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    [System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke(
        [Action]{ $frame.Continue = $false },
        [System.Windows.Threading.DispatcherPriority]::Background
    )
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

function Log($msg, $isError = $false) {
    $line = "$(Get-Date -Format "HH:mm:ss") - $msg"
    if ($global:logTextBox -and $global:logTextBox.Dispatcher) {
        $global:logTextBox.Dispatcher.Invoke([Action]{
            if ($global:logTextBox) { 
                $global:logTextBox.AppendText("$line`r`n")
                $global:logTextBox.ScrollToEnd()
            }
        })
    }
    if ($isError) { Write-Host "ERROR: $line" -ForegroundColor Red }
}

function Get-UnitSuffix { return $(if ($unitMM.IsChecked) { "mm" } else { "px" }) }

function Get-TextboxValue($textbox) {
    if ($textbox.Foreground -eq [System.Windows.Media.Brushes]::Gray -and $textbox.Text -eq $textbox.Tag) { return "" }
    return $textbox.Text
}

function Set-InputColor($textbox, $colorName) {
    if ($textbox) { 
        $textbox.Background = ConvertTo-SolidColorBrush $colorName
    }
}

function Set-Placeholder($textbox, $text) {
    $textbox.Tag = $text
    $textbox.Text = $text
    $textbox.Foreground = [System.Windows.Media.Brushes]::Gray
    
    $textbox.Add_GotFocus({
        if ($this.Foreground -eq [System.Windows.Media.Brushes]::Gray -and $this.Text -eq $this.Tag) {
            $this.Text = ""
            $this.Foreground = [System.Windows.Media.Brushes]::Black
        }
    })
    
    $textbox.Add_LostFocus({
        if ([string]::IsNullOrWhiteSpace($this.Text)) {
            $this.Text = $this.Tag
            $this.Foreground = [System.Windows.Media.Brushes]::Gray
        }
    })
}

function Normalize-Path($path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $path }
    try {
        $clean = $path -replace '[\\/]+', '\'
        return [System.IO.Path]::GetFullPath($clean)
    } catch {
        return ($path -replace '[\\/]+', '\')
    }
}

function Allow-OnlyNumbers($textbox) {
    if (-not $textbox) { return }
    $textbox.Add_PreviewTextInput({
        $_.Handled = (-not [char]::IsDigit($_.Text))
    })
    $textbox.Add_PreviewKeyDown({
        if ($_.Key -eq [System.Windows.Input.Key]::Space) { $_.Handled = $true }
    })
}

function Get-TargetSize {
    if (-not $widthBox -or -not $heightBox) { return $null }
    
    $wNum = Get-NumericValue $widthBox
    $hNum = Get-NumericValue $heightBox
    
    if ($wNum -eq 0 -or $hNum -eq 0) { return $null }
    
    if ($unitMM.IsChecked) {
        $dpi = 96.0
        $widthPx = [math]::Round($wNum / 25.4 * $dpi)
        $heightPx = [math]::Round($hNum / 25.4 * $dpi)
        return @{ Width = $widthPx; Height = $heightPx }
    } else {
        return @{ Width = $wNum; Height = $hNum }
    }
}

function Update-UnitsDisplay {
    if (-not $script:realCropRect) { 
        $script:isUpdatingBoxes = $true
        
        if ($widthBox) {
            $currentNum = Get-NumericValue $widthBox
            if ($unitMM.IsChecked) {
                $converted = [math]::Round($currentNum / 96.0 * 25.4)
                $widthBox.Text = "$converted mm"
            } else {
                $widthBox.Text = "$currentNum px"
            }
        }
        
        if ($heightBox) {
            $currentNum = Get-NumericValue $heightBox
            if ($unitMM.IsChecked) {
                $converted = [math]::Round($currentNum / 96.0 * 25.4)
                $heightBox.Text = "$converted mm"
            } else {
                $heightBox.Text = "$currentNum px"
            }
        }
        
        $script:isUpdatingBoxes = $false
        return
    }
    
    $w = [double]$script:realCropRect.Width
    $h = [double]$script:realCropRect.Height
    
    if ($unitMM.IsChecked) {
        $dpi = 96.0
        $w = [math]::Round($w / $dpi * 25.4)
        $h = [math]::Round($h / $dpi * 25.4)
        $unitLabel = "mm"
    } else {
        $unitLabel = "px"
    }
    
    $script:isUpdatingBoxes = $true
    if ($widthBox) { $widthBox.Text = "$w $unitLabel" }
    if ($heightBox) { $heightBox.Text = "$h $unitLabel" }
    $script:isUpdatingBoxes = $false
}

function Update-SourceSizeBoxes {
    if (-not $script:previewImage) {
        if ($widthSourceBox) { $widthSourceBox.Text = "0 px" }
        if ($heightSourceBox) { $heightSourceBox.Text = "0 px" }
        return
    }
    
    $w = $script:previewImage.Width
    $h = $script:previewImage.Height
    
    if ($unitMM.IsChecked) {
        $dpi = 96.0
        $w = [math]::Round($w / $dpi * 25.4)
        $h = [math]::Round($h / $dpi * 25.4)
        $unit = "mm"
    } else {
        $unit = "px"
    }
    
    if ($widthSourceBox) { $widthSourceBox.Text = "$w $unit" }
    if ($heightSourceBox) { $heightSourceBox.Text = "$h $unit" }
}

function Update-FilenamePreview {
    if (-not $script:currentFileName) { return }
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($script:currentFileName)
    $ext = [System.IO.Path]::GetExtension($script:currentFileName)
    $userText = $nameBox.Text -replace '[\\/:*?"<>|\s]', ''
    
    $final = if ($modeSuffix.IsChecked) { "$baseName$userText$ext" } else { "$userText$baseName$ext" }
    
    if ($previewLabel) {
        $previewLabel.Content = $final
    }
}

function Check-OverwriteWarning {
    $suffix = $nameBox.Text -replace '[\\/:*?"<>|\s]', ''
    if ([string]::IsNullOrWhiteSpace($suffix) -and (-not $noOverwriteChk.IsChecked)) {
        $result = [System.Windows.MessageBox]::Show(
            "WARNING: The name field is empty and 'Don't overwrite' is NOT checked!`n`nThis means existing files will be OVERWRITTEN if they have the original name.`n`nDo you want to continue?",
            "Overwrite Warning",
            "YesNo",
            "Warning"
        )
        return ($result -eq "Yes")
    }
    return $true
}
#endregion

#region Image Processing Functions
# -------------------------------------------------------------------------------------------------
function Test-JpegValid($filePath) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        if ($bytes.Length -lt 2 -or $bytes[0] -ne 0xFF -or $bytes[1] -ne 0xD8) { return $false }
        
        $eoiPosition = $bytes.Length - 2
        if ($eoiPosition -ge 0 -and $bytes[$eoiPosition] -eq 0xFF -and $bytes[$eoiPosition + 1] -eq 0xD9) { return $true }
        
        for ($i = 0; $i -lt [math]::Min($bytes.Length - 1, 10000); $i++) {
            if ($bytes[$i] -eq 0xFF -and $bytes[$i+1] -eq 0xDA) { return $true }
        }
        return $false
    } catch { return $false }
}

function Apply-ExifRotation($img) {
    try {
        $orientation = 1
        $hasOrientation = $false
        foreach ($prop in $img.PropertyItems) {
            if ($prop.Id -eq 0x0112) {
                $orientation = [System.BitConverter]::ToUInt16($prop.Value, 0)
                $hasOrientation = $true
                break
            }
        }
        $script:currentOrientation = $orientation
        if (-not $hasOrientation -or $orientation -eq 1) { return $img }
        
        switch ($orientation) {
            2 { $img.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX) }
            3 { $img.RotateFlip([System.Drawing.RotateFlipType]::Rotate180FlipNone) }
            4 { $img.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY) }
            5 { $img.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipX) }
            6 { $img.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone) }
            7 { $img.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipX) }
            8 { $img.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipNone) }
        }
        try { $img.RemovePropertyItem(0x0112) } catch { }
    } catch {
        Log "Failed to apply EXIF rotation: $($_.Exception.Message)" $true
    }
    return $img
}

function Copy-AllExifData($sourceImg, $destImg) {
    try {
        foreach ($prop in $sourceImg.PropertyItems) {
            try { $destImg.SetPropertyItem($prop) } catch { }
        }
    } catch {
        Log "Could not copy EXIF data: $($_.Exception.Message)" $true
    }
}

function Copy-FileDates($sourcePath, $destPath) {
    try {
        $sourceFile = Get-Item $sourcePath
        Set-ItemProperty -Path $destPath -Name CreationTime -Value $sourceFile.CreationTime
        Set-ItemProperty -Path $destPath -Name LastWriteTime -Value $sourceFile.LastWriteTime
        Set-ItemProperty -Path $destPath -Name LastAccessTime -Value $sourceFile.LastAccessTime
    } catch {
        Log "Could not copy file dates: $($_.Exception.Message)" $true
    }
}

function Set-OrientationToNormal($img) {
    try {
        foreach ($prop in $img.PropertyItems) {
            if ($prop.Id -eq 0x0112) {
                $prop.Value = [System.BitConverter]::GetBytes([uint16]1)
                $img.SetPropertyItem($prop)
                break
            }
        }
    } catch {
        Log "Could not set orientation to 1: $_.Exception.Message" $true
    }
}
#endregion

#region Preview and Crop Rectangle
# -------------------------------------------------------------------------------------------------
function Update-PreviewRect {
    if (-not $script:previewImage -or -not $script:realCropRect -or -not $cropOverlay) { return }
    
    try {
        $img = $script:previewImage
        $r = $script:realCropRect
        
        # Force scalar extraction from rectangle (fixes array issue)
        $rx = [double]($r.X -as [double]) + 0
        $ry = [double]($r.Y -as [double]) + 0
        $rw = [double]($r.Width -as [double]) + 0
        $rh = [double]($r.Height -as [double]) + 0
        
        $viewWidth  = [double]$previewCanvas.ActualWidth
        $viewHeight = [double]$previewCanvas.ActualHeight

        if ($viewWidth -le 0 -or $viewHeight -le 0) { return }
        
        $imgWidth = [double]$img.Width
        $imgHeight = [double]$img.Height
        
        if ($imgWidth -le 0 -or $imgHeight -le 0) { return }
        
        $scale = [math]::Min($viewWidth / $imgWidth, $viewHeight / $imgHeight)
        $displayedW = $imgWidth * $scale
        $displayedH = $imgHeight * $scale
        $offsetX = ($viewWidth - $displayedW) / 2.0
        $offsetY = ($viewHeight - $displayedH) / 2.0
        
        $script:scaleFactor = $scale
        
        $overlayLeft = $offsetX + ($rx * $scale)
        $overlayTop  = $offsetY + ($ry * $scale)
        $overlayWidth  = [math]::Max(10.0, $rw * $scale)
        $overlayHeight = [math]::Max(10.0, $rh * $scale)
        
        # Ensure scalar (take first element if array)
        $script:overlayLeft   = if ($overlayLeft -is [array]) { $overlayLeft[0] } else { $overlayLeft }
        $script:overlayTop    = if ($overlayTop -is [array]) { $overlayTop[0] } else { $overlayTop }
        $script:overlayWidth  = if ($overlayWidth -is [array]) { $overlayWidth[0] } else { $overlayWidth }
        $script:overlayHeight = if ($overlayHeight -is [array]) { $overlayHeight[0] } else { $overlayHeight }
        
        # Cast to double
        $script:overlayLeft   = [double]$script:overlayLeft
        $script:overlayTop    = [double]$script:overlayTop
        $script:overlayWidth  = [double]$script:overlayWidth
        $script:overlayHeight = [double]$script:overlayHeight
        
        $cropOverlay.Width = $script:overlayWidth
        $cropOverlay.Height = $script:overlayHeight
        [System.Windows.Controls.Canvas]::SetLeft($cropOverlay, $script:overlayLeft)
        [System.Windows.Controls.Canvas]::SetTop($cropOverlay, $script:overlayTop)
        
        Log "Before Update-ResizeHandles: left=$($script:overlayLeft) type=$($script:overlayLeft.GetType().FullName)"
        if (-not $script:resizeHandlesDisabled) {
                Update-ResizeHandles
        }
    } catch {
        Log "Update-ResizeHandles error: $($_.Exception.Message)" $true
        $script:resizeHandlesDisabled = $true
    }
}

function Update-ResizeHandles {
    if (-not $cropOverlay -or $script:resizeHandles.Count -eq 0) { return }
    try {
        # Helper that guarantees a scalar double
        function Get-ScalarDouble($val) {
            # Extract first element if array
            if ($val -is [array]) { $val = $val[0] }
            if ($null -eq $val) { return 0.0 }
            # Force numeric scalar by adding 0 after cast
            return ([double]$val) + 0.0
        }

        $x = Get-ScalarDouble $script:overlayLeft
        $y = Get-ScalarDouble $script:overlayTop
        $w = Get-ScalarDouble $script:overlayWidth
        $h = Get-ScalarDouble $script:overlayHeight
        $hs = Get-ScalarDouble $script:handleSize

        if ($w -le 0 -or $h -le 0) { return }

        # Pre‑compute using separate explicit operations
        $half = $hs / 2.0
        $left   = $x
        $right  = $x + $w
        $top    = $y
        $bottom = $y + $h
        $centerX = $x + ($w / 2.0)
        $centerY = $y + ($h / 2.0)

        # Compute each handle coordinate as a scalar double
        $coords = @(
            @( ($left - $half), ($top - $half) ),
            @( ($right - $half), ($top - $half) ),
            @( ($left - $half), ($bottom - $half) ),
            @( ($right - $half), ($bottom - $half) ),
            @( ($centerX - $half), ($top - $half) ),
            @( ($centerX - $half), ($bottom - $half) ),
            @( ($left - $half), ($centerY - $half) ),
            @( ($right - $half), ($centerY - $half) )
        )

        for ($i = 0; $i -lt $script:resizeHandles.Count; $i++) {
            [System.Windows.Controls.Canvas]::SetLeft($script:resizeHandles[$i], $coords[$i][0])
            [System.Windows.Controls.Canvas]::SetTop($script:resizeHandles[$i], $coords[$i][1])
        }
    } catch {
        Log "Update-ResizeHandles error: $($_.Exception.Message)" $true
        $script:resizeHandlesDisabled = $true   # disable future calls
    }
}

function FitToWindow {
    if ($global:previewImageControl -and $scrollViewer) {
        try {
            $global:previewImageControl.Dispatcher.Invoke([Action]{
                $global:previewImageControl.Stretch = "Uniform"
                $global:previewImageControl.LayoutTransform = $null
                $script:currentZoom = 1.0
                if ($zoomLabel) { $zoomLabel.Content = "Fit" }
                # Force a layout pass
                $scrollViewer.UpdateLayout()
                Update-PreviewRect
            })
        } catch {
            Log "FitToWindow error: $($_.Exception.Message)" $true
        }
    }
}

function Apply-SnapToEdges {
    param($rect)

    if (-not $script:previewImage) { return $rect }

    $imgW = $script:previewImage.Width
    $imgH = $script:previewImage.Height

    $threshold = $script:snapThreshold / [math]::Max($script:scaleFactor, 0.001)

    if ([math]::Abs($rect.X) -lt $threshold) { $rect.X = 0 }
    if ([math]::Abs($rect.Y) -lt $threshold) { $rect.Y = 0 }

    if ([math]::Abs(($rect.X + $rect.Width) - $imgW) -lt $threshold) {
        $rect.Width = $imgW - $rect.X
    }

    if ([math]::Abs(($rect.Y + $rect.Height) - $imgH) -lt $threshold) {
        $rect.Height = $imgH - $rect.Y
    }

    return $rect
}

function Load-PreviewImage($path) {
    if (-not $script:controlsCreated) {
        Start-Sleep -Milliseconds 100
        if (-not $script:controlsCreated) { return }
    }
    
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path $path)) {
        if ($global:previewImageControl) { 
            $global:previewImageControl.Dispatcher.Invoke([Action]{ $global:previewImageControl.Source = $null })
        }
        return
    }
    
    $script:lastLoadedSource = $path
    
    if (Test-Path $path -PathType Leaf) {
        $script:currentFileName = [System.IO.Path]::GetFileName($path)
    } else {
        $firstValidFile = Get-ChildItem $path -File | Where-Object { 
            $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' 
        } | Select-Object -First 1
        if ($firstValidFile) {
            $script:currentFileName = $firstValidFile.Name
            $path = $firstValidFile.FullName
        }
    }
    
    if ($script:previewImage) {
        $script:previewImage.Dispose()
        $script:previewImage = $null
    }
    
    try {
        $img = [System.Drawing.Image]::FromFile($path)
        $img = Apply-ExifRotation($img)
        $script:previewImage = $img
        
        $bitmap = New-Object System.Windows.Media.Imaging.BitmapImage
        $bitmap.BeginInit()
        $stream = New-Object System.IO.MemoryStream
        $img.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $stream.Position = 0
        $bitmap.StreamSource = $stream
        $bitmap.EndInit()
        $bitmap.Freeze()
        
        if ($global:previewImageControl) {
            $global:previewImageControl.Dispatcher.Invoke([Action]{
            $global:previewImageControl.Source = $bitmap
            $global:previewImageControl.Stretch = "Uniform"
            $global:previewImageControl.LayoutTransform = $null
            $script:currentZoom = 1.0
            if ($zoomLabel) { $zoomLabel.Content = "Fit" }

            $previewCanvas.UpdateLayout()
            $scrollViewer.UpdateLayout()

            Update-PreviewRect
        })
        }
        
        Update-SourceSizeBoxes
        Update-FilenamePreview
        
        if (-not $script:realCropRect) {
            $target = Get-TargetSize
            if ($target -and $target.Width -gt 0 -and $target.Height -gt 0) {
                $cropW = [math]::Min($target.Width, $img.Width)
                $cropH = [math]::Min($target.Height, $img.Height)
                $script:realCropRect = New-Object System.Drawing.Rectangle 0, 0, $cropW, $cropH
            } else {
                $script:realCropRect = New-Object System.Drawing.Rectangle 0, 0, $img.Width, $img.Height
            }
        }
        
        Update-PreviewRect
        Log "Loaded preview: $script:currentFileName"
    } catch {
        Log "Failed to load image: $($_.Exception.Message)" $true
        $script:previewImage = $null
        if ($global:previewImageControl) {
            $global:previewImageControl.Dispatcher.Invoke([Action]{ $global:previewImageControl.Source = $null })
        }
    }
}

function Update-RunState {
    $source = Get-TextboxValue $srcBox
    $sourceValid = $false
    
    if ($source -and (Test-Path $source)) {
        if (Test-Path $source -PathType Container) {
            $images = Get-ChildItem $source -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' }
            if ($images) { $sourceValid = $true }
        } elseif (Test-Path $source -PathType Leaf) {
            $ext = [System.IO.Path]::GetExtension($source).ToLower()
            if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') { $sourceValid = $true }
        }
    }
    
    if ($cropBtn) { $cropBtn.IsEnabled = $sourceValid }
}

function Test-PathBox($textbox) {
    $realText = Get-TextboxValue $textbox
    
    if ([string]::IsNullOrWhiteSpace($realText)) {
        Set-InputColor $textbox "LightYellow"
        return
    }
    
    if (Test-Path $realText) {
        Set-InputColor $textbox "LightGreen"
        
        if ($textbox -eq $srcBox) {
            if (Test-Path $realText -PathType Leaf) {
                $ext = [System.IO.Path]::GetExtension($realText).ToLower()
                if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                    Load-PreviewImage $realText
                }
            } elseif (Test-Path $realText -PathType Container) {
                Load-PreviewImage $realText
            }
        }
        
        if ($textbox -eq $dstBox) {
            $srcText = Get-TextboxValue $srcBox
            if (-not [string]::IsNullOrWhiteSpace($srcText) -and (Test-Path $srcText)) {
                $suggestedDest = if (Test-Path $srcText -PathType Container) { $srcText } else { Split-Path $srcText }
                if ([string]::IsNullOrWhiteSpace($dstBox.Text) -or $dstBox.Foreground -eq [System.Windows.Media.Brushes]::Gray) {
                    $dstBox.Text = $suggestedDest
                    $dstBox.Foreground = [System.Windows.Media.Brushes]::Black
                }
            }
        }
    } else {
        Set-InputColor $textbox "LightCoral"
    }
    Update-RunState
}
#endregion

#region Validation Timer
# -------------------------------------------------------------------------------------------------
$validationTimer = New-Object System.Windows.Threading.DispatcherTimer
$validationTimer.Interval = [TimeSpan]::FromMilliseconds(400)
$validationTimer.Add_Tick({
    $validationTimer.Stop()
    Test-PathBox $srcBox
    Test-PathBox $dstBox
})
#endregion

#region Create Main WPF Window
# -------------------------------------------------------------------------------------------------
$form = New-Object System.Windows.Window
$form.Title = "Image Crop Tool by João Silva - WPF Version"
$form.Width = 1100
$form.Height = 700
$form.MinWidth = 1100
$form.MinHeight = 700
$form.WindowStartupLocation = "CenterScreen"

$mainGrid = New-Object System.Windows.Controls.Grid
$form.Content = $mainGrid

$colDefLeft = New-Object System.Windows.Controls.ColumnDefinition
$colDefLeft.Width = "420"
$colDefRight = New-Object System.Windows.Controls.ColumnDefinition
$colDefRight.Width = "*"
$mainGrid.ColumnDefinitions.Add($colDefLeft)
$mainGrid.ColumnDefinitions.Add($colDefRight)

$leftScroll = New-Object System.Windows.Controls.ScrollViewer
$leftScroll.VerticalScrollBarVisibility = "Auto"
$mainGrid.AddChild($leftScroll)
[System.Windows.Controls.Grid]::SetColumn($leftScroll, 0)

$controlsPanel = New-Object System.Windows.Controls.StackPanel
$controlsPanel.Margin = "10"
$leftScroll.Content = $controlsPanel

$previewGrid = New-Object System.Windows.Controls.Grid
$mainGrid.AddChild($previewGrid)
[System.Windows.Controls.Grid]::SetColumn($previewGrid, 1)
$previewGrid.Margin = "10"

# Use a DockPanel or Grid to keep toolbar at bottom
$previewDock = New-Object System.Windows.Controls.DockPanel
$previewGrid.AddChild($previewDock)

# Toolbar will be docked at bottom
$toolBarBorder = New-Object System.Windows.Controls.Border
$toolBarBorder.Background = [System.Windows.Media.Brushes]::White
$toolBarBorder.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$toolBarBorder.BorderThickness = "1"
$toolBarBorder.CornerRadius = "8"
$toolBarBorder.Padding = "5"
$toolBarBorder.Margin = "0,10,0,10"
[System.Windows.Controls.DockPanel]::SetDock($toolBarBorder, "Bottom")
$previewDock.AddChild($toolBarBorder)

$toolBarPanel = New-Object System.Windows.Controls.StackPanel
$toolBarPanel.Orientation = "Horizontal"
$toolBarPanel.HorizontalAlignment = "Center"
$toolBarBorder.Child = $toolBarPanel

# Toolbar buttons
$zoomInBtn = New-Object System.Windows.Controls.Button
$zoomInBtn.Content = "🔍 Zoom In"
$zoomInBtn.Margin = "2"
$zoomInBtn.Padding = "8,5"
$zoomInBtn.ToolTip = "Zoom In (Ctrl+Plus)"
$zoomInBtn.Background = [System.Windows.Media.Brushes]::White
$zoomInBtn.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$zoomInBtn.BorderThickness = "1"
$zoomInBtn.Cursor = [System.Windows.Input.Cursors]::Hand
$zoomInBtn.Add_Click({
    if ($global:previewImageControl) {
        $global:previewImageControl.Stretch = "None"
        $newScale = $script:currentZoom * 1.2
        $script:currentZoom = [math]::Min(5.0, $newScale)
        $global:previewImageControl.LayoutTransform = New-Object System.Windows.Media.ScaleTransform($script:currentZoom, $script:currentZoom)
        $zoomPercent = [math]::Round($script:currentZoom * 100)
        if ($zoomLabel) { $zoomLabel.Content = "$zoomPercent%" }
        Update-PreviewRect
    }
})
$toolBarPanel.AddChild($zoomInBtn)

$zoomOutBtn = New-Object System.Windows.Controls.Button
$zoomOutBtn.Content = "🔍 Zoom Out"
$zoomOutBtn.Margin = "2"
$zoomOutBtn.Padding = "8,5"
$zoomOutBtn.ToolTip = "Zoom Out (Ctrl+Minus)"
$zoomOutBtn.Background = [System.Windows.Media.Brushes]::White
$zoomOutBtn.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$zoomOutBtn.BorderThickness = "1"
$zoomOutBtn.Cursor = [System.Windows.Input.Cursors]::Hand
$zoomOutBtn.Add_Click({
    if ($global:previewImageControl) {
        $global:previewImageControl.Stretch = "None"
        $newScale = $script:currentZoom / 1.2
        $script:currentZoom = [math]::Max(0.1, $newScale)
        $global:previewImageControl.LayoutTransform = New-Object System.Windows.Media.ScaleTransform($script:currentZoom, $script:currentZoom)
        $zoomPercent = [math]::Round($script:currentZoom * 100)
        if ($zoomLabel) { $zoomLabel.Content = "$zoomPercent%" }
        Update-PreviewRect
    }
})
$toolBarPanel.AddChild($zoomOutBtn)

$fitBtn = New-Object System.Windows.Controls.Button
$fitBtn.Content = "⟳ Fit to Window"
$fitBtn.Margin = "2"
$fitBtn.Padding = "8,5"
$fitBtn.ToolTip = "Fit image to window (Ctrl+0)"
$fitBtn.Background = [System.Windows.Media.Brushes]::White
$fitBtn.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$fitBtn.BorderThickness = "1"
$fitBtn.Cursor = [System.Windows.Input.Cursors]::Hand
$fitBtn.Add_Click({
    FitToWindow
})
$toolBarPanel.AddChild($fitBtn)

$actualSizeBtn = New-Object System.Windows.Controls.Button
$actualSizeBtn.Content = "1:1 Actual Size"
$actualSizeBtn.Margin = "2"
$actualSizeBtn.Padding = "8,5"
$actualSizeBtn.ToolTip = "Show actual size (100%)"
$actualSizeBtn.Background = [System.Windows.Media.Brushes]::White
$actualSizeBtn.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$actualSizeBtn.BorderThickness = "1"
$actualSizeBtn.Cursor = [System.Windows.Input.Cursors]::Hand
$actualSizeBtn.Add_Click({
    if ($script:previewImage) {
        $global:previewImageControl.Stretch = "None"
        $script:currentZoom = 1.0
        $global:previewImageControl.LayoutTransform = $null
        $global:previewImageControl.Width = $script:previewImage.Width
        $global:previewImageControl.Height = $script:previewImage.Height
        if ($zoomLabel) { $zoomLabel.Content = "100%" }
        Update-PreviewRect
    }
})
$toolBarPanel.AddChild($actualSizeBtn)

$sep = New-Object System.Windows.Controls.Separator
$sep.Width = 1
$sep.Height = 20
$sep.Margin = "5,0,5,0"
$sep.Background = [System.Windows.Media.Brushes]::LightGray
$toolBarPanel.AddChild($sep)

$panModeBtn = New-Object System.Windows.Controls.Button
$panModeBtn.Content = "✋ Pan Mode"
$panModeBtn.Margin = "2"
$panModeBtn.Padding = "8,5"
$panModeBtn.ToolTip = "Toggle pan mode to drag image"
$panModeBtn.Background = [System.Windows.Media.Brushes]::White
$panModeBtn.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$panModeBtn.BorderThickness = "1"
$panModeBtn.Cursor = [System.Windows.Input.Cursors]::Hand
$panModeBtn.Add_Click({
    $script:panModeActive = -not $script:panModeActive
    if ($script:panModeActive) {
        $panModeBtn.Background = [System.Windows.Media.Brushes]::LightBlue
        $panModeBtn.Content = "✋ Pan Mode (ON)"
        $scrollViewer.Cursor = [System.Windows.Input.Cursors]::Hand
    } else {
        $panModeBtn.Background = [System.Windows.Media.Brushes]::White
        $panModeBtn.Content = "✋ Pan Mode"
        $scrollViewer.Cursor = [System.Windows.Input.Cursors]::Arrow
    }
})
$toolBarPanel.AddChild($panModeBtn)

$zoomLabel = New-Object System.Windows.Controls.Label
$zoomLabel.Content = "Fit"
$zoomLabel.Margin = "10,0,5,0"
$zoomLabel.FontSize = 11
$zoomLabel.Foreground = [System.Windows.Media.Brushes]::Gray
$zoomLabel.VerticalAlignment = "Center"
$toolBarPanel.AddChild($zoomLabel)

$previewBorder = New-Object System.Windows.Controls.Border
$previewBorder.BorderBrush = [System.Windows.Media.Brushes]::DarkGray
$previewBorder.BorderThickness = "1"
$previewBorder.Background = [System.Windows.Media.Brushes]::LightGray
$previewBorder.CornerRadius = "5"
$previewDock.AddChild($previewBorder)

$previewCanvas = New-Object System.Windows.Controls.Canvas
$previewCanvas.HorizontalAlignment = "Stretch"
$previewCanvas.VerticalAlignment   = "Stretch"
$previewCanvas.Background = [System.Windows.Media.Brushes]::LightGray
$previewBorder.Child = $previewCanvas
$scrollViewer = New-Object System.Windows.Controls.ScrollViewer
$scrollViewer.HorizontalAlignment = "Stretch"
$scrollViewer.VerticalAlignment   = "Stretch"
$scrollViewer.HorizontalScrollBarVisibility = "Auto"
$scrollViewer.VerticalScrollBarVisibility = "Auto"
$previewCanvas.AddChild($scrollViewer)

$global:previewImageControl = New-Object System.Windows.Controls.Image
$global:previewImageControl.Stretch = "Uniform"
$scrollViewer.Content = $global:previewImageControl

$cropOverlay = New-Object System.Windows.Controls.Border
$cropOverlay.BorderBrush = [System.Windows.Media.Brushes]::Red
$cropOverlay.BorderThickness = "2"
$cropOverlay.Background = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(50, 255, 0, 0))
$cropOverlay.Visibility = "Visible"
$previewCanvas.AddChild($cropOverlay)

# Create resize handles (same as before)
for ($i = 0; $i -lt 8; $i++) {
    $handle = New-Object System.Windows.Controls.Border
    $handle.Width = $script:handleSize
    $handle.Height = $script:handleSize
    $handle.Background = [System.Windows.Media.Brushes]::White
    $handle.BorderBrush = [System.Windows.Media.Brushes]::Black
    $handle.BorderThickness = "1"
    $handle.Tag = $i
    $previewCanvas.AddChild($handle)
    $script:resizeHandles += $handle
    $handle.Add_MouseDown({
    $script:isDragging = $true
    $script:resizeMode = "move"
    $script:activeHandle = -1
    $script:activeHandle = [int]$this.Tag
    $script:startMousePoint = $_.GetPosition($previewCanvas)
    $_.Handled = $true
})
    
    $handle.Add_MouseEnter({
        switch ($this.Tag) {
            0 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNWSE }
            1 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNESW }
            2 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNESW }
            3 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNWSE }
            4 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNS }
            5 { $this.Cursor = [System.Windows.Input.Cursors]::SizeNS }
            6 { $this.Cursor = [System.Windows.Input.Cursors]::SizeWE }
            7 { $this.Cursor = [System.Windows.Input.Cursors]::SizeWE }
        }
    })
}

# Mouse events for crop rectangle (same)
$cropOverlay.Add_MouseEnter({ $this.Cursor = [System.Windows.Input.Cursors]::SizeAll })
$cropOverlay.Add_MouseLeave({ $this.Cursor = [System.Windows.Input.Cursors]::Arrow })

$cropOverlay.Add_MouseDown({
    $script:isDragging = $true
    $script:startMousePoint = $_.GetPosition($previewCanvas)
})

$previewCanvas.Add_MouseMove({

    if (-not $script:isDragging -or -not $script:previewImage) { return }

    $currentPos = $_.GetPosition($previewCanvas)

    $dx = ($currentPos.X - $script:startMousePoint.X) / $script:scaleFactor
    $dy = ($currentPos.Y - $script:startMousePoint.Y) / $script:scaleFactor

    $r = $script:realCropRect

    if ($script:resizeMode -eq "resize") {

        switch ($script:activeHandle) {
            0 { $r.X += $dx; $r.Y += $dy; $r.Width -= $dx; $r.Height -= $dy }
            1 { $r.Y += $dy; $r.Width += $dx; $r.Height -= $dy }
            2 { $r.X += $dx; $r.Width -= $dx; $r.Height += $dy }
            3 { $r.Width += $dx; $r.Height += $dy }
            4 { $r.Y += $dy; $r.Height -= $dy }
            5 { $r.Height += $dy }
            6 { $r.X += $dx; $r.Width -= $dx }
            7 { $r.Width += $dx }
        }

        $r = Apply-SnapToEdges $r
    }
    else {
        $r.X += $dx
        $r.Y += $dy
    }

    # clamp values
    $r.Width  = [math]::Max(10, $r.Width)
    $r.Height = [math]::Max(10, $r.Height)
    $r.X = [math]::Max(0, $r.X)
    $r.Y = [math]::Max(0, $r.Y)

    $script:realCropRect = $r
    $script:startMousePoint = $currentPos

    Update-UnitsDisplay
    Update-PreviewRect
})

$previewCanvas.Add_MouseUp({
    $script:isDragging = $false
})

# Pan functionality (keep existing)
$scrollViewer.Add_MouseDown({
    if ($script:panModeActive) {
        $script:isPanning = $true
        $script:panStartPoint = $_.GetPosition($scrollViewer)
        $scrollViewer.Cursor = [System.Windows.Input.Cursors]::SizeAll
    }
})

$scrollViewer.Add_MouseMove({
    if ($script:isPanning -and $script:panModeActive) {
        $currentPoint = $_.GetPosition($scrollViewer)
        $deltaX = $currentPoint.X - $script:panStartPoint.X
        $deltaY = $currentPoint.Y - $script:panStartPoint.Y
        
        $scrollViewer.ScrollToHorizontalOffset($scrollViewer.HorizontalOffset - $deltaX)
        $scrollViewer.ScrollToVerticalOffset($scrollViewer.VerticalOffset - $deltaY)
        
        $script:panStartPoint = $currentPoint
    }
})

$scrollViewer.Add_MouseUp({
    $script:isPanning = $false
    if ($script:panModeActive) {
        $scrollViewer.Cursor = [System.Windows.Input.Cursors]::Hand
    }
})
#endregion

#region Controls Panel Content
# -------------------------------------------------------------------------------------------------
$srcLabel = New-Object System.Windows.Controls.Label
$srcLabel.Content = "Source folder or file:"
$srcLabel.FontWeight = "Bold"
$srcLabel.Margin = "0,0,0,5"
$controlsPanel.AddChild($srcLabel)

$srcGrid = New-Object System.Windows.Controls.Grid
$srcGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $srcGrid.ColumnDefinitions[0].Width = "*"
$srcGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $srcGrid.ColumnDefinitions[1].Width = "Auto"
$controlsPanel.AddChild($srcGrid)

$srcBox = New-Object System.Windows.Controls.TextBox
$srcBox.Height = [Double]::NaN
$srcBox.Margin = "0,0,5,0"
$srcBox.AcceptsReturn = $false
$srcBox.TextWrapping = "Wrap"
$srcBox.HorizontalScrollBarVisibility = "Auto"
$srcBox.VerticalScrollBarVisibility = "Auto"
$srcBox.TextAlignment = "Left"
Set-Placeholder $srcBox "Write/paste the path or Drop a folder/file here..."
$srcGrid.AddChild($srcBox)
[System.Windows.Controls.Grid]::SetColumn($srcBox, 0)

$srcBox.AllowDrop = $true
$srcBox.Add_DragEnter({ $_.Effects = "Copy"; $srcBox.Background = ConvertTo-SolidColorBrush "LightBlue" })
$srcBox.Add_DragLeave({ Test-PathBox $srcBox })
$srcBox.Add_Drop({
    $files = $_.Data.GetData([System.Windows.DataFormats]::FileDrop)
    if ($files -and $files.Count -gt 0) {
        $path = $files[0]
        if (Test-Path $path) {
            $srcBox.Text = $path
            $srcBox.Foreground = [System.Windows.Media.Brushes]::Black
            Test-PathBox $srcBox
            $validationTimer.Stop()
            $validationTimer.Start()
        }
    }
})

$srcBtn = New-Object System.Windows.Controls.Button
$srcBtn.Content = "Browse"
$srcBtn.Width = 70
$srcBtn.Height = 25
$srcBtn.Add_Click({
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
    if ($dlg.ShowDialog() -eq $true) {
        $srcBox.Text = $dlg.FileName
        $srcBox.Foreground = [System.Windows.Media.Brushes]::Black
        Test-PathBox $srcBox
        $validationTimer.Stop()
        $validationTimer.Start()
    }
})
$srcGrid.AddChild($srcBtn)
[System.Windows.Controls.Grid]::SetColumn($srcBtn, 1)

$dstLabel = New-Object System.Windows.Controls.Label
$dstLabel.Content = "Output folder:"
$dstLabel.FontWeight = "Bold"
$dstLabel.Margin = "0,5,0,5"
$controlsPanel.AddChild($dstLabel)

$dstGrid = New-Object System.Windows.Controls.Grid
$dstGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dstGrid.ColumnDefinitions[0].Width = "*"
$dstGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dstGrid.ColumnDefinitions[1].Width = "Auto"
$controlsPanel.AddChild($dstGrid)

$dstBox = New-Object System.Windows.Controls.TextBox
$dstBox.Height = [Double]::NaN
$dstBox.Margin = "0,0,5,0"
$dstBox.AcceptsReturn = $false
$dstBox.TextWrapping = "Wrap"
$dstBox.HorizontalScrollBarVisibility = "Auto"
$dstBox.VerticalScrollBarVisibility = "Auto"
$dstBox.TextAlignment = "Left"
Set-Placeholder $dstBox "Write/paste the path or Drop the destination folder..."
$dstGrid.AddChild($dstBox)
[System.Windows.Controls.Grid]::SetColumn($dstBox, 0)

$dstBox.AllowDrop = $true
$dstBox.Add_DragEnter({ $_.Effects = "Copy"; $dstBox.Background = ConvertTo-SolidColorBrush "LightBlue" })
$dstBox.Add_DragLeave({ Test-PathBox $dstBox })
$dstBox.Add_Drop({
    $files = $_.Data.GetData([System.Windows.DataFormats]::FileDrop)
    if ($files -and $files.Count -gt 0) {
        $path = $files[0]
        if (Test-Path $path) {
            if (Test-Path $path -PathType Leaf) {
                $result = [System.Windows.MessageBox]::Show("You dropped a file. Use its parent folder instead?", "File Detected", "YesNo", "Question")
                if ($result -eq "Yes") {
                    $dstBox.Text = Split-Path $path
                } else { return }
            } else {
                $dstBox.Text = $path
            }
            $dstBox.Foreground = [System.Windows.Media.Brushes]::Black
            Test-PathBox $dstBox
        }
    }
})

$dstBtn = New-Object System.Windows.Controls.Button
$dstBtn.Content = "Browse"
$dstBtn.Width = 70
$dstBtn.Height = 25
$dstBtn.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    if ($dlg.ShowDialog() -eq "OK") {
        $dstBox.Text = $dlg.SelectedPath
        $dstBox.Foreground = [System.Windows.Media.Brushes]::Black
        Test-PathBox $dstBox
        $validationTimer.Stop()
        $validationTimer.Start()
    }
})
$dstGrid.AddChild($dstBtn)
[System.Windows.Controls.Grid]::SetColumn($dstBtn, 1)

$openAfterChk = New-Object System.Windows.Controls.CheckBox
$openAfterChk.Margin = "10,10,0,0"
$openAfterChk.IsChecked = $true
$openAfterChk.Content = "Open folder when done"
$controlsPanel.AddChild($openAfterChk)

$sep1 = New-Object System.Windows.Controls.Separator
$sep1.Margin = "0,10,0,10"
$controlsPanel.AddChild($sep1)

$nameGroup = New-Object System.Windows.Controls.GroupBox
$nameGroup.Header = "Output Filename"
$nameGroup.Margin = "0,0,0,10"
$controlsPanel.AddChild($nameGroup)

$nameGrid = New-Object System.Windows.Controls.Grid
$nameGroup.Content = $nameGrid
$nameGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition)); $nameGrid.RowDefinitions[0].Height = "Auto"
$nameGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition)); $nameGrid.RowDefinitions[1].Height = "Auto"
$nameGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition)); $nameGrid.RowDefinitions[2].Height = "Auto"
$nameGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $nameGrid.ColumnDefinitions[0].Width = "100"
$nameGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $nameGrid.ColumnDefinitions[1].Width = "2"
$nameGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $nameGrid.ColumnDefinitions[2].Width = "159"
$nameGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $nameGrid.ColumnDefinitions[3].Width = "159"

$noOverwriteStack = New-Object System.Windows.Controls.StackPanel
$noOverwriteStack.Orientation = "Vertical"
$noOverwriteStack.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
$noOverwriteStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Top
$noOverwriteStack.Width = 100
$noOverwriteStack.Margin = "0,8,0,0"
$nameGrid.AddChild($noOverwriteStack)
[System.Windows.Controls.Grid]::SetRow($noOverwriteStack, 0)
[System.Windows.Controls.Grid]::SetRowSpan($noOverwriteStack, 3)
[System.Windows.Controls.Grid]::SetColumn($noOverwriteStack, 0)

$noOverwriteChk = New-Object System.Windows.Controls.CheckBox
$noOverwriteChk.IsChecked = $true
$noOverwriteChk.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
$noOverwriteChk.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
$noOverwriteChk.Margin = "0,0,0,2"
$noOverwriteChk.FontSize = 18
$noOverwriteChk.Padding = New-Object System.Windows.Thickness(0,0,0,0)
$noOverwriteChk.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
$noOverwriteChk.LayoutTransform = New-Object System.Windows.Media.ScaleTransform(3, 3)
$noOverwriteStack.AddChild($noOverwriteChk)

$noOverwriteLabel = New-Object System.Windows.Controls.TextBlock
$noOverwriteLabel.Text = "Don't overwrite"
$noOverwriteLabel.TextWrapping = [System.Windows.TextWrapping]::Wrap
$noOverwriteLabel.TextAlignment = [System.Windows.TextAlignment]::Center
$noOverwriteLabel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
$noOverwriteLabel.Width = 90
$noOverwriteLabel.FontSize = 13
$noOverwriteStack.AddChild($noOverwriteLabel)

$nameSep = New-Object System.Windows.Controls.Border
$nameSep.BorderBrush = [System.Windows.Media.Brushes]::LightGray
$nameSep.BorderThickness = New-Object System.Windows.Thickness(1, 0, 0, 0)
$nameSep.Margin = "0,4,0,4"
$nameSep.Width = 1
$nameGrid.AddChild($nameSep)
[System.Windows.Controls.Grid]::SetRow($nameSep, 0)
[System.Windows.Controls.Grid]::SetRowSpan($nameSep, 3)
[System.Windows.Controls.Grid]::SetColumn($nameSep, 1)

$modePrefix = New-Object System.Windows.Controls.RadioButton; 
$modePrefix.Content = "Prefix"; 
$modePrefix.Margin = "8,6,5,2"
$modePrefix.HorizontalAlignment = "Center"
$nameGrid.AddChild($modePrefix)
[System.Windows.Controls.Grid]::SetColumn($modePrefix, 2)

$modeSuffix = New-Object System.Windows.Controls.RadioButton; 
$modeSuffix.Content = "Suffix"; 
$modeSuffix.IsChecked = $true; 
$modeSuffix.Margin = "0,6,5,2"
$modeSuffix.HorizontalAlignment = "Center"
$nameGrid.AddChild($modeSuffix)
[System.Windows.Controls.Grid]::SetColumn($modeSuffix, 3)

$nameBox = New-Object System.Windows.Controls.TextBox
$nameBox.Text = "_cropped"
$nameBox.Width = 90
$nameBox.Height = 30
$nameBox.MaxLength = 8
$nameBox.TextAlignment = [System.Windows.TextAlignment]::Center
$nameBox.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
$nameBox.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
$nameBox.Margin = "10"
$nameBox.FontSize = 18
$nameGrid.AddChild($nameBox)
[System.Windows.Controls.Grid]::SetColumn($nameBox, 2)
[System.Windows.Controls.Grid]::SetRow($nameBox, 1)

$previewLabel = New-Object System.Windows.Controls.Label; 
$previewLabel.Content = "filename.jpg"; 
$previewLabel.Foreground = [System.Windows.Media.Brushes]::Gray; 
$previewLabel.Margin = "10,0,0,0"
$nameGrid.AddChild($previewLabel)
[System.Windows.Controls.Grid]::SetColumn($previewLabel, 2)
[System.Windows.Controls.Grid]::SetRow($previewLabel, 2)

$unitGroup = New-Object System.Windows.Controls.GroupBox
$unitGroup.Header = "Units"
$unitGroup.Margin = "0,0,0,10"
$controlsPanel.AddChild($unitGroup)

$unitStack = New-Object System.Windows.Controls.StackPanel
$unitStack.Orientation = "Horizontal"
$unitGroup.Content = $unitStack

$unitPixels = New-Object System.Windows.Controls.RadioButton; $unitPixels.Content = "Pixels"; $unitPixels.IsChecked = $true; $unitPixels.Margin = "5"
$unitStack.AddChild($unitPixels)
$unitMM = New-Object System.Windows.Controls.RadioButton; $unitMM.Content = "Millimeters"; $unitMM.Margin = "5"
$unitStack.AddChild($unitMM)
$unitPer = New-Object System.Windows.Controls.RadioButton; $unitPer.Content = "Percentage"; $unitPer.Margin = "5"
$unitStack.AddChild($unitPer)

$dimGroup = New-Object System.Windows.Controls.GroupBox
$dimGroup.Header = "Dimensions"
$dimGroup.Margin = "0,0,0,10"
$controlsPanel.AddChild($dimGroup)

$dimGrid = New-Object System.Windows.Controls.Grid
$dimGroup.Content = $dimGrid
$dimGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition))
$dimGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition))
$dimGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dimGrid.ColumnDefinitions[0].Width = "Auto"
$dimGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dimGrid.ColumnDefinitions[1].Width = "Auto"
$dimGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dimGrid.ColumnDefinitions[2].Width = "Auto"
$dimGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition)); $dimGrid.ColumnDefinitions[3].Width = "Auto"

$sourceDimLabel = New-Object System.Windows.Controls.Label; $sourceDimLabel.Content = "Source:"; $sourceDimLabel.Margin = "5"
$dimGrid.AddChild($sourceDimLabel); [System.Windows.Controls.Grid]::SetRow($sourceDimLabel, 0); [System.Windows.Controls.Grid]::SetColumn($sourceDimLabel, 0)

$widthSourceBox = New-Object System.Windows.Controls.TextBox; $widthSourceBox.Text = "0 px"; $widthSourceBox.Width = 70; $widthSourceBox.IsReadOnly = $true; $widthSourceBox.Background = ConvertTo-SolidColorBrush "LightGray"; $widthSourceBox.TextAlignment = "Center"; $widthSourceBox.Margin = "2"
$dimGrid.AddChild($widthSourceBox); [System.Windows.Controls.Grid]::SetRow($widthSourceBox, 0); [System.Windows.Controls.Grid]::SetColumn($widthSourceBox, 1)

$xLabel = New-Object System.Windows.Controls.Label; $xLabel.Content = "×"; $xLabel.Margin = "5"
$dimGrid.AddChild($xLabel); [System.Windows.Controls.Grid]::SetRow($xLabel, 0); [System.Windows.Controls.Grid]::SetColumn($xLabel, 2)

$heightSourceBox = New-Object System.Windows.Controls.TextBox; $heightSourceBox.Text = "0 px"; $heightSourceBox.Width = 70; $heightSourceBox.IsReadOnly = $true; $heightSourceBox.Background = ConvertTo-SolidColorBrush "LightGray"; $heightSourceBox.TextAlignment = "Center"; $heightSourceBox.Margin = "2"
$dimGrid.AddChild($heightSourceBox); [System.Windows.Controls.Grid]::SetRow($heightSourceBox, 0); [System.Windows.Controls.Grid]::SetColumn($heightSourceBox, 3)

$outputDimLabel = New-Object System.Windows.Controls.Label; $outputDimLabel.Content = "Output:"; $outputDimLabel.Margin = "5"
$dimGrid.AddChild($outputDimLabel); [System.Windows.Controls.Grid]::SetRow($outputDimLabel, 1); [System.Windows.Controls.Grid]::SetColumn($outputDimLabel, 0)

$widthBox = New-Object System.Windows.Controls.TextBox; $widthBox.Text = "1024 px"; $widthBox.Width = 70; $widthBox.TextAlignment = "Center"; $widthBox.Margin = "2"
Allow-OnlyNumbers $widthBox
$dimGrid.AddChild($widthBox); [System.Windows.Controls.Grid]::SetRow($widthBox, 1); [System.Windows.Controls.Grid]::SetColumn($widthBox, 1)

$xLabel2 = New-Object System.Windows.Controls.Label; $xLabel2.Content = "×"; $xLabel2.Margin = "5"
$dimGrid.AddChild($xLabel2); [System.Windows.Controls.Grid]::SetRow($xLabel2, 1); [System.Windows.Controls.Grid]::SetColumn($xLabel2, 2)

$heightBox = New-Object System.Windows.Controls.TextBox; $heightBox.Text = "768 px"; $heightBox.Width = 70; $heightBox.TextAlignment = "Center"; $heightBox.Margin = "2"
Allow-OnlyNumbers $heightBox
$dimGrid.AddChild($heightBox); [System.Windows.Controls.Grid]::SetRow($heightBox, 1); [System.Windows.Controls.Grid]::SetColumn($heightBox, 3)

$buttonStack = New-Object System.Windows.Controls.StackPanel
$buttonStack.Orientation = "Horizontal"
$buttonStack.Margin = "0,0,0,10"
$controlsPanel.AddChild($buttonStack)

$resetBtn = New-Object System.Windows.Controls.Button; $resetBtn.Content = "Reset"; $resetBtn.Width = 80; $resetBtn.Height = 30; $resetBtn.Margin = "0,0,5,0"
$buttonStack.AddChild($resetBtn)

$cropBtn = New-Object System.Windows.Controls.Button; $cropBtn.Content = "CROP IMAGES"; $cropBtn.Width = 100; $cropBtn.Height = 30; $cropBtn.Margin = "0,0,5,0"; $cropBtn.Background = ConvertTo-SolidColorBrush "LightGreen"
$buttonStack.AddChild($cropBtn)

$cancelBtn = New-Object System.Windows.Controls.Button; $cancelBtn.Content = "Cancel"; $cancelBtn.Width = 80; $cancelBtn.Height = 30; $cancelBtn.IsEnabled = $false
$buttonStack.AddChild($cancelBtn)

$progress = New-Object System.Windows.Controls.ProgressBar
$progress.Height = 20
$progress.Margin = "0,0,0,10"
$controlsPanel.AddChild($progress)

$logLabel = New-Object System.Windows.Controls.Label
$logLabel.Content = "Log:"
$logLabel.FontWeight = "Bold"
$controlsPanel.AddChild($logLabel)

$global:logTextBox = New-Object System.Windows.Controls.TextBox
$global:logTextBox.IsReadOnly = $true
$global:logTextBox.TextWrapping = "Wrap"
$global:logTextBox.AcceptsReturn = $true
$global:logTextBox.VerticalScrollBarVisibility = "Auto"
$global:logTextBox.Height = 90
$global:logTextBox.Background = ConvertTo-SolidColorBrush "LightGray"
$controlsPanel.AddChild($global:logTextBox)

$script:controlsCreated = $true
#endregion

#region Event Handlers
# -------------------------------------------------------------------------------------------------
$resetBtn.Add_Click({
    $unitPixels.IsChecked = $true
    $modeSuffix.IsChecked = $true
    $widthBox.Text = "1024 px"
    $heightBox.Text = "768 px"
    $nameBox.Text = "_cropped"
    $progress.Value = 0
    $script:realCropRect = $null
    Log "Settings reset"
    if ($script:previewImage) { Update-PreviewRect }
})

$cropBtn.Add_Click({
    if (-not (Check-OverwriteWarning)) {
        Log "Crop cancelled - overwrite not confirmed"
        return
    }
    
    $script:cancel = $false
    $cancelBtn.IsEnabled = $true
    $cropBtn.IsEnabled = $false
    $progress.Value = 0
    
    $source = Get-TextboxValue $srcBox
    $dest = Get-TextboxValue $dstBox
    
    if ([string]::IsNullOrWhiteSpace($dest)) {
        $dest = if (Test-Path $source -PathType Container) { $source } else { Split-Path $source }
    }
    
    $target = Get-TargetSize
    if (-not $target) {
        Log "Invalid target dimensions" $true
        $cancelBtn.IsEnabled = $false
        $cropBtn.IsEnabled = $true
        return
    }
    
    $files = @()
    if (Test-Path $source -PathType Container) {
        $files = Get-ChildItem $source -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' }
    } else {
        $files = @(Get-Item $source)
    }
    
    $total = $files.Count
    $croppedCount = 0
    $index = 0
    $cleanSuffix = $nameBox.Text -replace '[\\/:*?"<>|]', ''
    
    if (-not (Test-Path $dest)) {
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
    }
    
    foreach ($f in $files) {
        $index++
        if ($script:cancel) { Log "Cancelled by user"; break }
        
        $percentComplete = if ($total -gt 0) { [math]::Round(($index / $total) * 100) } else { 0 }
        $progress.Value = $percentComplete
        Log "Processing $($f.Name)..."
        Invoke-ProcessEvents
        
        try {
            $img = [System.Drawing.Image]::FromFile($f.FullName)
            $img = Apply-ExifRotation($img)
            
            $cropRect = $script:realCropRect
            if (-not $cropRect) {
                $cropRect = New-Object System.Drawing.Rectangle 0, 0, $img.Width, $img.Height
            }
            
            $croppedImg = $img.Clone($cropRect, $img.PixelFormat)
            
            $finalImg = $croppedImg
            if ($croppedImg.Width -ne $target.Width -or $croppedImg.Height -ne $target.Height) {
                $finalImg = New-Object System.Drawing.Bitmap($croppedImg, $target.Width, $target.Height)
                $croppedImg.Dispose()
            }
            
            Copy-AllExifData $img $finalImg
            Set-OrientationToNormal $finalImg
            
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            $ext = $f.Extension
            $newName = if ($modeSuffix.IsChecked) { "$baseName$cleanSuffix$ext" } else { "$cleanSuffix$baseName$ext" }
            
            $outputPath = Join-Path $dest $newName
            
            if ($noOverwriteChk.IsChecked) {
                $counter = 1
                $originalPath = $outputPath
                while (Test-Path $outputPath) {
                    $newName = [System.IO.Path]::GetFileNameWithoutExtension($originalPath) + "_$counter" + $ext
                    $outputPath = Join-Path $dest $newName
                    $counter++
                }
            }
            
            if ($ext -match '\.jpe?g$') {
                $encoder = [System.Drawing.Imaging.Encoder]::Quality
                $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
                $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter($encoder, 95L)
                $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.FormatDescription -eq "JPEG" }
                $finalImg.Save($outputPath, $codec, $encoderParams)
            } else {
                $finalImg.Save($outputPath)
            }
            
            Copy-FileDates $f.FullName $outputPath
            $finalImg.Dispose()
            $img.Dispose()
            $croppedCount++
            Log "Saved: $newName"
        }
        catch {
            Log "Error processing $($f.Name): $($_.Exception.Message)" $true
        }
    }
    
    if ($openAfterChk.IsChecked -and $croppedCount -gt 0) {
        Start-Process $dest
    }
    
    $cancelBtn.IsEnabled = $false
    $cropBtn.IsEnabled = $true
    Log "Done. Processed $croppedCount of $total images."
})

$cancelBtn.Add_Click({
    $script:cancel = $true
    $cancelBtn.IsEnabled = $false
    Log "Operation cancelled"
})

$nameBox.Add_TextChanged({ Update-FilenamePreview })

$widthBox.Add_TextChanged({ 
    if (-not $script:isUpdatingBoxes) { 
        if ($script:previewImage) {
            $target = Get-TargetSize
            if ($target -and $script:realCropRect) {
                $script:realCropRect.Width = [math]::Min($target.Width, $script:previewImage.Width)
                Update-PreviewRect
            }
        }
        Update-UnitsDisplay
    }
})

$heightBox.Add_TextChanged({ 
    if (-not $script:isUpdatingBoxes) { 
        if ($script:previewImage) {
            $target = Get-TargetSize
            if ($target -and $script:realCropRect) {
                $script:realCropRect.Height = [math]::Min($target.Height, $script:previewImage.Height)
                Update-PreviewRect
            }
        }
        Update-UnitsDisplay
    }
})

$unitPixels.Add_Checked({
    if ($unitPixels.IsChecked) {
        Update-UnitsDisplay
        Update-SourceSizeBoxes
        Log "Switched to Pixels"
    }
})

$unitMM.Add_Checked({
    if ($unitMM.IsChecked) {
        Update-UnitsDisplay
        Update-SourceSizeBoxes
        Log "Switched to Millimeters"
    }
})

$modePrefix.Add_Checked({ Update-FilenamePreview })
$modeSuffix.Add_Checked({ Update-FilenamePreview })

$srcBox.Add_TextChanged({
    $validationTimer.Stop()
    $validationTimer.Start()
})

$srcBox.Add_LostFocus({
    $text = Get-TextboxValue $srcBox
    if (-not [string]::IsNullOrWhiteSpace($text)) {
        $srcBox.Text = Normalize-Path $text
    }
    Test-PathBox $srcBox
})

$dstBox.Add_TextChanged({
    $validationTimer.Stop()
    $validationTimer.Start()
})

$dstBox.Add_LostFocus({
    $text = Get-TextboxValue $dstBox
    if (-not [string]::IsNullOrWhiteSpace($text)) {
        $dstBox.Text = Normalize-Path $text
    }
    Test-PathBox $dstBox
})

$form.Add_SizeChanged({
    if ($script:previewImage) {
        FitToWindow
        Update-PreviewRect
    }
})
#endregion

#region Initialize and Show
# -------------------------------------------------------------------------------------------------
Log "Image Crop Tool - WPF Version Started"
Update-FilenamePreview

$form.ShowDialog() | Out-Null

if ($script:previewImage) { $script:previewImage.Dispose() }

return
#endregion