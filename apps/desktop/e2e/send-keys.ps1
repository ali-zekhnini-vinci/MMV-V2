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
}
"@
$proc = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
if (-not $proc) { throw "Main window not found for process: $Process" }
[void][MmvForeground]::ShowWindow($proc.MainWindowHandle, 9)
[void][MmvForeground]::SetForegroundWindow($proc.MainWindowHandle)
Start-Sleep -Milliseconds 300
for ($i = 0; $i -lt $Repeat; $i++) {
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 250
}
