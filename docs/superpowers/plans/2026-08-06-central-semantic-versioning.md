# Central Semantic Versioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace duplicated hard-coded versions with one `Version.props` source, bump the application from `1.1.1` to `1.2.0`, and document the required major/minor/patch decision for every future code commit.

**Architecture:** MSBuild imports the canonical version from repository-root `Version.props`; runtime UI and logs read assembly metadata, while packaging reads the same props file for ZIP and packaged-document versions. A focused PowerShell verification script checks synchronization, build metadata, and package output without introducing Git hooks or CI.

**Tech Stack:** .NET 8 WPF, MSBuild props, C#, PowerShell 5.1+, Git

## Global Constraints

- Canonical product version after implementation is exactly `1.2.0`.
- File and assembly versions are exactly `1.2.0.0`.
- Breaking changes increment major; features increment minor; fixes increment patch; docs-only changes do not bump.
- Code and behavior-changing script commits must include their version decision before push.
- Do not add Git hooks, commit wrappers, GitHub Actions, automatic tags, releases, commits, or pushes.
- Preserve the portable ZIP layout with one root executable and `_internal/rclone.exe`.
- Preserve all transfer behavior and existing user data paths.
- Make one `feat:` code commit for this implementation so the version increments only once to `1.2.0`; later documentation-only updates use `docs:`.

---

### Task 1: Centralize and expose version `1.2.0`

**Files:**
- Create: `Version.props`
- Create: `scripts/version-test.ps1`
- Modify: `src/RcloneTransferManager/RcloneTransferManager.csproj:1-17`
- Modify: `src/RcloneTransferManager/Services/LogService.cs:1-39`
- Modify: `src/RcloneTransferManager/MainWindow.xaml:1-6,241-245`
- Modify: `src/RcloneTransferManager/MainWindow.xaml.cs:20-36`
- Modify: `src/RcloneTransferManager/app.manifest:3`
- Modify: `scripts/package.ps1:6-23`
- Modify: `README.md:1-31`
- Modify: `docs/RcloneTransferManager-README.txt:1,66-70`

**Interfaces:**
- Consumes: Existing `AppInfo.Name`, `AppInfo.Creator`, WPF `MainWindow`, and portable package script.
- Produces: `Version.props` with MSBuild properties; `AppInfo.Version : string`; `scripts/version-test.ps1 -ExpectedVersion <semver> [-BuildExe <path>] [-PackageRoot <path>] [-ZipPath <path>]`.

- [ ] **Step 1: Write the failing synchronization test**

Create `scripts/version-test.ps1` with these assertions and no mutation behavior:

