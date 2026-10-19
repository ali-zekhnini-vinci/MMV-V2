# Envoie de vraies frappes clavier Windows a la fenetre principale d'un processus (phase 02, preuve native).
# Usage : send-keys.ps1 -Process mmv-desktop -Keys "^0" [-Repeat 1]
# Syntaxe SendKeys : ^ = Ctrl ; {ADD} = + du pave numerique. Arguments ASCII uniquement.
param(
    [Parameter(Mandatory)] [string] $Process,
    [Parameter(Mandatory)] [string] $Keys,
    [int] $Repeat = 1
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MmvForeground {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@
$proc = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
if (-not $proc) { throw "Main window not found for process: $Process" }
$hwnd = $proc.MainWindowHandle
[void][MmvForeground]::ShowWindow($hwnd, 9)
# 03-C : Windows refuse SetForegroundWindow a un processus qui n'est pas au premier plan (verrou de premier plan) ;
# une frappe Alt seule le leve. Les frappes ne partent que si la fenetre est reellement au premier plan.
for ($try = 0; $try -lt 5 -and [MmvForeground]::GetForegroundWindow() -ne $hwnd; $try++) {
    [MmvForeground]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [MmvForeground]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][MmvForeground]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 200
}
if ([MmvForeground]::GetForegroundWindow() -ne $hwnd) { throw "Window of $Process is not in the foreground: keys not sent." }
Start-Sleep -Milliseconds 300
for ($i = 0; $i -lt $Repeat; $i++) {
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 250
}
