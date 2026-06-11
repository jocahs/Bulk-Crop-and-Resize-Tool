# =================================================================================================
# IMAGE CROP TOOL - by João Silva
# =================================================================================================

#region Initialization and Console Hiding
# -------------------------------------------------------------------------------------------------
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

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

Add-Type -AssemblyName PresentationFramework

$null = [Win32]::ShowWindow([Win32]::GetConsoleWindow(), 0)
$null = [System.Console]::SetError([System.IO.TextWriter]::Null)
$null = [System.Console]::SetOut([System.IO.TextWriter]::Null)

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
#endregion

#region Global Variables
# -------------------------------------------------------------------------------------------------
$form = New-Object Windows.Forms.Form
$form.Text = "Image Crop Tool by João Silva"
$form.Size = "615,340"
$form.MinimumSize = $form.Size

$previewForm = New-Object Windows.Forms.Form
$previewForm.Text = "Image Preview - Crop Tool"
$previewForm.Size = "600,600"
$previewForm.MinimumSize = "340,340"
$previewForm.StartPosition = "Manual"
$previewForm.FormBorderStyle = "SizableToolWindow"

$previewPanel = New-Object Windows.Forms.Panel
$previewPanel.Dock = "Fill"
$previewPanel.AutoScroll = $true
$previewForm.Controls.Add($previewPanel)

$previewBox = New-Object Windows.Forms.PictureBox
$previewBox.SizeMode = "Zoom"
$previewBox.BackColor = [System.Drawing.Color]::FromArgb(248, 248, 248)
$previewBox.AllowDrop = $true
$previewPanel.Controls.Add($previewBox)

# Script-scope variables
$script:cancel = $false
$script:dstSuggestionShown = $false
$script:dstPendingSuggestion = $false
$script:dstPendingPath = ""
$script:previewImage = $null
$script:realCropRect = $null
$script:previewRect = $null
$script:isDragging = $false
$script:resizeMode = ""
$script:isUpdatingBoxes = $false
$script:snapThreshold = 8
$script:snapActive = $false
$script:currentOrientation = 1
$script:orientationCache = @{}
$script:currentFileName = "filename.jpg"
$script:scaleFactor = 1
$script:lastLoadedSource = ""
$script:isPanning = $false
$script:panStartPoint = $null
$script:currentZoom = 1.0

try {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) { Write-Debug "Admin mode - Drag & Drop may not work" }
} catch { }
#endregion

#region Logging
# -------------------------------------------------------------------------------------------------
function Log($msg, $isError = $false) {
    $line = "$(Get-Date -Format "HH:mm:ss") - $msg"
    try {
        if ($isError) { $log.AppendText("ERROR: $line`r`n") }
        else { $log.AppendText("$line`r`n") }
    } catch { }
}
#endregion

#region Helper Functions
# -------------------------------------------------------------------------------------------------
function Add-Label($text, $x, $y) {
    $label = New-Object System.Windows.Controls.TextBox
    $label.Background = [System.Windows.Media.Brushes]::Transparent
    $label.Text = $text
    $label.Location = "$x,$y"
    $label.ForeColor = [System.Drawing.Color]::Black
    $label.AutoSize = $true
    $form.Controls.Add($label)
    return @{ Label = $label}
}

function Add-Textbox($x, $y, $w) {
    $t = New-Object Windows.Forms.TextBox
    $t.Location = "$x,$y"
    $t.Size = "$w,20"
    $form.Controls.Add($t)
    return $t
}

function Add-TextboxWithIcon($x, $y, $w) {
    $panel = New-Object Windows.Forms.Panel
    $panel.Location = "$x,$y"
    $panel.Size = "$w,24"
    $panel.BorderStyle = "FixedSingle"
    $panel.BackColor = [System.Drawing.Color]::White
    $textbox = New-Object Windows.Forms.TextBox
    $textbox.BorderStyle = "None"
    $textbox.Margin = "0,0,0,0"
    $textbox.Location = "4,4"
    $textbox.Width = $w - 30
    $textbox.Anchor = "Top,Left,Right"
    $textbox.BackColor = $panel.BackColor
    $icon = New-Object Windows.Forms.Label
    $icon.Size = "20,20"
    $icon.Location = "$($w-24),2"
    $icon.TextAlign = "MiddleCenter"
    $icon.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
    $panel.Controls.Add($textbox)
    $panel.Controls.Add($icon)
    $form.Controls.Add($panel)
    return @{ Panel = $panel; TextBox = $textbox; Icon = $icon }
}

function Get-TextboxValue($textbox) {
    if ($textbox.Text -eq $textbox.Tag -and $textbox.ForeColor -eq [System.Drawing.Color]::Gray) { return "" }
    return $textbox.Text
}

function Set-InputColor($textbox, $color) {
    if ($textbox -and $textbox.Parent) {
        $textbox.Parent.BackColor = $color
        $textbox.BackColor = $color
    }
}