```powershell
param(
    [string]$ExpectedVersion = "1.2.0",
    [string]$BuildExe = "",
    [string]$PackageRoot = "",
    [string]$ZipPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Version.props"
if (-not (Test-Path -LiteralPath $propsPath)) { throw "Version.props is missing." }

[xml]$props = Get-Content -Raw -LiteralPath $propsPath
$version = [string]$props.Project.PropertyGroup.Version
$fileVersion = [string]$props.Project.PropertyGroup.FileVersion
$assemblyVersion = [string]$props.Project.PropertyGroup.AssemblyVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must use major.minor.patch: $version" }
if ($version -ne $ExpectedVersion) { throw "Expected $ExpectedVersion but found $version." }
if ($fileVersion -ne "$ExpectedVersion.0") { throw "Unexpected FileVersion: $fileVersion" }
if ($assemblyVersion -ne "$ExpectedVersion.0") { throw "Unexpected AssemblyVersion: $assemblyVersion" }

$project = Get-Content -Raw -LiteralPath (Join-Path $root "src\RcloneTransferManager\RcloneTransferManager.csproj")
if ($project -notmatch [regex]::Escape('Import Project="..\..\Version.props"')) { throw "The project does not import Version.props." }
if ($project -match '<Version>|<FileVersion>|<AssemblyVersion>') { throw "The project still contains local version properties." }

$activeFiles = @(
    "README.md",
    "docs\RcloneTransferManager-README.txt",
    "scripts\package.ps1",
    "src\RcloneTransferManager\MainWindow.xaml",
    "src\RcloneTransferManager\Services\LogService.cs",
    "src\RcloneTransferManager\app.manifest"
)
foreach ($relativePath in $activeFiles) {
    $content = Get-Content -Raw -LiteralPath (Join-Path $root $relativePath)
    if ($content -match '1\.1\.1') { throw "Stale 1.1.1 found in $relativePath." }
}

if ($BuildExe) {
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $BuildExe).Path)
    if ($metadata.FileVersion -ne "$ExpectedVersion.0") { throw "Built EXE FileVersion mismatch: $($metadata.FileVersion)" }
    if ($metadata.ProductVersion -notlike "$ExpectedVersion*") { throw "Built EXE ProductVersion mismatch: $($metadata.ProductVersion)" }
}

if ($PackageRoot) {
    $packagedReadme = Get-Content -Raw -LiteralPath (Join-Path $PackageRoot "README.txt")
    if ($packagedReadme -notmatch [regex]::Escape("Version: $ExpectedVersion")) { throw "Packaged README version mismatch." }
    $rootExecutables = @(Get-ChildItem -LiteralPath $PackageRoot -File -Filter "*.exe")
    if ($rootExecutables.Count -ne 1 -or $rootExecutables[0].Name -ne "RcloneTransferManager.exe") { throw "Portable root executable layout changed." }
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot "_internal\rclone.exe"))) { throw "Internal rclone backend is missing." }
}

if ($ZipPath) {
    $expectedName = "RcloneTransferManager-v$ExpectedVersion-win-x64.zip"
    if ((Split-Path -Leaf $ZipPath) -ne $expectedName) { throw "ZIP name mismatch: $ZipPath" }
    if (-not (Test-Path -LiteralPath $ZipPath)) { throw "ZIP is missing: $ZipPath" }
}

Write-Host "Version verification passed for $ExpectedVersion."
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
& .\scripts\version-test.ps1 -ExpectedVersion 1.2.0
```

Expected: FAIL with `Version.props is missing.`

- [ ] **Step 3: Add the canonical MSBuild version source**

Create `Version.props`:

```xml
<Project>
  <PropertyGroup>
    <Version>1.2.0</Version>
    <FileVersion>1.2.0.0</FileVersion>
    <AssemblyVersion>1.2.0.0</AssemblyVersion>
  </PropertyGroup>
</Project>
```

At the top of `RcloneTransferManager.csproj`, immediately below `<Project ...>`, add:

```xml
  <Import Project="..\..\Version.props" />
```

Delete the local `<Version>`, `<FileVersion>`, and `<AssemblyVersion>` properties from the project.

- [ ] **Step 4: Make runtime version displays read assembly metadata**

In `LogService.cs`, add `using System.Reflection;` and replace the version constant with:

```csharp
public static string Version { get; } =
    typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion.Split('+')[0]
    ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
    ?? "unknown";
```

In `MainWindow.xaml`, change the root title to `Title="Rclone Transfer Manager"` and change the right footer to:

```xml
<TextBlock x:Name="VersionText"
           HorizontalAlignment="Right" Foreground="{StaticResource MutedBrush}" FontSize="11"/>
```

Immediately after `InitializeComponent();` in `MainWindow`'s constructor, add:

```csharp
Title = $"{AppInfo.Name} v{AppInfo.Version}";
VersionText.Text = $"v{AppInfo.Version}  |  {AppInfo.Creator}";
```

Keep the About dialog and log header unchanged because both already consume `AppInfo.Version`.

- [ ] **Step 5: Remove remaining independent product versions**

Set the technical manifest identity to a stable non-product value:

```xml
<assemblyIdentity version="1.0.0.0" name="Arkiez.RcloneTransferManager" />
```

