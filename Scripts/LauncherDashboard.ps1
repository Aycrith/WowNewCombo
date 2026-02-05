<#
.SYNOPSIS
  Simple WinForms dashboard for OneClickLauncher logs.

.DESCRIPTION
  Shows a persistent, scrollable log window for `launcher-latest.log` and a list
  of crash reports. This window is a separate process so it remains open even if
  the launcher console closes unexpectedly.

.NOTES
  PowerShell 5.1+ on Windows.
#>

#Requires -Version 5.1

param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    [Parameter(Mandatory = $true)]
    [string]$LogsDir,

    [int]$TailLines = 400,
    [int]$RefreshMs = 500
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Get-TailText {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$Tail = 400
    )

    try {
        if (-not (Test-Path -LiteralPath $Path)) { return "" }
        $lines = Get-Content -LiteralPath $Path -Tail $Tail -ErrorAction Stop
        return ($lines -join [Environment]::NewLine)
    } catch {
        return ("Failed to read log: " + $_.Exception.Message)
    }
}

function Get-CrashReports {
    param([Parameter(Mandatory = $true)][string]$Dir)

    try {
        if (-not (Test-Path -LiteralPath $Dir)) { return @() }
        return Get-ChildItem -LiteralPath $Dir -File -Filter "crash-*.json" -ErrorAction Stop |
            Sort-Object LastWriteTime -Descending
    } catch {
        return @()
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "WowClassicGrindBot - Launcher Dashboard"
$form.StartPosition = "CenterScreen"
$form.Width = 1100
$form.Height = 760

$fontMono = New-Object System.Drawing.Font("Consolas", 9)

$panelTop = New-Object System.Windows.Forms.Panel
$panelTop.Dock = "Top"
$panelTop.Height = 44

$btnOpen = New-Object System.Windows.Forms.Button
$btnOpen.Text = "Open logs folder"
$btnOpen.Width = 140
$btnOpen.Height = 28
$btnOpen.Left = 10
$btnOpen.Top = 8
$btnOpen.Add_Click({
    try { Start-Process explorer.exe $LogsDir | Out-Null } catch { }
})

$btnExport = New-Object System.Windows.Forms.Button
$btnExport.Text = "Export diagnostics (.zip)"
$btnExport.Width = 170
$btnExport.Height = 28
$btnExport.Left = 160
$btnExport.Top = 8
$btnExport.Add_Click({
    try {
        $ts = Get-Date -Format "yyyyMMdd-HHmmss"
        $zipPath = Join-Path $LogsDir ("diagnostics-{0}.zip" -f $ts)
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue }
        Compress-Archive -Path (Join-Path $LogsDir "*") -DestinationPath $zipPath -Force
        [System.Windows.Forms.MessageBox]::Show("Exported: $zipPath", "Diagnostics Export") | Out-Null
    } catch {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Export failed") | Out-Null
    }
})

$lblHint = New-Object System.Windows.Forms.Label
$lblHint.Text = "This window stays open even if the launcher closes."
$lblHint.AutoSize = $true
$lblHint.Left = 350
$lblHint.Top = 13

$panelTop.Controls.Add($btnOpen)
$panelTop.Controls.Add($btnExport)
$panelTop.Controls.Add($lblHint)

$split = New-Object System.Windows.Forms.SplitContainer
$split.Dock = "Fill"
$split.Orientation = "Vertical"
$split.SplitterDistance = 780

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Multiline = $true
$txtLog.ReadOnly = $true
$txtLog.ScrollBars = "Vertical"
$txtLog.Dock = "Fill"
$txtLog.Font = $fontMono
$txtLog.WordWrap = $false

$panelRight = New-Object System.Windows.Forms.Panel
$panelRight.Dock = "Fill"

$lblCrashes = New-Object System.Windows.Forms.Label
$lblCrashes.Text = "Crash reports"
$lblCrashes.AutoSize = $true
$lblCrashes.Top = 8
$lblCrashes.Left = 8

$listCrashes = New-Object System.Windows.Forms.ListBox
$listCrashes.Left = 8
$listCrashes.Top = 28
$listCrashes.Width = 280
$listCrashes.Height = 640
$listCrashes.Anchor = "Top,Bottom,Left,Right"
$listCrashes.Font = $fontMono
$listCrashes.Add_DoubleClick({
    try {
        $item = $listCrashes.SelectedItem
        if ($item) { Start-Process notepad.exe $item | Out-Null }
    } catch { }
})

$panelRight.Controls.Add($lblCrashes)
$panelRight.Controls.Add($listCrashes)

$split.Panel1.Controls.Add($txtLog)
$split.Panel2.Controls.Add($panelRight)

$form.Controls.Add($split)
$form.Controls.Add($panelTop)

$lastText = ""

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = [Math]::Max(200, $RefreshMs)
$timer.Add_Tick({
    $text = Get-TailText -Path $LogPath -Tail $TailLines
    if ($text -ne $lastText) {
        $txtLog.Text = $text
        $txtLog.SelectionStart = $txtLog.TextLength
        $txtLog.ScrollToCaret()
        $lastText = $text
    }

    $crashFiles = Get-CrashReports -Dir $LogsDir
    $items = @($crashFiles | ForEach-Object { $_.FullName })

    if ($items.Count -ne $listCrashes.Items.Count) {
        $listCrashes.BeginUpdate()
        $listCrashes.Items.Clear()
        foreach ($i in $items) { [void]$listCrashes.Items.Add($i) }
        $listCrashes.EndUpdate()
    }
})

$form.Add_Shown({
    $txtLog.Text = Get-TailText -Path $LogPath -Tail $TailLines
    $timer.Start()
})

[void]$form.ShowDialog()

