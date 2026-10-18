# Capture NATIVE de la fenêtre Tauri (phase 02) : PrintWindow(PW_RENDERFULLCONTENT) sur le handle de la
# fenêtre principale, contenu WebView2 compris. Option -ClientWidth/-ClientHeight : redimensionne d'abord
# la fenêtre pour obtenir exactement cette zone cliente (pixels physiques, DPI 100 %).
# Usage : capture-window.ps1 -Process mmv-desktop -Out chemin.png [-ClientOnly] [-ClientWidth 1920 -ClientHeight 1080]
# (fenetre principale du processus ; arguments ASCII : PowerShell 5.1 altere les arguments UTF-8)
param(
    [Parameter(Mandatory)] [string] $Process,
    [Parameter(Mandatory)] [string] $Out,
    [int] $ClientWidth = 0,
    [int] $ClientHeight = 0,
    [switch] $ClientOnly,
    # 03-C : redimensionnement seul (résolution d'essai), sans capture.
    [switch] $ResizeOnly
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MmvWin32 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@

$proc = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
if (-not $proc) { throw "Main window not found for process: $Process" }
$hwnd = $proc.MainWindowHandle

if ($ClientWidth -gt 0 -and $ClientHeight -gt 0) {
    [void][MmvWin32]::ShowWindow($hwnd, 9) # SW_RESTORE
    $w = New-Object MmvWin32+RECT; $c = New-Object MmvWin32+RECT
    [void][MmvWin32]::GetWindowRect($hwnd, [ref]$w); [void][MmvWin32]::GetClientRect($hwnd, [ref]$c)
    $extraW = ($w.Right - $w.Left) - $c.Right; $extraH = ($w.Bottom - $w.Top) - $c.Bottom
    [void][MmvWin32]::SetWindowPos($hwnd, [IntPtr]::Zero, 0, 0, $ClientWidth + $extraW, $ClientHeight + $extraH, 0x0014) # NOZORDER|NOACTIVATE
    Start-Sleep -Milliseconds 900
}
if ($ResizeOnly) {
    $c = New-Object MmvWin32+RECT; [void][MmvWin32]::GetClientRect($hwnd, [ref]$c)
    "client $($c.Right)x$($c.Bottom)"
    return
}

$w = New-Object MmvWin32+RECT; [void][MmvWin32]::GetWindowRect($hwnd, [ref]$w)
$width = $w.Right - $w.Left; $height = $w.Bottom - $w.Top
$bmp = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [MmvWin32]::PrintWindow($hwnd, $hdc, 2) # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc); $g.Dispose()
if (-not $ok) { throw "PrintWindow failed" }

if ($ClientOnly) {
    $c = New-Object MmvWin32+RECT; [void][MmvWin32]::GetClientRect($hwnd, [ref]$c)
    $p = New-Object MmvWin32+POINT; [void][MmvWin32]::ClientToScreen($hwnd, [ref]$p)
    $crop = $bmp.Clone((New-Object System.Drawing.Rectangle ($p.X - $w.Left), ($p.Y - $w.Top), $c.Right, $c.Bottom), $bmp.PixelFormat)
    $bmp.Dispose(); $bmp = $crop
}

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"$Out $($bmp.Width)x$($bmp.Height)"
$bmp.Dispose()