In `README.md`, remove `Version: 1.1.1`, state that the current version is defined in `Version.props`, and replace the concrete ZIP name with `RcloneTransferManager-v&lt;version&gt;-win-x64.zip`.

In `docs/RcloneTransferManager-README.txt`, replace both product-version values with the exact token `{{VERSION}}`; this token exists only in the source guide and is replaced during packaging.

- [ ] **Step 6: Make packaging read and validate `Version.props`**

At the start of `scripts/package.ps1`, load and validate the canonical version before calculating ZIP paths:

```powershell
$versionProps = Join-Path $root "Version.props"
if (-not (Test-Path -LiteralPath $versionProps)) { throw "Version.props not found: $versionProps" }
[xml]$versionDocument = Get-Content -Raw -LiteralPath $versionProps
$version = [string]$versionDocument.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Version.props must contain a major.minor.patch Version." }

$zip = Join-Path $root "RcloneTransferManager-v$version-win-x64.zip"
$stagingZip = Join-Path $root "dist\RcloneTransferManager-v$version-win-x64.staging.zip"
```

Replace the packaged README copy operation with token substitution:

```powershell
$readmeTemplate = Get-Content -Raw -LiteralPath $readme
if (-not $readmeTemplate.Contains("{{VERSION}}")) { throw "Packaged README template is missing {{VERSION}}." }
$readmeText = $readmeTemplate.Replace("{{VERSION}}", $version)
[IO.File]::WriteAllText(
    (Join-Path $publish "README.txt"),
    $readmeText,
    [Text.UTF8Encoding]::new($false))
```

- [ ] **Step 7: Run source synchronization and Release build tests**

Run:

```powershell
& .\scripts\version-test.ps1 -ExpectedVersion 1.2.0
dotnet build .\src\RcloneTransferManager\RcloneTransferManager.csproj --configuration Release
& .\scripts\version-test.ps1 -ExpectedVersion 1.2.0 -BuildExe .\src\RcloneTransferManager\bin\Release\net8.0-windows\RcloneTransferManager.exe
```

Expected: both version checks pass; build reports `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 8: Build and verify the portable package**

Run:

```powershell
& .\scripts\package.ps1
& .\scripts\version-test.ps1 `
  -ExpectedVersion 1.2.0 `
  -BuildExe .\dist\RcloneTransferManager\RcloneTransferManager.exe `
  -PackageRoot .\dist\RcloneTransferManager `
  -ZipPath .\RcloneTransferManager-v1.2.0-win-x64.zip
& .\scripts\smoke-test.ps1 -AppRoot .\dist\RcloneTransferManager
```

Expected: package is created with the `v1.2.0` filename; version verification and packaged-backend smoke tests pass.

- [ ] **Step 9: Verify visible UI version values**

Run this Windows UI Automation check:

```powershell
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Wait-VersionWindow([int]$ProcessId, [string]$Name) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        $elements = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Descendants,
            $condition)
        foreach ($element in $elements) {
            if ($element.Current.ControlType -eq [Windows.Automation.ControlType]::Window -and
                $element.Current.Name -eq $Name) { return $element }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for $Name."
}

function Find-VersionElement($Root, [string]$Name) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    return $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
}