function Set-Placeholder($textbox, $text) {
    $textbox.Tag = $text
    $textbox.Text = $text
    $textbox.ForeColor = [System.Drawing.Color]::Gray
    $textbox.Anchor = "Top,Left,Right"
    $textbox.Add_Enter({
        if ($this.ForeColor -eq [System.Drawing.Color]::Gray -and $this.Text -eq $this.Tag) {
            $this.Text = ""
            $this.ForeColor = [System.Drawing.Color]::Black
        }
    })
    $textbox.Add_Leave({
        if ([string]::IsNullOrWhiteSpace($this.Text)) {
            $this.Text = $this.Tag
            $this.ForeColor = [System.Drawing.Color]::Gray
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

function Update-PreviewBoxAppearance {
    if (-not $previewBox) { return }
    try {
        $previewBox.BackColor = if ($script:previewImage) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(248, 248, 248) }
        $previewBox.Refresh()
    } catch { }
}
#endregion

#region Path Validation Functions
# -------------------------------------------------------------------------------------------------
function Set-ErrorMessage($textbox, $message) {
    if ($textbox -eq $srcBox -and $srcError) { $srcError.Text = $message }
    elseif ($textbox -eq $dstBox -and $dstError) { $dstError.Text = $message }
    Position-ErrorLabel $textbox
}

function Position-ErrorLabel($textbox) {
    if (-not $form -or -not $srcUI -or -not $dstUI) { return }
    
    if ($textbox -eq $srcBox -and $srcError -and $srcValidIcon -and $srcUI.Panel) {
        $label = $srcError
        $icon = $srcValidIcon
        $panel = $srcUI.Panel
    } elseif ($textbox -eq $dstBox -and $dstError -and $dstValidIcon -and $dstUI.Panel) {
        $label = $dstError
        $icon = $dstValidIcon
        $panel = $dstUI.Panel
    } else { return }
    
    try {
        $label.Top = $panel.Top - 17
        $iconScreenPos = $icon.PointToScreen([System.Drawing.Point]::Empty)
        $formScreenPos = $form.PointToScreen([System.Drawing.Point]::Empty)
        $iconX = $iconScreenPos.X - $formScreenPos.X
        $textSize = [System.Windows.Forms.TextRenderer]::MeasureText($label.Text, $label.Font, [System.Drawing.Size]::Empty, [System.Windows.Forms.TextFormatFlags]::NoPadding)
        $label.Left = $iconX + $icon.Width - $textSize.Width
    } catch {
        # Ignore positioning errors
    }
}

function Update-RunState {
    $source = Get-TextboxValue $srcBox
    $destText = Get-TextboxValue $dstBox
    $sourceValid = $false
    
    if ($source -and (Test-Path $source)) {
        if (Test-Path $source -PathType Container) {
            $images = Get-ChildItem $source -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' }
            if ($images) {
                foreach ($img in $images) {
                    $ext = $img.Extension.ToLower()
                    if ($ext -match '\.jpe?g$') {
                        if (Test-JpegValid $img.FullName) {
                            $sourceValid = $true
                            break
                        }
                    } else {
                        $sourceValid = $true
                        break
                    }
                }
            }
        } elseif (Test-Path $source -PathType Leaf) {
            $ext = [System.IO.Path]::GetExtension($source).ToLower()
            if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                if ($ext -match '\.jpe?g$') {
                    $sourceValid = Test-JpegValid $source
                } else {
                    $sourceValid = $true
                }
            }
        }
    }
    
    $destValid = if ([string]::IsNullOrWhiteSpace($destText)) {
        $true
    } elseif ([System.IO.Path]::HasExtension($destText) -or (Test-Path $destText -PathType Leaf)) {
        $false
    } elseif (Test-Path $destText -PathType Container) {
        $true
    } else {
        try {
            $root = [System.IO.Path]::GetPathRoot($destText)
            if (-not $root -or -not (Test-Path $root)) { throw }
            [System.IO.Path]::GetFullPath($destText) | Out-Null
            $true
        } catch { $false }
    }
    
    if ($cropBtn) { $cropBtn.Enabled = ($sourceValid -and $destValid) }
}

function Validate-PathBox($textbox) {
    if (-not $textbox) { return }
    
    $realText = Get-TextboxValue $textbox
    $icon = if ($textbox -eq $srcBox -and $srcValidIcon) { $srcValidIcon } 
            elseif ($textbox -eq $dstBox -and $dstValidIcon) { $dstValidIcon } 
            else { return }
    $isDest = ($textbox -eq $dstBox)

    if ($textbox.ForeColor -eq [System.Drawing.Color]::Gray -or [string]::IsNullOrWhiteSpace($realText)) {
        Set-InputColor $textbox ([System.Drawing.Color]::LightYellow)
        $icon.Text = ""
        Set-ErrorMessage $textbox ""
        return
    }
    
    if (Test-Path $realText -PathType Container) {
        if (-not $isDest) {
            $hasImages = $false
            $images = Get-ChildItem $realText -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' }
            if ($images) {
                foreach ($img in $images) {
                    $ext = $img.Extension.ToLower()
                    if ($ext -match '\.jpe?g$') {
                        if (Test-JpegValid $img.FullName) {
                            $hasImages = $true
                            break
                        }
                    } else {
                        $hasImages = $true
                        break
                    }
                }
            }
            
            if ($hasImages) {
                Set-InputColor $textbox ([System.Drawing.Color]::LightGreen)
                $icon.Text = "✔"; $icon.ForeColor = "Green"
                Set-ErrorMessage $textbox "Valid folder with images"
            } else {
                Set-InputColor $textbox ([System.Drawing.Color]::LightYellow)
                $icon.Text = "⚠"; $icon.ForeColor = "Orange"
                Set-ErrorMessage $textbox "Folder contains no valid images"
            }
        } else {
            Set-InputColor $textbox ([System.Drawing.Color]::LightGreen)
            $icon.Text = "✔"; $icon.ForeColor = "Green"
            Set-ErrorMessage $textbox "Valid destination folder"
        }
    }
    elseif (-not $isDest -and (Test-Path $realText -PathType Leaf)) {
        Set-InputColor $textbox ([System.Drawing.Color]::LightGreen)
        $icon.Text = "✔"; $icon.ForeColor = "Green"
        Set-ErrorMessage $textbox "Valid file"
    }
    elseif ($isDest) {
        $isFileLike = (Test-Path $realText -PathType Leaf) -or ([System.IO.Path]::HasExtension($realText))
        if ($isFileLike) {
            if (-not $script:dstSuggestionShown) {
                $script:dstPendingSuggestion = $true
                $script:dstPendingPath = $realText
            }
            Set-InputColor $textbox ([System.Drawing.Color]::LightCoral)
            $icon.Text = "✖"; $icon.ForeColor = "Red"
            Set-ErrorMessage $textbox "Invalid: destination must be a folder"
            return
        }
        
        try {
            $root = [System.IO.Path]::GetPathRoot($realText)
            if (-not $root -or -not (Test-Path $root)) { throw }
            [System.IO.Path]::GetFullPath($realText) | Out-Null
            Set-InputColor $textbox ([System.Drawing.Color]::LightBlue)
            $icon.Text = "➕"; $icon.ForeColor = "Blue"
            Set-ErrorMessage $textbox "Folder will be created"
        } catch {
            Set-InputColor $textbox ([System.Drawing.Color]::LightCoral)
            $icon.Text = "✖"; $icon.ForeColor = "Red"
            Set-ErrorMessage $textbox "Invalid path"
        }
    }
    else {
        Set-InputColor $textbox ([System.Drawing.Color]::LightCoral)
        $icon.Text = "✖"; $icon.ForeColor = "Red"
        Set-ErrorMessage $textbox "Invalid or not existing source path"
    }
    Update-RunState
}
#endregion

#region Unit and Size Functions
# -------------------------------------------------------------------------------------------------
function Get-UnitSuffix {
    return $(if ($unitMM -and $unitMM.Checked) { "mm" } else { "px" })
}

function Add-UnitToBox($textbox, $unit) {
    if ($textbox -and $textbox.ForeColor -ne [System.Drawing.Color]::Gray -and $textbox.Text -ne "" -and $textbox.Text -notmatch "$unit$") {
        $textbox.Text = "$($textbox.Text) $unit"
    }
}

function Remove-UnitFromBox($textbox, $unit) {
    if ($textbox -and $textbox.Text -match "\s*$unit$") {
        $textbox.Text = $textbox.Text -replace "\s*$unit$", ""
    }
}

function Allow-OnlyNumbers($textbox) {
    if (-not $textbox) { return }
    $textbox.Add_KeyPress({
        if (-not [char]::IsDigit($_.KeyChar) -and $_.KeyChar -ne [char]8) {
            $_.Handled = $true
            return
        }
        $clean = $this.Text -replace '[^\d]', ''
        if ($clean.Length -ge 5 -and $_.KeyChar -ne [char]8) {
            $_.Handled = $true
        }
    })
}

function Get-TargetSize {
    if (-not $widthBox -or -not $heightBox) { return $null }
    $wRaw = $widthBox.Text -replace '[^\d]', ''
    $hRaw = $heightBox.Text -replace '[^\d]', ''
    if (-not $wRaw -or -not $hRaw) { return $null }
    
    if ($unitMM -and $unitMM.Checked) {
        $dpi = 96
        return @{ 
            Width = [int](($wRaw / 25.4) * $dpi)
            Height = [int](($hRaw / 25.4) * $dpi)
        }
    } else {
        return @{ Width = [int]$wRaw; Height = [int]$hRaw }
    }
}

function Update-UnitsDisplay {
    if (-not $script:realCropRect) { 
        $script:isUpdatingBoxes = $true
        
        if ($widthBox -and $widthBox.Text) {
            $currentValue = $widthBox.Text -replace '\s*(?:px|mm)$', ''
            if ($currentValue -match '^\d+$') {
                $num = [int]$currentValue
                if ($unitMM -and $unitMM.Checked) {
                    $converted = [math]::Round(($num / 96) * 25.4)
                    $widthBox.Text = "$converted mm"
                } else {
                    $converted = [math]::Round(($num / 25.4) * 96)
                    $widthBox.Text = "$converted px"
                }
            } else {
                $widthBox.Text = if ($unitMM -and $unitMM.Checked) { "0 mm" } else { "0 px" }
            }
        }
        
        if ($heightBox -and $heightBox.Text) {
            $currentValue = $heightBox.Text -replace '\s*(?:px|mm)$', ''
            if ($currentValue -match '^\d+$') {
                $num = [int]$currentValue
                if ($unitMM -and $unitMM.Checked) {
                    $converted = [math]::Round(($num / 96) * 25.4)
                    $heightBox.Text = "$converted mm"
                } else {
                    $converted = [math]::Round(($num / 25.4) * 96)
                    $heightBox.Text = "$converted px"
                }
            } else {
                $heightBox.Text = if ($unitMM -and $unitMM.Checked) { "0 mm" } else { "0 px" }
            }
        }
        
        $script:isUpdatingBoxes = $false
        return
    }
    
    $w = $script:realCropRect.Width
    $h = $script:realCropRect.Height
    
    if ($unitMM -and $unitMM.Checked) {
        $dpi = 96
        $w = [math]::Round(($w / $dpi) * 25.4)
        $h = [math]::Round(($h / $dpi) * 25.4)
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
        if ($widthSourceBox -and $widthSourceBox.Text) {
            $currentValue = $widthSourceBox.Text -replace '\s*(?:px|mm)$', ''
            if ($currentValue -match '^\d+$') {
                $num = [int]$currentValue
                if ($unitMM -and $unitMM.Checked) {
                    $converted = [math]::Round(($num / 96) * 25.4)
                    $widthSourceBox.Text = "$converted mm"
                } else {
                    $converted = [math]::Round(($num / 25.4) * 96)
                    $widthSourceBox.Text = "$converted px"
                }
            } else {
                $widthSourceBox.Text = if ($unitMM -and $unitMM.Checked) { "0 mm" } else { "0 px" }
            }
        }
        
        if ($heightSourceBox -and $heightSourceBox.Text) {
            $currentValue = $heightSourceBox.Text -replace '\s*(?:px|mm)$', ''
            if ($currentValue -match '^\d+$') {
                $num = [int]$currentValue
                if ($unitMM -and $unitMM.Checked) {
                    $converted = [math]::Round(($num / 96) * 25.4)
                    $heightSourceBox.Text = "$converted mm"
                } else {
                    $converted = [math]::Round(($num / 25.4) * 96)
                    $heightSourceBox.Text = "$converted px"
                }
            } else {
                $heightSourceBox.Text = if ($unitMM -and $unitMM.Checked) { "0 mm" } else { "0 px" }
            }
        }
        return
    }
    
    $w = $script:previewImage.Width
    $h = $script:previewImage.Height
    
    if ($unitMM -and $unitMM.Checked) {
        $dpi = 96
        $w = [math]::Round(($w / $dpi) * 25.4)
        $h = [math]::Round(($h / $dpi) * 25.4)
        $unit = "mm"
    } else {
        $unit = "px"
    }
    
    if ($widthSourceBox) { $widthSourceBox.Text = "$w $unit" }
    if ($heightSourceBox) { $heightSourceBox.Text = "$h $unit" }
}
#endregion

#region Filename Functions
# -------------------------------------------------------------------------------------------------
function Update-FilenamePreview {
    if (-not $script:currentFileName) { return }
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($script:currentFileName)
    $ext = [System.IO.Path]::GetExtension($script:currentFileName)
    $userText = $nameBox.Text -replace '[\\/:*?"<>|\s]', ''
    
    $final = if ($modeSuffix -and $modeSuffix.Checked) { "$baseName$userText$ext" } else { "$userText$baseName$ext" }
    
    if ($previewLabel) {
        $previewLabel.Text = $final
        $previewLabel.AutoSize = $true
        $previewLabel.Update()
        $previewLabel.Left = 380 - ($previewLabel.Width / 2)
    }
}

function Filename-Format {
    if (-not $nameExample -or -not $nameBox -or -not $plusLabel) { return }
    if ($modeSuffix -and $modeSuffix.Checked) {
        $nameExample.Location = "312,172"
        $nameBox.Location = "393,170"
    } elseif ($modePrefix -and $modePrefix.Checked) {
        $nameBox.Location = "312,168"
        $nameExample.Location = "393,170"
    }
    $nameBox.BringToFront()
    $plusLabel.BringToFront()
    $nameExample.BringToFront()
    $form.Refresh()
}

function Check-OverwriteWarning {
    $suffix = $nameBox.Text -replace '[\\/:*?"<>|\s]', ''
    if ([string]::IsNullOrWhiteSpace($suffix) -and (-not $noOverwriteChk.Checked)) {
        $result = [System.Windows.Forms.MessageBox]::Show(
            $form,
            "WARNING: The name field is empty and 'Don't overwrite' is NOT checked!`n`nThis means existing files will be OVERWRITTEN if they have the original name.`n`nDo you want to continue?",
            "Overwrite Warning",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Warning
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
        Log "Could not set orientation to 1: $($_.Exception.Message)" $true
    }
}
#endregion

#region Preview Functions
# -------------------------------------------------------------------------------------------------
function Update-PreviewRect {
    if (-not $script:previewImage -or -not $script:realCropRect -or -not $previewBox) { return }
    
    $img = $script:previewImage
    $r = $script:realCropRect
    $boxW = $previewBox.Width
    $boxH = $previewBox.Height
    
    $scale = [math]::Min([double]$boxW / $img.Width, [double]$boxH / $img.Height)
    $displayedW = $img.Width * $scale
    $displayedH = $img.Height * $scale
    $offsetX = ($boxW - $displayedW) / 2
    $offsetY = ($boxH - $displayedH) / 2
    
    $script:scaleFactor = $scale
    if ($r.Width -le 0 -or $r.Height -le 0) { return }
    
    $script:previewRect = New-Object System.Drawing.Rectangle(
        [int]($offsetX + ($r.X * $scale)),
        [int]($offsetY + ($r.Y * $scale)),
        [int]($r.Width * $scale),
        [int]($r.Height * $scale)
    )
    $previewBox.Invalidate()
}

function Update-PreviewCrop {
    if (-not $script:previewImage) { return }
    $size = Get-TargetSize
    if (-not $size) { return }
    
    $targetW = [int]$size.Width
    $targetH = [int]$size.Height
    $imgW = $script:previewImage.Width
    $imgH = $script:previewImage.Height
    $cropW = [math]::Min($targetW, $imgW)
    $cropH = [math]::Min($targetH, $imgH)
    
    if (-not $script:realCropRect -or $script:realCropRect.Width -le 0 -or $script:realCropRect.Height -le 0) {
        $script:realCropRect = New-Object System.Drawing.Rectangle 0, 0, $cropW, $cropH
    }
    
    $r = $script:realCropRect
    $r.X = [math]::Max(0, [math]::Min($r.X, $imgW - $cropW))
    $r.Y = [math]::Max(0, [math]::Min($r.Y, $imgH - $cropH))
    $r.Width = $cropW
    $r.Height = $cropH
    
    $script:realCropRect = $r
    Update-PreviewRect
    if ($previewBox) { $previewBox.Invalidate() }
}

function Load-PreviewImage($path) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path $path)) {
        Update-PreviewBoxAppearance
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
        }
    }
    
    $script:currentOrientation = 1
    
    if ($script:previewImage) {
        $script:previewImage.Dispose()
        $script:previewImage = $null
    }
    
    try {
        $img = $null
        
        if (Test-Path $path -PathType Leaf) {
            $ext = [System.IO.Path]::GetExtension($path).ToLower()
            if ($ext -notin '.jpg', '.jpeg', '.png', '.bmp', '.gif') {
                Update-PreviewBoxAppearance
                return
            }
            
            try {
                $img = [System.Drawing.Image]::FromFile($path)
            } catch {
                $img = $null
            }
            
            if (-not $img) {
                Log "Failed to load image: $(Split-Path $path -Leaf)" $true
                Update-PreviewBoxAppearance
                return
            }
        }
        else {
            $validFile = Get-ChildItem $path -File | Where-Object {
                $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$'
            } | Select-Object -First 1
            
            if (-not $validFile) {
                Log "No valid images found in folder" $true
                Update-PreviewBoxAppearance
                return
            }
            
            $img = $null
            try {
                $img = [System.Drawing.Image]::FromFile($validFile.FullName)
            } catch {
                $img = $null
            }
            
            if (-not $img) {
                Log "Failed to load image from folder" $true
                Update-PreviewBoxAppearance
                return
            }
        }
        
        if ($img.Width -eq 0 -or $img.Height -eq 0) {
            $img.Dispose()
            Update-PreviewBoxAppearance
            return
        }
        
        $previewImg = $img.Clone()
        $img.Dispose()
        $previewImg = Apply-ExifRotation($previewImg)
        
        $script:previewImage = $previewImg
        if ($previewBox) { $previewBox.Image = $previewImg }
        Update-PreviewBoxAppearance
        Update-SourceSizeBoxes
        Update-PreviewCrop
        Update-FilenamePreview
        
        $script:currentZoom = 1.0
        if ($previewBox) {
            $previewBox.SizeMode = "Zoom"
            $previewBox.Width = $previewPanel.ClientSize.Width
            $previewBox.Height = $previewPanel.ClientSize.Height
            $previewBox.Invalidate()
        }
        
    } catch {
        if ($_.Exception.Message -match "Invalid SOS parameters") {
        } else {
            Log "Preview load failed: $($_.Exception.Message)" $true
            $script:previewImage = $null
            if ($previewBox) { $previewBox.Image = $null }
            Update-PreviewBoxAppearance
        }
    }
}
#endregion

