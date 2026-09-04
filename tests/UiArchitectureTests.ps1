param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$projectPath = Join-Path $root "src\SensitivityRandomizer\SensitivityRandomizer.csproj"
$uiPath = Join-Path $root "src\SensitivityRandomizer\MainWindow.Ui.cs"
$logicPath = Join-Path $root "src\SensitivityRandomizer\MainWindow.Logic.cs"
$programPath = Join-Path $root "src\SensitivityRandomizer\Program.cs"
$themePath = Join-Path $root "src\SensitivityRandomizer\ThemePalette.cs"
$controlsPath = Join-Path $root "src\SensitivityRandomizer\WpfControls.cs"

$project = Get-Content $projectPath -Raw
$ui = Get-Content $uiPath -Raw
$logic = Get-Content $logicPath -Raw
$program = Get-Content $programPath -Raw
$theme = Get-Content $themePath -Raw
$controls = Get-Content $controlsPath -Raw

function Assert-Contains([string]$text, [string]$needle, [string]$message) {
    if (-not $text.Contains($needle)) { throw $message }
}

function Assert-NotContains([string]$text, [string]$needle, [string]$message) {
    if ($text.Contains($needle)) { throw $message }
}

Assert-Contains $project '<Reference Include="PresentationFramework" />' "WPF reference is missing"
Assert-Contains $project '<Compile Include="MainWindow.Ui.cs" />' "WPF window is not compiled"
Assert-Contains $project '<Compile Include="WpfControls.cs" />' "WPF controls are not compiled"
Assert-Contains $project '<Compile Include="ThemePalette.cs" />' "Theme palette is not compiled"
Assert-Contains $project '<ApplicationIcon>..\..\assets\SensitivityRandomizer.ico</ApplicationIcon>' "Application icon is missing"
Assert-NotContains $project '<Compile Include="MainForm' "Legacy WinForms UI is still compiled"
Assert-NotContains $project '<Compile Include="MultiplierChart.cs" />' "Legacy WinForms chart is still compiled"
Assert-NotContains $project '<Compile Include="UiLayoutMath.cs" />' "Legacy resize helper is still compiled"
Assert-NotContains $project '<Reference Include="System.Windows.Forms" />' "WinForms is still referenced"

$activeWindow = $ui + "`n" + $logic
foreach ($forbidden in @(
    "System.Windows.Forms",
    "SplitContainer",
    "FlowLayoutPanel",
    "PerformLayout(",
    "ResizeBegin +=",
    "ResizeEnd +=",
    "SizeChanged +="
)) {
    Assert-NotContains $activeWindow $forbidden "Active window contains legacy resize/UI code: $forbidden"
}

Assert-Contains $program "new System.Windows.Application" "Program does not start the WPF dispatcher"
Assert-Contains $ui "TimeSpan.FromMilliseconds(500)" "Unexpected UI refresh cadence"
Assert-Contains $ui "if (state == EngineState.Running || state == EngineState.Paused)" "Idle graph invalidation guard is missing"
Assert-Contains $logic "if (_safetyResetRequired" "Idle ResetGuard file guard is missing"
Assert-Contains $logic "_ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Close));" "Final Close is not deferred beyond the Closing event"
Assert-NotContains $logic "                Close();" "A direct reentrant Close remains in the shutdown path"
Assert-Contains $program "_fatalUiErrorReported = true;" "Runtime UI errors are not marked before propagation"
Assert-Contains $program "if (_fatalUiErrorReported)" "Runtime UI errors can still be reported again as startup failures"
Assert-Contains $theme "AppTheme.Pastel" "Pastel theme is missing"
Assert-Contains $logic "ThemeBoxOnSelectionChanged" "Theme selection is not wired"
Assert-Contains $controls "class LogoMark" "Window header logo is missing"
Assert-Contains $ui 'NavigateUri = new Uri("https://dinati.ru/")' "Clickable author link is missing"
Assert-Contains $ui 'RequestNavigate += AuthorLinkOnRequestNavigate' "Author link is not wired"
$retiredBrand = "C" + "S2"
Assert-NotContains $activeWindow $retiredBrand "Retired game-specific branding remains in the active interface"
Assert-NotContains $activeWindow "BeginAnimation(Width" "A width animation can regress interactive resize"
Assert-NotContains $activeWindow "BeginAnimation(Height" "A height animation can regress interactive resize"

Write-Host "PASS: WPF UI architecture and resize-path checks"