$process = Start-Process -FilePath (Resolve-Path '.\dist\RcloneTransferManager\RcloneTransferManager.exe').Path -PassThru
try {
    $main = Wait-VersionWindow $process.Id 'Rclone Transfer Manager v1.2.0'
    if ($null -eq (Find-VersionElement $main "v1.2.0  |  Arkie'z K. Khositkhanawut")) {
        throw 'Footer version 1.2.0 was not found.'
    }
    $aboutButton = Find-VersionElement $main 'Open about dialog'
    ([Windows.Automation.InvokePattern]$aboutButton.GetCurrentPattern(
        [Windows.Automation.InvokePattern]::Pattern)).Invoke()
    $about = Wait-VersionWindow $process.Id 'About'
    $aboutText = @($about.FindAll(
        [Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join ' '
    if ($aboutText -notmatch 'Rclone Transfer Manager v1\.2\.0') {
        throw "About version 1.2.0 was not found: $aboutText"
    }
    $ok = Find-VersionElement $about 'OK'
    ([Windows.Automation.InvokePattern]$ok.GetCurrentPattern(
        [Windows.Automation.InvokePattern]::Pattern)).Invoke()
    ([Windows.Automation.WindowPattern]$main.GetCurrentPattern(
        [Windows.Automation.WindowPattern]::Pattern)).Close()
    if (-not $process.WaitForExit(10000)) { throw 'Application did not exit.' }
    Write-Host 'Visible version verification passed.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
```

Expected: `Visible version verification passed.` and no application process remains.

- [ ] **Step 10: Commit the complete code change once**

Run:

```powershell
git add Version.props scripts/version-test.ps1 scripts/package.ps1 README.md docs/RcloneTransferManager-README.txt src/RcloneTransferManager/RcloneTransferManager.csproj src/RcloneTransferManager/Services/LogService.cs src/RcloneTransferManager/MainWindow.xaml src/RcloneTransferManager/MainWindow.xaml.cs src/RcloneTransferManager/app.manifest
git diff --cached --check
git commit -m "feat: centralize application version"
```

Expected: the commit contains version `1.2.0` and all code changes; no second version bump is created.

### Task 2: Record the permanent repository rule and finish verification

**Files:**
- Create: `AGENTS.md`
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-08-06-automatic-semantic-versioning-design.md:4`

**Interfaces:**
- Consumes: `Version.props` and the verified `1.2.0` implementation from Task 1.
- Produces: A concise repository instruction that future agents and contributors follow before code commits.

- [ ] **Step 1: Add the repository version requirement**

Create `AGENTS.md`:

```markdown
# Repository Instructions

## Versioning

Before committing or pushing a code change, update the canonical version in
`Version.props` in the same commit:

- Breaking or incompatible change: increment major and reset minor/patch.
- Backward-compatible feature: increment minor and reset patch.
- Backward-compatible bug fix or small behavior correction: increment patch.
- Documentation-only change: do not change the version.

All visible versions and package names must continue to derive from
`Version.props`. Do not add independent hard-coded product versions.
```

Add the same four-line rule under a `## Versioning` section in `README.md` and include this verification command:

```powershell
.\scripts\version-test.ps1
```

- [ ] **Step 2: Mark the design implemented and run final static checks**

Change the design status to `Status: Implemented and verified`, then run:

```powershell
rg -n "1\.1\.1" Version.props AGENTS.md README.md docs/RcloneTransferManager-README.txt scripts src
git diff --check
git status --short
```

Expected: `rg` returns no active hard-coded `1.1.1`; diff check reports no whitespace errors; only intended documentation files are uncommitted after Task 1.

- [ ] **Step 3: Re-run the final verification suite**

Run:

```powershell
& .\scripts\version-test.ps1 `
  -ExpectedVersion 1.2.0 `
  -BuildExe .\dist\RcloneTransferManager\RcloneTransferManager.exe `
  -PackageRoot .\dist\RcloneTransferManager `
  -ZipPath .\RcloneTransferManager-v1.2.0-win-x64.zip
& .\scripts\smoke-test.ps1
& .\scripts\smoke-test.ps1 -AppRoot .\dist\RcloneTransferManager
```

Expected: all checks report success.

- [ ] **Step 4: Commit documentation without changing the product version**

Run:

```powershell
git add AGENTS.md README.md docs/superpowers/specs/2026-08-06-automatic-semantic-versioning-design.md
git diff --cached --check
git commit -m "docs: require semantic version updates"
git status --short
```

Expected: documentation commit succeeds, `Version.props` remains `1.2.0`, and the working tree is clean.