#region Browse Functions
# -------------------------------------------------------------------------------------------------
function BrowseSourceModern($targetBox) {
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.CheckFileExists = $false
    $dlg.ValidateNames = $false
    $dlg.FileName = "Select folder or file"
    $dlg.Title = "Select file or folder"
    $dlg.Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
    $dlg.FilterIndex = 1
    $dlg.Multiselect = $true
    $dlg.RestoreDirectory = $true
    $dlg.AutoUpgradeEnabled = $true
    
    $currentPath = Get-TextboxValue $targetBox
    if (-not [string]::IsNullOrWhiteSpace($currentPath) -and (Test-Path $currentPath)) {
        if (Test-Path $currentPath -PathType Container) {
            $dlg.InitialDirectory = $currentPath
        } else {
            $dlg.InitialDirectory = Split-Path $currentPath
        }
    } else {
        $currentDir = if ($PSScriptRoot) { $PSScriptRoot } else { [Environment]::CurrentDirectory }
        if (Test-Path $currentDir) {
            $dlg.InitialDirectory = $currentDir
        } else {
            $dlg.InitialDirectory = [Environment]::GetFolderPath("Desktop")
        }
    }
    
    if ($dlg.ShowDialog($form) -eq "OK") {
        $rawPath = $dlg.FileName
        
        if ($rawPath -match "Select folder") {
            $path = Split-Path $rawPath
        }
        elseif (Test-Path $rawPath -PathType Container) {
            $path = $rawPath
        }
        else {
            $path = $rawPath
        }
        
        if (Test-Path $path -PathType Container) {
            $targetBox.Text = $path
            $targetBox.ForeColor = [System.Drawing.Color]::Black
    
            $destText = $dstBox.Text
            if ([string]::IsNullOrWhiteSpace($destText) -or $dstBox.ForeColor -eq [System.Drawing.Color]::Gray) {
                $dstBox.Text = $path
                $dstBox.ForeColor = [System.Drawing.Color]::Black
                Validate-PathBox $dstBox
            }
    
            $validationTimer.Stop()
            $validationTimer.Start()
        }
        elseif (Test-Path $path -PathType Leaf) {
            $ext = [System.IO.Path]::GetExtension($path).ToLower()
            if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                $targetBox.Text = $path
                $targetBox.ForeColor = [System.Drawing.Color]::Black
                $file = Get-Item $path
                $script:currentFileName = $file.Name
                Load-PreviewImage $path
            } else {
                [System.Windows.Forms.MessageBox]::Show("The selected file is not an image file.`nPlease select an image file or folder containing images.", "Invalid File Type", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
                $targetBox.Text = ""
                return
            }
        }
        Update-FilenamePreview
        Validate-PathBox $targetBox
    }
}

function BrowseFolder($targetBox) {
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    
    $currentPath = Get-TextboxValue $targetBox
    
    if (-not [string]::IsNullOrWhiteSpace($currentPath) -and (Test-Path $currentPath)) {
        if (Test-Path $currentPath -PathType Container) {
            $dlg.SelectedPath = $currentPath
        } else {
            $dlg.SelectedPath = Split-Path $currentPath
        }
    } else {
        $sourcePath = Get-TextboxValue $srcBox
        if (-not [string]::IsNullOrWhiteSpace($sourcePath) -and (Test-Path $sourcePath)) {
            if (Test-Path $sourcePath -PathType Container) {
                $dlg.SelectedPath = $sourcePath
            } else {
                $dlg.SelectedPath = Split-Path $sourcePath
            }
        } else {
            $currentDir = if ($PSScriptRoot) { $PSScriptRoot } else { [Environment]::CurrentDirectory }
            if (Test-Path $currentDir) {
                $dlg.SelectedPath = $currentDir
            } else {
                $dlg.SelectedPath = [Environment]::GetFolderPath("Desktop")
            }
        }
    }
    
    $dlg.Description = "Select destination folder for cropped images"
    
    if ($dlg.ShowDialog() -eq "OK") {
        $targetBox.Text = $dlg.SelectedPath
        $targetBox.ForeColor = [System.Drawing.Color]::Black
        $file = Get-ChildItem $dlg.SelectedPath -File | Select-Object -First 1
        $script:currentFileName = if ($file) { $file.Name } else { "filename.jpg" }
        Update-FilenamePreview
        Load-PreviewImage $dlg.SelectedPath
        $script:lastLoadedSource = $dlg.SelectedPath
        Validate-PathBox $targetBox
    }
}
#endregion

#region Validation Timer (unchanged except remove Activate/BringToFront)
# -------------------------------------------------------------------------------------------------
$validationTimer = New-Object System.Windows.Forms.Timer
$validationTimer.Interval = 400
$validationTimer.Add_Tick({
    try {
        $validationTimer.Stop()
        Validate-PathBox $srcBox
        Validate-PathBox $dstBox
        $path = Get-TextboxValue $srcBox
        $sourceChanged = ($path -ne $script:lastLoadedSource)
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            if (Test-Path $path -PathType Leaf) {
                $ext = [System.IO.Path]::GetExtension($path).ToLower()
                if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                    if ($ext -match '\.jpe?g$' -and -not (Test-JpegValid $path)) {
                        Log "Skipping corrupted JPEG file: $(Split-Path $path -Leaf)" $true
                        if ($previewBox) { $previewBox.Image = $null }
                        $script:previewImage = $null
                        Update-PreviewBoxAppearance
                        [System.Windows.Forms.MessageBox]::Show("The selected JPEG file appears to be corrupted.", "Invalid Image", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
                    } else {
                        if ($sourceChanged) {
                            Load-PreviewImage $path
                            $script:currentFileName = [System.IO.Path]::GetFileName($path)
                            $script:lastLoadedSource = $path
                            Update-FilenamePreview
                        }
                    }
                } else {
                    Log "Selected file is not an image: $ext" $true
                    if ($previewBox) { $previewBox.Image = $null }
                    $script:previewImage = $null
                    Update-PreviewBoxAppearance
                }
            } elseif (Test-Path $path -PathType Container) {
                if ($sourceChanged) {
                    $file = $null
                    foreach ($f in Get-ChildItem $path -File) {
                        $ext = $f.Extension.ToLower()
                        if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                            $isValid = if ($ext -match '\.jpe?g$') { Test-JpegValid $f.FullName } else { $true }
                            if ($isValid) {
                                try {
                                    $testImg = [System.Drawing.Image]::FromFile($f.FullName)
                                    $testImg.Dispose()
                                    $file = $f
                                    break
                                } catch {
                                    Log "Skipping corrupted image: $($f.Name)" $true
                                    continue
                                }
                            } else {
                                Log "Skipping corrupted JPEG: $($f.Name)" $true
                            }
                        }
                    }
                    if ($file) {
                        Load-PreviewImage $file.FullName
                        $script:currentFileName = $file.Name
                        $script:lastLoadedSource = $path
                        Update-FilenamePreview
                    } else {
                        Log "No valid images found in folder for preview" $true
                        if ($previewBox) { $previewBox.Image = $null }
                        $script:previewImage = $null
                        Update-PreviewBoxAppearance
                        Update-RunState
                    }
                }
            }
        } else {
            if ($script:previewImage) {
                $script:previewImage.Dispose()
                $script:previewImage = $null
            }
            if ($previewBox) { $previewBox.Image = $null }
            $script:realCropRect = $null
            $script:lastLoadedSource = ""
            Update-PreviewBoxAppearance
        }
        
        if ($script:dstPendingSuggestion -and -not $script:dstSuggestionShown) {
            $script:dstSuggestionShown = $true
            $script:dstPendingSuggestion = $false
            $result = [System.Windows.Forms.MessageBox]::Show($form, "You entered a file path.`nDo you want to use its parent folder instead?", "File detected", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
            # REMOVED: $form.Activate() and $form.BringToFront() – they steal focus
            if ($result -eq "Yes") {
                $folder = Split-Path $script:dstPendingPath
                $dstBox.Text = $folder
                $dstBox.ForeColor = [System.Drawing.Color]::Black
                Validate-PathBox $dstBox
            }
        }
    } catch {
        if ($_.Exception.Message -notmatch "Invalid SOS parameters") {
            Log "Validation error: $($_.Exception.Message)" $true
        }
    }
})
#endregion

#region Form Controls - Source & Output
# -------------------------------------------------------------------------------------------------
# Removed the intrusive Add_Activated event that was forcing focus to main form

# Source section
$null = Add-Label "Source folder or file:" 85 10
$srcUI = Add-TextboxWithIcon 85 30 505
$srcBox = $srcUI.TextBox
$srcValidIcon = $srcUI.Icon
Set-InputColor $srcBox ([System.Drawing.Color]::LightYellow)
$srcBox.TabStop = $true
Set-Placeholder $srcBox "Write/paste the path or Drop a folder/file here..."

$srcError = New-Object Windows.Forms.Label
$srcError.ForeColor = [System.Drawing.Color]::Red
$srcError.AutoSize = $true
$srcError.TextAlign = "TopRight"
$form.Controls.Add($srcError)

$srcBox.Add_TextChanged({
    $validationTimer.Stop()
    $validationTimer.Start()
    $path = $srcBox.Text
    
    if ($path -eq "Write/paste the path or Drop a folder/file here...") {
        $path = ""
    }
    
    $destText = $dstBox.Text
    $isDestEmpty = ([string]::IsNullOrWhiteSpace($destText) -or 
                    $destText -eq "Write/paste the path or Drop the destination folder...")
    
    if ($isDestEmpty -and -not [string]::IsNullOrWhiteSpace($path)) {
        if (Test-Path $path) {
            if (Test-Path $path -PathType Container) {
                $sourceFolder = $path
            } else {
                $sourceFolder = Split-Path $path
            }
            
            if (Test-Path $sourceFolder) {
                $dstBox.Text = $sourceFolder
                $dstBox.ForeColor = [System.Drawing.Color]::Black
                Validate-PathBox $dstBox
            }
        }
    }
    
    if ([string]::IsNullOrWhiteSpace($path)) {
        if ($script:previewImage) {
            $script:previewImage.Dispose()
            $script:previewImage = $null
        }
        if ($previewBox) { $previewBox.Image = $null }
        $script:realCropRect = $null
        $script:lastLoadedSource = ""
        Update-PreviewBoxAppearance
    }
    Update-FilenamePreview
    if ($previewBase) { $previewBase.Refresh() }
    if ($previewInsert) { $previewInsert.Refresh() }
    if ($previewExt) { $previewExt.Refresh() }
})

$srcBox.Add_Leave({
    $text = $this.Text
    if ($text -eq "Write/paste the path or Drop a folder/file here...") {
        $text = ""
    }
    
    if ($this.ForeColor -ne [System.Drawing.Color]::Gray -and $text) {
        $this.Text = Normalize-Path $text
    }
    Validate-PathBox $this
    
    $destText = $dstBox.Text
    $isDestEmpty = ([string]::IsNullOrWhiteSpace($destText) -or 
                    $destText -eq "Write/paste the path or Drop the destination folder...")
    
    if ($isDestEmpty -and -not [string]::IsNullOrWhiteSpace($text)) {
        if (Test-Path $text) {
            if (Test-Path $text -PathType Container) {
                $sourceFolder = $text
            } else {
                $sourceFolder = Split-Path $text
            }
            
            if (Test-Path $sourceFolder) {
                $dstBox.Text = $sourceFolder
                $dstBox.ForeColor = [System.Drawing.Color]::Black
                Validate-PathBox $dstBox
            }
        }
    }
    
    if ([string]::IsNullOrWhiteSpace($text)) {
        if ($script:previewImage) {
            $script:previewImage.Dispose()
            $script:previewImage = $null
        }
        if ($previewBox) { $previewBox.Image = $null }
        $script:realCropRect = $null
        Update-PreviewBoxAppearance
    }
})

$srcBtn = New-Object Windows.Forms.Button
$srcBtn.Text = "Browse Source"
$srcBtn.Location = "10,10"
$srcBtn.Size = "70,45"
$srcBtn.Add_Click({ BrowseSourceModern $srcBox })
$form.Controls.Add($srcBtn)

# Output section
$null = Add-Label "Output folder (if not the same as source):" 85 70
$dstUI = Add-TextboxWithIcon 85 90 505
$dstBox = $dstUI.TextBox
$dstValidIcon = $dstUI.Icon
Set-InputColor $dstBox ([System.Drawing.Color]::LightYellow)
$dstBox.TabStop = $true
Set-Placeholder $dstBox "Write/paste the path or Drop the destination folder..."

$dstError = New-Object Windows.Forms.Label
$dstError.ForeColor = [System.Drawing.Color]::Red
$dstError.AutoSize = $true
$dstError.TextAlign = "TopRight"
$form.Controls.Add($dstError)

$dstBox.Add_Leave({
    $text = Get-TextboxValue $this
    if ($this.ForeColor -ne [System.Drawing.Color]::Gray -and $text) {
        $this.Text = Normalize-Path $text
    }
    
    if ([string]::IsNullOrWhiteSpace($this.Text)) {
        $this.Text = $this.Tag
        $this.ForeColor = [System.Drawing.Color]::Gray
    }
    
    Validate-PathBox $this
})

$dstBox.Add_TextChanged({
    $script:dstSuggestionShown = $false
    $script:dstPendingSuggestion = $false
    $validationTimer.Stop()
    $validationTimer.Start()
})

$dstBtn = New-Object Windows.Forms.Button
$dstBtn.Text = "Browse Output"
$dstBtn.Location = "10,70"
$dstBtn.Size = "70,45"
$dstBtn.Add_Click({ BrowseFolder $dstBox })
$form.Controls.Add($dstBtn)
#endregion

#region Form Controls - Size Options
# -------------------------------------------------------------------------------------------------
$dimensionsGroup = New-Object Windows.Forms.GroupBox
$dimensionsGroup.Text = "Dimensions"
$dimensionsGroup.Location = "110,125"
$dimensionsGroup.Size = "160,90"
$form.Controls.Add($dimensionsGroup)

$sourceLabel = New-Object Windows.Forms.Label
$sourceLabel.Text = "Source"
$sourceLabel.Location = "127,140"
$sourceLabel.AutoSize = $true
$form.Controls.Add($sourceLabel)

$destinationLabel = New-Object Windows.Forms.Label
$destinationLabel.Text = "Output"
$destinationLabel.Location = "202,140"
$destinationLabel.AutoSize = $true
$form.Controls.Add($destinationLabel)

$widthSourceBox = Add-Textbox 120 160 55
$widthSourceBox.TextAlign = "Center"
$widthSourceBox.ReadOnly = $true
$widthSourceBox.BackColor = [System.Drawing.Color]::LightGray
$widthSourceBox.BringtoFront()

$heightSourceBox = Add-Textbox 120 190 55
$heightSourceBox.TextAlign = "Center"
$heightSourceBox.ReadOnly = $true
$heightSourceBox.BackColor = [System.Drawing.Color]::LightGray
$heightSourceBox.BringtoFront()

$widthIcon = New-Object Windows.Forms.Label
$widthIcon.Text = "↔"
$widthIcon.Location = "176,151"
$widthIcon.AutoSize = $true
$widthIcon.Font = New-Object System.Drawing.Font("Segoe UI", 18)
$form.Controls.Add($widthIcon)

$widthBox = Add-Textbox 205 160 55
$widthBox.Text = "1024 px"
Allow-OnlyNumbers $widthBox
$widthBox.TextAlign = "Center"
$widthBox.BringtoFront()
$widthBox.Add_Enter({ Remove-UnitFromBox $this (Get-UnitSuffix) })
$widthBox.Add_Leave({ Add-UnitToBox $this (Get-UnitSuffix) })

$heightIcon = New-Object Windows.Forms.Label
$heightIcon.Text = "↕"
$heightIcon.Location = "182,183"
$heightIcon.AutoSize = $true
$heightIcon.Font = New-Object System.Drawing.Font("Segoe UI", 13)
$form.Controls.Add($heightIcon)

$heightBox = Add-Textbox 205 187 55
$heightBox.Text = "768 px"
Allow-OnlyNumbers $heightBox
$heightBox.TextAlign = "Center"
$heightBox.BringtoFront()
$heightBox.Add_Enter({ Remove-UnitFromBox $this (Get-UnitSuffix) })
$heightBox.Add_Leave({ Add-UnitToBox $this (Get-UnitSuffix) })

$widthBox.Add_TextChanged({ if (-not $script:isUpdatingBoxes) { Update-PreviewCrop } })
$heightBox.Add_TextChanged({ if (-not $script:isUpdatingBoxes) { Update-PreviewCrop } })
#endregion

#region Form Controls - Units
# -------------------------------------------------------------------------------------------------
$unitGroup = New-Object Windows.Forms.GroupBox
$unitGroup.Text = "Units"
$unitGroup.Location = "10,125"
$unitGroup.Size = "95,90"
$form.Controls.Add($unitGroup)

$unitPixels = New-Object Windows.Forms.RadioButton
$unitPixels.Text = "Pixels"
$unitPixels.Location = "10,30"
$unitPixels.Checked = $true
$unitPixels.AutoSize = $true
$unitGroup.Controls.Add($unitPixels)

$unitMM = New-Object Windows.Forms.RadioButton
$unitMM.Text = "Millimeters"
$unitMM.Location = "10,60"
$unitMM.AutoSize = $true
$unitGroup.Controls.Add($unitMM)

$unitPixels.Add_CheckedChanged({
    if ($unitPixels.Checked) {
        Update-UnitsDisplay
        Update-SourceSizeBoxes
    }
})
$unitMM.Add_CheckedChanged({
    if ($unitMM.Checked) {
        Update-UnitsDisplay
        Update-SourceSizeBoxes
    }
})
#endregion

#region Form Controls - Naming
# -------------------------------------------------------------------------------------------------
$modeGroup = New-Object Windows.Forms.GroupBox
$modeGroup.Text = "Name mode"
$modeGroup.Location = "275,125"
$modeGroup.Size = "210,90"
$form.Controls.Add($modeGroup)

$modePrefix = New-Object Windows.Forms.RadioButton
$modePrefix.Text = "Prefix"
$modePrefix.Location = "40,20"
$modePrefix.AutoSize = $true
$modeGroup.Controls.Add($modePrefix)

$modeSuffix = New-Object Windows.Forms.RadioButton
$modeSuffix.Text = "Suffix"
$modeSuffix.Location = "120,20"
$modeSuffix.Checked = $true
$modeSuffix.AutoSize = $true
$modeSuffix.BringToFront()
$modeGroup.Controls.Add($modeSuffix)

$nameBox = Add-Textbox 318 168 55
$nameBox.Text = "_cropped"
$nameBox.MaxLength = 8
$nameBox.BringToFront()
$nameBox.TextAlign = "Center"

$modeSuffix.Add_CheckedChanged({
    if ($modeSuffix.Checked) {
        Filename-Format
        Update-FilenamePreview
    }
})
$modePrefix.Add_CheckedChanged({
    if ($modePrefix.Checked) {
        Filename-Format
        Update-FilenamePreview
    }
})

$plusLabel = New-Object Windows.Forms.Label
$plusLabel.Text = "+"
$plusLabel.Location = "369,165"
$plusLabel.AutoSize = $true
$plusLabel.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($plusLabel)

$previewBase = New-Object Windows.Forms.Label
$previewBase.AutoSize = $true
$previewBase.Location = "313,195"
$previewBase.TextAlign = "MiddleCenter"
$form.Controls.Add($previewBase)

$previewInsert = New-Object Windows.Forms.Label
$previewInsert.AutoSize = $true
$previewInsert.ForeColor = [System.Drawing.Color]::DodgerBlue
$previewInsert.Location = "0,0"
$form.Controls.Add($previewInsert)

$previewExt = New-Object Windows.Forms.Label
$previewExt.AutoSize = $true
$form.Controls.Add($previewExt)

$nameExample = New-Object Windows.Forms.Label
$nameExample.Text = "FILENAME"
$nameExample.Location = "293,172"
$nameExample.AutoSize = $true
$form.Controls.Add($nameExample)

$previewBase.ForeColor = [System.Drawing.Color]::Gray
$previewExt.ForeColor = [System.Drawing.Color]::Gray

$nameBox.Add_TextChanged({
    $nameBox.BackColor = if ($nameBox.Text -match '[\\/:*?"<>|]') { [System.Drawing.Color]::LightCoral } else { [System.Drawing.Color]::White }
    Update-FilenamePreview
})

$previewLabel = New-Object Windows.Forms.Label
$previewLabel.AutoSize = $true
$previewLabel.Location = "330,190"
$previewLabel.ForeColor = [System.Drawing.Color]::Gray
$previewLabel.TextAlign = "MiddleCenter"
$form.Controls.Add($previewLabel)
$previewLabel.Text = $script:currentFileName
$previewLabel.BringToFront()
#endregion

#region Form Controls - Checkboxes
# -------------------------------------------------------------------------------------------------
$openAfterChk = New-Object Windows.Forms.CheckBox
$openAfterChk.Text = "Open folder`nwhen done"
$openAfterChk.AutoSize = $true
$openAfterChk.Location = "504,135"
$openAfterChk.CheckAlign = "MiddleRight"
$openAfterChk.TextAlign = "MiddleCenter"
$form.Controls.Add($openAfterChk)

$noOverwriteChk = New-Object Windows.Forms.CheckBox
$noOverwriteChk.Text = "Don't overwrite"
$noOverwriteChk.AutoSize = $true
$noOverwriteChk.Location = "488,170"
$noOverwriteChk.CheckAlign = "MiddleRight"
$noOverwriteChk.TextAlign = "MiddleCenter"
$form.Controls.Add($noOverwriteChk)
#endregion

#region Form Controls - Buttons
# -------------------------------------------------------------------------------------------------
$cropBtn = New-Object Windows.Forms.Button
$cropBtn.Text = "CROP IMAGES"
$cropBtn.Location = "430,250"
$cropBtn.Size = "160,40"
$form.Controls.Add($cropBtn)

$cancelBtn = New-Object Windows.Forms.Button
$cancelBtn.Text = "CANCEL"
$cancelBtn.Location = "265,250"
$cancelBtn.Size = "160,40"
$cancelBtn.Enabled = $false
$form.Controls.Add($cancelBtn)

$resetBtn = New-Object Windows.Forms.Button
$resetBtn.Text = "Reset Settings"
$resetBtn.Location = "10,250"
$resetBtn.Size = "80,40"
$form.Controls.Add($resetBtn)

$showLogBtn = New-Object Windows.Forms.Button
$showLogBtn.Text = "Show log"
$showLogBtn.Location = "95,250"
$showLogBtn.Size = "80,40"
$form.Controls.Add($showLogBtn)

$clearLogBtn = New-Object Windows.Forms.Button
$clearLogBtn.Text = "Clear log"
$clearLogBtn.Location = "180,250"
$clearLogBtn.Size = "80,40"

$showLogBtn.Add_Click({
    if ($showLogBtn.Text -eq "Show log") {
        $form.Height = 600
        $showLogBtn.Text = "Hide log"
        $form.Controls.Add($clearLogBtn)
    } else {
        $form.Height = 340
        $showLogBtn.Text = "Show log"
        $form.Controls.Remove($clearLogBtn)
    }
})

$clearLogBtn.Add_Click({
    if ($log) { $log.Clear() }
    
    # Clean up progress labels
    $form.Controls.Remove($progressPercentage)
    $form.Controls.Remove($progressFileInfo)
    $form.Controls.Remove($progressFileName)
    $progressPercentage.Dispose()
    $progressFileInfo.Dispose()
    $progressFileName.Dispose()
    $progress.Value = 0

})
#endregion

#region Form Controls - Progress & Log
# -------------------------------------------------------------------------------------------------
$progress = New-Object Windows.Forms.ProgressBar
$progress.Location = "10,220"
$progress.Size = "290,20"
$form.Controls.Add($progress)

$log = New-Object Windows.Forms.TextBox
$log.Multiline = $true
$log.ScrollBars = "Vertical"
$log.Location = "10,310"
$log.Size = "580,240"
$form.Controls.Add($log)
#endregion

#region Preview Box with Drag & Drop, Zoom, and Pan
# -------------------------------------------------------------------------------------------------
$toolStrip = New-Object Windows.Forms.ToolStrip
$toolStrip.Dock = "Top"
$toolStrip.GripStyle = "Hidden"
$toolStrip.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

$zoomInBtn = New-Object Windows.Forms.ToolStripButton
$zoomInBtn.Text = "🔍+"
$zoomInBtn.ToolTipText = "Zoom In (Ctrl+Plus or Mouse Wheel)"
$zoomInBtn.Add_Click({
    if ($previewBox.SizeMode -eq "Zoom") {
        $previewBox.SizeMode = "Normal"
        $script:currentZoom = 1.2
        if ($script:previewImage) {
            $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
            $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
        }
    } else {
        $script:currentZoom = [math]::Min(5.0, $script:currentZoom * 1.2)
        if ($script:previewImage) {
            $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
            $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
        }
    }
    $previewBox.Invalidate()
})
$toolStrip.Items.Add($zoomInBtn)

$zoomOutBtn = New-Object Windows.Forms.ToolStripButton
$zoomOutBtn.Text = "🔍-"
$zoomOutBtn.ToolTipText = "Zoom Out (Ctrl+Minus or Mouse Wheel)"
$zoomOutBtn.Add_Click({
    if ($previewBox.SizeMode -eq "Zoom") {
        $previewBox.SizeMode = "Normal"
        $script:currentZoom = 0.8
        if ($script:previewImage) {
            $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
            $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
        }
    } else {
        $script:currentZoom = [math]::Max(0.1, $script:currentZoom / 1.2)
        if ($script:previewImage) {
            $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
            $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
        }
    }
    $previewBox.Invalidate()
})
$toolStrip.Items.Add($zoomOutBtn)

$resetZoomBtn = New-Object Windows.Forms.ToolStripButton
$resetZoomBtn.Text = "⟳ Fit"
$resetZoomBtn.ToolTipText = "Fit to Window (Ctrl+0)"
$resetZoomBtn.Add_Click({
    if ($previewBox) {
        $previewBox.SizeMode = "Zoom"
        $previewBox.Width = $previewPanel.ClientSize.Width
        $previewBox.Height = $previewPanel.ClientSize.Height
        $previewBox.Invalidate()
    }
})
$toolStrip.Items.Add($resetZoomBtn)

$actualSizeBtn = New-Object Windows.Forms.ToolStripButton
$actualSizeBtn.Text = "1:1"
$actualSizeBtn.ToolTipText = "Actual Size (100%)"
$actualSizeBtn.Add_Click({
    if ($script:previewImage) {
        $previewBox.SizeMode = "Normal"
        $script:currentZoom = 1.0
        $previewBox.Width = $script:previewImage.Width
        $previewBox.Height = $script:previewImage.Height
        $previewBox.Invalidate()
    }
})
$toolStrip.Items.Add($actualSizeBtn)

# Separator – fixed syntax
$separator = New-Object Windows.Forms.ToolStripSeparator
$toolStrip.Items.Add($separator)

$panModeBtn = New-Object Windows.Forms.ToolStripButton
$panModeBtn.Text = "✋ Pan"
$panModeBtn.ToolTipText = "Toggle Pan Mode (click to pan)"
$panModeBtn.CheckOnClick = $true
$panModeBtn.Add_Click({
    if ($panModeBtn.Checked) {
        $previewBox.Cursor = [System.Windows.Forms.Cursors]::Hand
    } else {
        $previewBox.Cursor = [System.Windows.Forms.Cursors]::Default
    }
})
$toolStrip.Items.Add($panModeBtn)

$previewForm.Controls.Add($toolStrip)

# Mouse wheel zoom
$previewBox.MouseWheel.Add({
    if ($_.Delta -gt 0) {
        if ($previewBox.SizeMode -eq "Zoom") {
            $previewBox.SizeMode = "Normal"
            $script:currentZoom = 1.2
            if ($script:previewImage) {
                $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
                $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
            }
        } else {
            $script:currentZoom = [math]::Min(5.0, $script:currentZoom * 1.1)
            if ($script:previewImage) {
                $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
                $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
            }
        }
    } else {
        if ($previewBox.SizeMode -eq "Zoom") {
            $previewBox.SizeMode = "Normal"
            $script:currentZoom = 0.8
            if ($script:previewImage) {
                $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
                $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
            }
        } else {
            $script:currentZoom = [math]::Max(0.1, $script:currentZoom / 1.1)
            if ($script:previewImage) {
                $previewBox.Width = [math]::Round($script:previewImage.Width * $script:currentZoom)
                $previewBox.Height = [math]::Round($script:previewImage.Height * $script:currentZoom)
            }
        }
    }
    $previewBox.Invalidate()
})

# Panning mouse events (using panel's AutoScroll)
$previewBox.Add_MouseDown({
    if ($panModeBtn.Checked -or $_.Button -eq [System.Windows.Forms.MouseButtons]::Middle) {
        $script:isPanning = $true
        $script:panStartPoint = New-Object System.Drawing.Point($_.X, $_.Y)
        $previewBox.Cursor = [System.Windows.Forms.Cursors]::SizeAll
    }
})

$previewBox.Add_MouseMove({
    if ($script:isPanning) {
        $dx = $_.X - $script:panStartPoint.X
        $dy = $_.Y - $script:panStartPoint.Y
        $previewPanel.AutoScrollPosition = New-Object System.Drawing.Point(-$previewPanel.AutoScrollPosition.X + $dx, -$previewPanel.AutoScrollPosition.Y + $dy)
        $script:panStartPoint = New-Object System.Drawing.Point($_.X, $_.Y)
    }
})

$previewBox.Add_MouseUp({
    $script:isPanning = $false
    if ($panModeBtn.Checked) {
        $previewBox.Cursor = [System.Windows.Forms.Cursors]::Hand
    } else {
        $previewBox.Cursor = [System.Windows.Forms.Cursors]::Default
    }
})

# Keyboard shortcuts for zoom
$previewForm.Add_KeyDown({
    if ($_.Control -and $_.KeyCode -eq "Add") {
        $zoomInBtn.PerformClick()
    } elseif ($_.Control -and $_.KeyCode -eq "Subtract") {
        $zoomOutBtn.PerformClick()
    } elseif ($_.Control -and $_.KeyCode -eq "D0") {
        $resetZoomBtn.PerformClick()
    } elseif ($_.KeyCode -eq "Space") {
        $panModeBtn.Checked = $true
    }
})
$previewForm.KeyPreview = $true

# Handle form resize to fit image
$previewForm.Add_Resize({
    if ($previewBox -and $previewBox.SizeMode -eq "Zoom") {
        $previewBox.Width = $previewPanel.ClientSize.Width
        $previewBox.Height = $previewPanel.ClientSize.Height
        $previewBox.Invalidate()
    }
})

# Drag & Drop events for preview window
$previewBox.Add_DragEnter({
    if ($_.Data.GetDataPresent([Windows.Forms.DataFormats]::FileDrop)) {
        $_.Effect = "Copy"
        $previewBox.BackColor = [System.Drawing.Color]::LightYellow
    }
})

$previewBox.Add_DragLeave({ Update-PreviewBoxAppearance })

$previewBox.Add_DragDrop({
    $files = $_.Data.GetData([Windows.Forms.DataFormats]::FileDrop)
    if ($files -and $files.Count -gt 0) {
        $path = $files[0]
        
        if (Test-Path $path -PathType Container) {
            $firstImage = Get-ChildItem $path -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' } | Select-Object -First 1
            if ($firstImage) {
                $srcBox.Text = $path
                $srcBox.ForeColor = [System.Drawing.Color]::Black
                $script:currentFileName = $firstImage.Name
                Validate-PathBox $srcBox
                $validationTimer.Stop()
                $validationTimer.Start()
            } else {
                [System.Windows.Forms.MessageBox]::Show("The folder contains no valid image files.", "No Images Found", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            }
        } elseif (Test-Path $path -PathType Leaf) {
            $ext = [System.IO.Path]::GetExtension($path).ToLower()
            if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
                if ($ext -match '\.jpe?g$' -and -not (Test-JpegValid $path)) {
                    [System.Windows.Forms.MessageBox]::Show("The selected JPEG file appears to be corrupted.", "Invalid Image", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
                    return
                }
                $srcBox.Text = $path
                $srcBox.ForeColor = [System.Drawing.Color]::Black
                $file = Get-Item $path
                $script:currentFileName = $file.Name
                Validate-PathBox $srcBox
                $validationTimer.Stop()
                $validationTimer.Start()
            } else {
                [System.Windows.Forms.MessageBox]::Show("The dropped file is not an image file.", "Invalid File Type", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            }
        }
        Update-PreviewBoxAppearance
    }
})

# Paint event for crop rectangle
$previewBox.Add_Paint({
    if ($script:previewImage -eq $null -or $previewBox.Image -eq $null) {
        $g = $_.Graphics
        $font1 = New-Object System.Drawing.Font("Segoe UI", 18, [System.Drawing.FontStyle]::Bold)
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::DarkGray)
        
        $lines = @("Drag & Drop", "Image file", "or Folder")
        $y = ($previewBox.Height - ($lines.Count * 35)) / 2
        
        foreach ($line in $lines) {
            $size = $g.MeasureString($line, $font1)
            $x = ($previewBox.Width - $size.Width) / 2
            $g.DrawString($line, $font1, $brush, $x, $y)
            $y += $size.Height + 5
        }
        
        $brush.Dispose()
        $font1.Dispose()
        return
    }
    
    if (-not $script:previewRect) { return }
    
    $g = $_.Graphics
    $r = $script:previewRect
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Red, 2)
    $g.DrawRectangle($pen, $r)
    
    $handleSize = 6
    $half = [int]($handleSize / 2)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $pen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::Black, 1)
    
    $handles = @(
        @{ X = [int]$r.Left; Y = [int]$r.Top },
        @{ X = [int]$r.Right; Y = [int]$r.Top },
        @{ X = [int]$r.Left; Y = [int]$r.Bottom },
        @{ X = [int]$r.Right; Y = [int]$r.Bottom },
        @{ X = [int](($r.Left + $r.Right) / 2); Y = [int]$r.Top },
        @{ X = [int](($r.Left + $r.Right) / 2); Y = [int]$r.Bottom },
        @{ X = [int]$r.Left; Y = [int](($r.Top + $r.Bottom) / 2) },
        @{ X = [int]$r.Right; Y = [int](($r.Top + $r.Bottom) / 2) }
    )
    
    foreach ($h in $handles) {
        $rect = New-Object System.Drawing.Rectangle(($h.X - $half), ($h.Y - $half), $handleSize, $handleSize)
        $g.FillRectangle($brush, $rect)
        $g.DrawRectangle($pen2, $rect)
    }
    
    if ($script:snapActive) {
        $snapPen = New-Object System.Drawing.Pen([System.Drawing.Color]::Yellow, 2)
        $g.DrawRectangle($snapPen, $r)
    }
})

# Mouse events for crop rectangle manipulation
$previewBox.Add_MouseDown({
    if (-not $script:previewRect) { return }
    if ($script:isPanning) { return }
    
    $m = $_.Location
    $rPreview = $script:previewRect
    $margin = 8
    $hitRect = New-Object System.Drawing.Rectangle(($rPreview.Left - $margin), ($rPreview.Top - $margin), ($rPreview.Width + ($margin * 2)), ($rPreview.Height + ($margin * 2)))
    $handleSize = 8
    $left = [math]::Abs($m.X - $rPreview.Left) -lt $handleSize
    $right = [math]::Abs($m.X - $rPreview.Right) -lt $handleSize
    $top = [math]::Abs($m.Y - $rPreview.Top) -lt $handleSize
    $bottom = [math]::Abs($m.Y - $rPreview.Bottom) -lt $handleSize
    $nearEdge = $left -or $right -or $top -or $bottom
    
    $script:resizeMode = ""
    if ($left -and $top) { $script:resizeMode = "TopLeft" }
    elseif ($right -and $top) { $script:resizeMode = "TopRight" }
    elseif ($left -and $bottom) { $script:resizeMode = "BottomLeft" }
    elseif ($right -and $bottom) { $script:resizeMode = "BottomRight" }
    elseif ($left) { $script:resizeMode = "Left" }
    elseif ($right) { $script:resizeMode = "Right" }
    elseif ($top) { $script:resizeMode = "Top" }
    elseif ($bottom) { $script:resizeMode = "Bottom" }
    elseif (-not $nearEdge -and $hitRect.Contains($m)) { $script:isDragging = $true }
    
    $script:startMouse = $m
    $script:startRect = New-Object System.Drawing.Rectangle($script:realCropRect.X, $script:realCropRect.Y, $script:realCropRect.Width, $script:realCropRect.Height)
})

$previewBox.Add_MouseMove({
    if (-not $script:previewRect) { return }
    if ($script:isPanning) { return }
    
    $rPreview = [System.Drawing.Rectangle]$script:previewRect
    $m = $_.Location
    $margin = 8
    $hitRect = New-Object System.Drawing.Rectangle(($rPreview.Left - $margin), ($rPreview.Top - $margin), ($rPreview.Width + ($margin * 2)), ($rPreview.Height + ($margin * 2)))
    
    if (-not $script:previewImage) { return }
    
    $handleSize = 8
    $left = [math]::Abs($m.X - $rPreview.Left) -lt $handleSize
    $right = [math]::Abs($m.X - $rPreview.Right) -lt $handleSize
    $top = [math]::Abs($m.Y - $rPreview.Top) -lt $handleSize
    $bottom = [math]::Abs($m.Y - $rPreview.Bottom) -lt $handleSize
    
    if (($left -and $top) -or ($right -and $bottom)) { $previewBox.Cursor = "SizeNWSE" }
    elseif (($right -and $top) -or ($left -and $bottom)) { $previewBox.Cursor = "SizeNESW" }
    elseif ($left -or $right) { $previewBox.Cursor = "SizeWE" }
    elseif ($top -or $bottom) { $previewBox.Cursor = "SizeNS" }
    elseif ($hitRect.Contains($m)) { $previewBox.Cursor = "SizeAll" }
    else { $previewBox.Cursor = "Default" }
    
    if (-not ($script:isDragging -or $script:resizeMode)) { return }
    
    $dx = ($_.X - $script:startMouse.X) / $script:scaleFactor
    $dy = ($_.Y - $script:startMouse.Y) / $script:scaleFactor
    $r = $script:realCropRect
    $s = $script:startRect
    
    if ($script:isDragging) {
        $newX = $s.X + $dx
        $newY = $s.Y + $dy
        $newX = [math]::Max(0, [math]::Min($newX, $script:previewImage.Width - $r.Width))
        $newY = [math]::Max(0, [math]::Min($newY, $script:previewImage.Height - $r.Height))
        $r.X = [int]$newX
        $r.Y = [int]$newY
    } else {
        switch ($script:resizeMode) {
            "Right" { $r.Width = $s.Width + $dx }
            "Left" { $r.X = $s.X + $dx; $r.Width = $s.Width - $dx }
            "Bottom" { $r.Height = $s.Height + $dy }
            "Top" { $r.Y = $s.Y + $dy; $r.Height = $s.Height - $dy }
            "TopLeft" { $r.X = $s.X + $dx; $r.Y = $s.Y + $dy; $r.Width = $s.Width - $dx; $r.Height = $s.Height - $dy }
            "TopRight" { $r.Y = $s.Y + $dy; $r.Width = $s.Width + $dx; $r.Height = $s.Height - $dy }
            "BottomLeft" { $r.X = $s.X + $dx; $r.Width = $s.Width - $dx; $r.Height = $s.Height + $dy }
            "BottomRight" { $r.Width = $s.Width + $dx; $r.Height = $s.Height + $dy }
        }
    }
    
    $imgW = $script:previewImage.Width
    $imgH = $script:previewImage.Height
    $t = $script:snapThreshold
    $script:snapActive = $false
    
    if ([math]::Abs($r.X - 0) -lt $t) { $r.X = 0; $script:snapActive = $true }
    if ([math]::Abs($r.Y - 0) -lt $t) { $r.Y = 0; $script:snapActive = $true }
    if ([math]::Abs(($r.X + $r.Width) - $imgW) -lt $t) {
        if ($script:isDragging) { $r.X = $imgW - $r.Width } else { $r.Width = $imgW - $r.X }
        $script:snapActive = $true
    }
    if ([math]::Abs(($r.Y + $r.Height) - $imgH) -lt $t) {
        if ($script:isDragging) { $r.Y = $imgH - $r.Height } else { $r.Height = $imgH - $r.Y }
        $script:snapActive = $true
    }
    
    $r.Width = [math]::Max(10, $r.Width)
    $r.Height = [math]::Max(10, $r.Height)
    $r.X = [math]::Max(0, $r.X)
    $r.Y = [math]::Max(0, $r.Y)
    
    if (-not $script:isDragging) {
        if ($r.Right -gt $imgW) { $r.Width = $imgW - $r.X }
        if ($r.Bottom -gt $imgH) { $r.Height = $imgH - $r.Y }
    }
    
    $script:isUpdatingBoxes = $true
    if ($unitMM -and $unitMM.Checked) {
        $dpi = 96
        $w_mm = [math]::Round(($r.Width / $dpi) * 25.4)
        $h_mm = [math]::Round(($r.Height / $dpi) * 25.4)
        $widthBox.Text = "$w_mm mm"
        $heightBox.Text = "$h_mm mm"
    } else {
        $widthBox.Text = "$([math]::Round($r.Width)) px"
        $heightBox.Text = "$([math]::Round($r.Height)) px"
    }
    $script:isUpdatingBoxes = $false
    $script:realCropRect = $r
    Update-PreviewRect
})

$previewBox.Add_MouseUp({
    $script:isDragging = $false
    $script:resizeMode = ""
})
#endregion

#region Source and Output Drag & Drop
# -------------------------------------------------------------------------------------------------
$srcBox.AllowDrop = $true
$srcBox.Add_DragEnter({
    if ($_.Data.GetDataPresent([Windows.Forms.DataFormats]::FileDrop)) {
        $_.Effect = "Copy"
        Set-InputColor $srcBox ([System.Drawing.Color]::LightBlue)
    }
})
$srcBox.Add_DragLeave({ Validate-PathBox $srcBox })

$srcBox.Add_DragDrop({
    $path = $_.Data.GetData([Windows.Forms.DataFormats]::FileDrop)[0]
    if (Test-Path $path -PathType Container) {
        $firstImage = Get-ChildItem $path -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' } | Select-Object -First 1
        if ($firstImage) {
            $srcBox.Text = $path
            $srcBox.ForeColor = [System.Drawing.Color]::Black
            $script:currentFileName = $firstImage.Name
            Update-FilenamePreview
        } else {
            [System.Windows.Forms.MessageBox]::Show("The folder contains no image files.", "No Images Found", 
            [System.Windows.Forms.MessageBoxButtons]::OK, 
            [System.Windows.Forms.MessageBoxIcon]::Warning)
            return
        }
    } else {
        $ext = [System.IO.Path]::GetExtension($path).ToLower()
        if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
            $srcBox.Text = $path
            $srcBox.ForeColor = [System.Drawing.Color]::Black
            $file = Get-Item $path
            $script:currentFileName = $file.Name
            Update-FilenamePreview
        } else {
            [System.Windows.Forms.MessageBox]::Show("The dropped file is not an image file.`nPlease drop an image file or folder containing images.", "Invalid File Type", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            return
        }
    }
    Validate-PathBox $srcBox
    $validationTimer.Stop()
    $validationTimer.Start()
})

$dstBox.AllowDrop = $true
$dstBox.Add_DragEnter({
    if ($_.Data.GetDataPresent([Windows.Forms.DataFormats]::FileDrop)) {
        $_.Effect = "Copy"
        $dstBox.BackColor = [System.Drawing.Color]::LightBlue
    }
})
$dstBox.Add_DragLeave({ Validate-PathBox $dstBox })
$dstBox.Add_DragDrop({
    $path = $_.Data.GetData([Windows.Forms.DataFormats]::FileDrop)[0]
    if (Test-Path $path -PathType Leaf) {
        $result = [System.Windows.Forms.MessageBox]::Show($form, "You entered a file path.`nDo you want to use its parent folder instead?", "File detected", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
        $form.Activate()
        $form.BringToFront()
        [System.Windows.Forms.Application]::DoEvents()
        if ($result -eq "Yes") {
            $folder = Split-Path $path
            $dstBox.Text = $folder
            $dstBox.ForeColor = [System.Drawing.Color]::Black
            Validate-PathBox $dstBox
        }
    } else {
        $dstBox.Text = $path
        $dstBox.ForeColor = [System.Drawing.Color]::Black
        Validate-PathBox $dstBox
    }
})
#endregion

#region Button Event Handlers
# -------------------------------------------------------------------------------------------------
$cancelBtn.Add_Click({
    $script:cancel = $true
    $cancelBtn.Enabled = $false
    Log "Cancel requested"
})

$resetBtn.Add_Click({
    if ($unitPixels) { $unitPixels.Checked = $true }
    if ($modeSuffix) { $modeSuffix.Checked = $true }
    if ($widthBox) { $widthBox.Text = "1024 px" }
    if ($heightBox) { $heightBox.Text = "768 px" }
    $script:realCropRect = $null
    if ($previewBox) { $previewBox.Refresh() }
    Update-PreviewCrop
    if ($nameBox) { $nameBox.Text = "_cropped" }
    if ($progress) { $progress.Value = 0 }
    Log "Fields reset"
    Update-RunState
    $form.ActiveControl = $null
})

$cropBtn.Add_Click({
    $script:cancel = $false
    $cancelBtn.Enabled = $true
    $cropBtn.Enabled = $false
    $croppedCount = 0
    
    # Clean up progress labels
    $form.Controls.Remove($progressPercentage)
    $form.Controls.Remove($progressFileInfo)
    $form.Controls.Remove($progressFileName)
    $progressPercentage.Dispose()
    $progressFileInfo.Dispose()
    $progressFileName.Dispose()
    $progress.Value = 0
    
    $source = Get-TextboxValue $srcBox
    if ([string]::IsNullOrWhiteSpace($source)) {
        [System.Windows.Forms.MessageBox]::Show("Please select a source.", "Missing Source")
        Log "No source selected" $true
        $cropBtn.Enabled = $true
        $cancelBtn.Enabled = $false
        return
    }
    
    if (-not (Test-Path $source)) {
        [System.Windows.Forms.MessageBox]::Show("Invalid source path.", "Error")
        Log "Invalid source path: $source" $true
        $cropBtn.Enabled = $true
        $cancelBtn.Enabled = $false
        return
    }
    
    $dest = if ($dstBox.ForeColor -eq [System.Drawing.Color]::Gray -or [string]::IsNullOrWhiteSpace($dstBox.Text)) {
        if (Test-Path $source -PathType Container) { $source } else { Split-Path $source }
    } else { $dstBox.Text }
    
    $wRaw = $widthBox.Text -replace '[^\d]', ''
    $hRaw = $heightBox.Text -replace '[^\d]', ''
    if (-not $wRaw -or -not $hRaw) {
        [System.Windows.Forms.MessageBox]::Show("Width and Height must contain valid numbers", "Invalid Input")
        Log "Invalid crop size" $true
        $cropBtn.Enabled = $true
        $cancelBtn.Enabled = $false
        return
    }
    
    if ($unitMM -and $unitMM.Checked) {
        $dpi = 96
        $targetW = [math]::Max(1, [int](($wRaw / 25.4) * $dpi))
        $targetH = [math]::Max(1, [int](($hRaw / 25.4) * $dpi))
    } else {
        $targetW = [math]::Max(1, [int]$wRaw)
        $targetH = [math]::Max(1, [int]$hRaw)
    }
    
    $files = @()
    if (Test-Path $source -PathType Container) {
        $files = Get-ChildItem $source -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|bmp|gif)$' }
    } else {
        $ext = [System.IO.Path]::GetExtension($source).ToLower()
        if ($ext -match '\.(jpg|jpeg|png|bmp|gif)$') {
            $files = @(Get-Item $source)
        } else {
            [System.Windows.Forms.MessageBox]::Show("The selected source is not an image file.", "Invalid File Type", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
            $cropBtn.Enabled = $true
            $cancelBtn.Enabled = $false
            return
        }
    }
    
    $total = $files.Count
    $done = 0
    $cleanSuffix = $nameBox.Text -replace '[\\/:*?"<>|]', ''
    
    # Create labels for detailed progress
    $progressPercentage = New-Object Windows.Forms.Label
    $progressPercentage.Location = "310,220"
    $progressPercentage.Size = "10,20"
    $progressPercentage.Text = "0%"
    $progressPercentage.Font = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Bold)
    $form.Controls.Add($progressPercentage)
    
    $progressFileInfo = New-Object Windows.Forms.Label
    $progressFileInfo.Location = "350,220"
    $progressFileInfo.Size = "50,20"
    $progressFileInfo.Text = "File 0 of $total"
    $progressFileInfo.Font = New-Object System.Drawing.Font("Segoe UI", 8)
    $form.Controls.Add($progressFileInfo)
    
    $progressFileName = New-Object Windows.Forms.Label
    $progressFileName.Location = "450,220"
    $progressFileName.Size = "80,20"
    $progressFileName.Text = ""
    $progressFileName.Font = New-Object System.Drawing.Font("Segoe UI", 8)
    $progressFileName.ForeColor = [System.Drawing.Color]::Gray
    $form.Controls.Add($progressFileName)
    
    if (-not (Check-OverwriteWarning)) {
        Log "Crop cancelled by user - overwrite not confirmed"
        $cancelBtn.Enabled = $false
        $cropBtn.Enabled = $true
        $form.Controls.Remove($progressPercentage)
        $form.Controls.Remove($progressFileInfo)
        $form.Controls.Remove($progressFileName)
        $progressPercentage.Dispose()
        $progressFileInfo.Dispose()
        $progressFileName.Dispose()
        return
    }
    
    if (-not (Test-Path $dest)) {
        try {
            New-Item -ItemType Directory -Path $dest -Force | Out-Null
            Log "Created output folder: $dest"
        } catch {
            Log "Cannot create output folder: $dest" $true
            $cropBtn.Enabled = $true
            $cancelBtn.Enabled = $false
            $form.Controls.Remove($progressPercentage)
            $form.Controls.Remove($progressFileInfo)
            $form.Controls.Remove($progressFileName)
            $progressPercentage.Dispose()
            $progressFileInfo.Dispose()
            $progressFileName.Dispose()
            return
        }
    }
    
    $userCropRect = if ($script:realCropRect) {
        New-Object System.Drawing.Rectangle $script:realCropRect.X, $script:realCropRect.Y, $script:realCropRect.Width, $script:realCropRect.Height
    } else {
        Log "No crop rectangle defined, will use centered crop"
        $null
    }
    
    $index = 0
    foreach ($f in $files) {
        $index++
        if ($script:cancel) { Log "Cancelled by user"; break }
        
        # Update progress labels
        $percentComplete = [math]::Round(($index / $total) * 100)
        $progressPercentage.Text = "$percentComplete%"
        $progressFileInfo.Text = "File $index of $total"
        $progressFileName.Text = $f.Name
        $progress.Value = $percentComplete
        [System.Windows.Forms.Application]::DoEvents()
        
        try {
            $img = $null
            try {
                $img = [System.Drawing.Image]::FromFile($f.FullName)
            } catch {
                Log "Direct load failed: $($_.Exception.Message)" $true
                try {
                    $imgTemp = [System.Drawing.Image]::FromFile($f.FullName)
                    $img = New-Object System.Drawing.Bitmap($imgTemp)
                    $imgTemp.Dispose()
                } catch {
                    Log "Cannot load image (corrupt): $($f.Name)" $true
                    $done++
                    continue
                }
            }
            
            # Read orientation
            $orientation = 1
            try {
                foreach ($prop in $img.PropertyItems) {
                    if ($prop.Id -eq 0x0112) {
                        $orientation = [System.BitConverter]::ToUInt16($prop.Value, 0)
                        break
                    }
                }
            } catch { $orientation = 1 }
            
            # Calculate crop coordinates
            $cropX = 0; $cropY = 0; $cropW = 0; $cropH = 0
            
            if ($userCropRect -and $userCropRect.Width -gt 0 -and $userCropRect.Height -gt 0) {
                $origW = $img.Width
                $origH = $img.Height
                switch ($orientation) {
                    1 { $cropX = $userCropRect.X; $cropY = $userCropRect.Y; $cropW = $userCropRect.Width; $cropH = $userCropRect.Height }
                    3 { $cropX = $origW - $userCropRect.X - $userCropRect.Width; $cropY = $origH - $userCropRect.Y - $userCropRect.Height; $cropW = $userCropRect.Width; $cropH = $userCropRect.Height }
                    6 { $cropX = $userCropRect.Y; $cropY = $origH - $userCropRect.X - $userCropRect.Width; $cropW = $userCropRect.Height; $cropH = $userCropRect.Width }
                    8 { $cropX = $origW - $userCropRect.Y - $userCropRect.Height; $cropY = $userCropRect.X; $cropW = $userCropRect.Height; $cropH = $userCropRect.Width }
                    default { $cropX = $userCropRect.X; $cropY = $userCropRect.Y; $cropW = $userCropRect.Width; $cropH = $userCropRect.Height }
                }
                $cropX = [math]::Max(0, $cropX)
                $cropY = [math]::Max(0, $cropY)
                if ($cropX + $cropW -gt $img.Width) { $cropW = $img.Width - $cropX }
                if ($cropY + $cropH -gt $img.Height) { $cropH = $img.Height - $cropY }
                $cropW = [math]::Max(1, $cropW)
                $cropH = [math]::Max(1, $cropH)
            } else {
                $cropW = [math]::Min($targetW, $img.Width)
                $cropH = [math]::Min($targetH, $img.Height)
                $cropX = [math]::Max(0, [math]::Floor(($img.Width - $cropW) / 2))
                $cropY = [math]::Max(0, [math]::Floor(($img.Height - $cropH) / 2))
            }
            
            $cropRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)
            $croppedImg = $img.Clone($cropRect, $img.PixelFormat)
            
            # Apply rotation
            switch ($orientation) {
                2 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX) }
                3 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::Rotate180FlipNone) }
                4 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY) }
                5 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipX) }
                6 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone) }
                7 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipX) }
                8 { $croppedImg.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipNone) }
            }
            
            # Resize if needed
            $finalImg = $croppedImg
            if ($croppedImg.Width -ne $targetW -or $croppedImg.Height -ne $targetH) {
                $finalImg = New-Object System.Drawing.Bitmap($croppedImg, $targetW, $targetH)
                $croppedImg.Dispose()
            }
            
            # Preserve EXIF data
            Copy-AllExifData $img $finalImg
            Set-OrientationToNormal $finalImg
            
            # Generate filename
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            $ext = $f.Extension
            $newName = if ($modeSuffix -and $modeSuffix.Checked) { "$baseName$cleanSuffix$ext" } else { "$cleanSuffix$baseName$ext" }
            
            $outputPath = Join-Path $dest $newName
            $originalPath = $outputPath
            $counter = 1
            
            if ($noOverwriteChk -and $noOverwriteChk.Checked) {
                while (Test-Path $outputPath) {
                    $newName = [System.IO.Path]::GetFileNameWithoutExtension($originalPath) + "_$counter" + $ext
                    $outputPath = Join-Path $dest $newName
                    $counter++
                }
            }
            
            # Save image
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
            Log "Saved: $newName"
            $croppedCount++            
        } 
        catch {
            Log "Error processing $($f.Name): $($_.Exception.Message)" $true
        }
        
        $done++
        [System.Windows.Forms.Application]::DoEvents()
    }
    
    if ($openAfterChk -and $openAfterChk.Checked -and (Test-Path $dest) -and $croppedCount -gt 0) {
        Start-Process $dest
    }
    
    $cancelBtn.Enabled = $false
    $cropBtn.Enabled = $true
    Log "Done. Processed $croppedCount of $total images."
})
#endregion

#region Form Finalization and Launch
# -------------------------------------------------------------------------------------------------
$form.Add_Shown({
    $null = [Win32]::ShowWindow([Win32]::GetConsoleWindow(), 0)
    Update-FilenamePreview
    if ($previewLabel) { $previewLabel.Refresh() }
    $form.ActiveControl = $null
    $form.Select()
    Validate-PathBox $srcBox
    Validate-PathBox $dstBox
    Update-RunState
    Filename-Format
    Update-FilenamePreview
    
    foreach ($ctrl in $form.Controls) {
        if ($ctrl -is [System.Windows.Forms.Label]) { $ctrl.BringToFront() }
        if ($ctrl.Controls.Count -gt 0) {
            foreach ($sub in $ctrl.Controls) {
                if ($sub -is [System.Windows.Forms.Label]) { $sub.BringToFront() }
            }
        }
    }
    
    # Position preview window - account for borders
    $previewForm.Owner = $form
    $previewForm.StartPosition = "Manual"
    
    # Calculate position: form's right edge minus 16px (8px border on main form + 8px border on preview form)
    # This makes the preview form's left border touch the main form's right border
    $borderWidth = 7
    $newX = $form.Location.X + $form.Width - ($borderWidth * 2)
    $newY = $form.Location.Y
    $previewForm.Location = New-Object System.Drawing.Point($newX, $newY)
    $previewForm.Show()
})

# Keep preview snapped when main form moves
$form.Add_LocationChanged({
    if ($previewForm.Visible) {
        $borderWidth = 8
        $newX = $form.Location.X + $form.Width - ($borderWidth * 2)
        $newY = $form.Location.Y
        $previewForm.Location = New-Object System.Drawing.Point($newX, $newY)
    }
})

# Keep preview snapped when main form resizes
$form.Add_Resize({
    if ($previewForm.Visible) {
        $borderWidth = 8
        $newX = $form.Location.X + $form.Width - ($borderWidth * 2)
        $newY = $form.Location.Y
        $previewForm.Location = New-Object System.Drawing.Point($newX, $newY)
    }
})

$form.Add_FormClosing({
    $previewForm.Close()
})

[System.Windows.Forms.Application]::Run($form)
[Environment]::Exit(0)
#endregion