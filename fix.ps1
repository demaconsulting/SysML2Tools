# fix.ps1
#
# PURPOSE:
#   Applies the available auto-fixers with progress output. A fixer run always exits 0.
#   Run this after making changes to automatically handle formatting
#   so agents and developers do not need to respond to lint output.
#   Handles: dotnet format, LintMark (markdown), yamlfix, YAML line endings.
#
# USAGE:
#   pwsh ./fix.ps1 -Help                      Print the usage text and exit 0
#   pwsh ./fix.ps1 -All                       All fixers
#   pwsh ./fix.ps1 -Changed [-Base <ref>]     Only the fixers for the file types changed since <ref>
#                                             (default origin/main, else main) plus uncommitted changes.
#                                             yamlfix, LintMark, and dotnet format are limited to the changed,
#                                             still-existing .yml/.yaml, .md, and .cs files (minus configured
#                                             ignores) unless their configuration changed (.yamlfix.toml or
#                                             pip-requirements.txt; .lintmark.yaml or .editorconfig; project or
#                                             build files) or more than 200 files changed: then the whole repository
#   pwsh ./fix.ps1 -Check lintmark,format     Only the named fixers, in full; with -Changed, only when their
#                                             inputs changed, and then limited as above
#   Add -DryRun to any scope to print which fixers would run without running them.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific auto-fix operations.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points, update tool versions, and add optional parameters that focus a run.
#   To add a fixer, add it to the $fixers table (its name becomes a valid -Check value and
#   appears in the usage text), then add its input filter and its run step at the
#   [PROJECT-SPECIFIC] points. The reusable scoping logic lives in scripts/ProjectScripts.psm1,
#   which is delivered by the Agents release zip, is shared across repositories, and must never
#   be edited here.
#   With no arguments the script prints the usage text and exits 1; -All must still apply every
#   fixer. A fixer run always exits 0 (usage and argument errors exit 1).

param(
    # Run every fixer.
    [switch]$All,

    # Run only the fixers for the changed file types.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Run only the named fixers (see the usage text for the valid names).
    [string[]]$Check,

    # Print which fixers would run and exit without running them.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# ==============================================================================
# FIXER TABLE
# Every fixer in run order: its -Check name and the reason shown when it is skipped.
# [PROJECT-SPECIFIC] Add repository-specific fixers here.
# ==============================================================================

$fixers = [ordered]@{
    yamlfix  = @{ Skip = 'no YAML files changed' }
    lintmark = @{ Skip = 'no Markdown files changed' }
    format   = @{ Skip = 'no C# or project files changed' }
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'fix.ps1' -IterateWith '-Changed (or -Check)' `
        -Purpose 'apply the auto-fixers (formatting); a fixer run always exits 0.' `
        -Options ([ordered]@{
            '-All'           = 'Every fixer; cannot be combined with another scope.'
            '-Changed'       = 'Fixers for changed file types, limited to the changed files (all files if config changed or >200 files).'
            '-Base <ref>'    = 'Base reference for -Changed (default origin/main, else main).'
            '-Check <names>' = 'Only the named fixers, in full; with -Changed, only those with changed inputs, limited the same way.'
            '-DryRun'        = 'Print which fixers would run or be skipped, and their file scope, then exit 0.'
            '-Help'          = 'Print this text and exit 0.'
        }) `
        -ChecksLabel 'Fixers (-Check names)' -Checks @($fixers.Keys) `
        -Examples @('-Changed', '-Check lintmark', '-Check format -Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Check   = [bool]$Check
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

$wanted = @($fixers.Keys)
if ($Check) {
    $wanted = Resolve-NameList -Names $Check -Valid @($fixers.Keys) -Label 'fixer'
    if ($null -eq $wanted) { exit 1 }
}

# ==============================================================================
# FILE SCOPING (-Changed)
# yamlfix and LintMark fix only the changed, still-existing files, so a change to one file never
# rewrites others. A fixer runs over the whole repository when its configuration changed or when
# more than $maxScopedFiles files changed (a long command line costs more than a full run).
# The same vendored and generated folders are skipped as by Convert-YamlLineEndings.
# ==============================================================================

$maxScopedFiles = 200
$batchSize = 50
$vendoredPattern = '(^|/)(\.git|node_modules|\.venv|thirdparty|third-party|3rd-party|\.agent-logs|generated)/'

# Lists the string items of a top-level key in a YAML file (for example the 'ignore' globs of .lintmark.yaml)
function Get-YamlList {
    param([string]$Path, [string]$Key)
    $items = @()
    $inList = $false
    foreach ($line in Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue) {
        if ($line -match "^${Key}:") { $inList = $true; continue }
        if ($inList -and $line -match '^\s+-\s+["'']?([^"''#\s]+)') { $items += $Matches[1] }
        elseif ($inList -and $line -match '^\S') { $inList = $false }
    }
    return $items
}

# Selects the changed, still-existing files with an extension, minus vendored folders and the given globs.
# Returns the files to pass (possibly none) or $null to run over the whole repository, with the reason.
function Get-ScopedFiles {
    param([string[]]$Files, [string]$Pattern, [string[]]$IgnoreGlobs = @())
    $ignore = @($IgnoreGlobs | ForEach-Object { Convert-GlobToRegex $_ })
    $selected = @($Files | Where-Object {
            $file = $_
            $_ -match $Pattern -and $_ -notmatch $vendoredPattern -and (Test-Path -LiteralPath $_ -PathType Leaf) -and
            -not @($ignore | Where-Object { $file -match $_ }).Count
        })
    if ($selected.Count -gt $maxScopedFiles) {
        return [pscustomobject]@{ Files = $null; Reason = "more than $maxScopedFiles changed files" }
    }
    return [pscustomobject]@{ Files = $selected; Reason = $null }
}

# Lists every YAML file of the repository that is not in a vendored or generated folder (the same exclusions as the
# scoped path and Convert-YamlLineEndings), so a whole-repository run never touches vendored or generated YAML.
# Uses git so ignored folders are not walked; falls back to a recursive search outside a git repository.
function Get-AllYamlFiles {
    $listed = @(git -c core.quotePath=false ls-files --cached --others --exclude-standard -- '*.yaml' '*.yml' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        $listed = @(Get-ChildItem -Recurse -File -Include '*.yaml', '*.yml' |
                ForEach-Object { [System.IO.Path]::GetRelativePath((Get-Location).Path, $_.FullName) })
    }
    return @($listed | ForEach-Object { $_ -replace '\\', '/' } |
            Where-Object { $_ -notmatch $vendoredPattern -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
            Sort-Object -Unique)
}

# Normalizes the given YAML files to LF line endings (UTF-8 without a BOM), as Convert-YamlLineEndings does for all
function Convert-ListedYamlLineEndings {
    param([string[]]$Files)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    foreach ($file in $Files) {
        $path = (Resolve-Path -LiteralPath $file).Path
        $raw = [System.IO.File]::ReadAllText($path)
        $fixed = $raw.Replace("`r`n", "`n")
        if ($raw -ne $fixed) { [System.IO.File]::WriteAllText($path, $fixed, $utf8NoBom) }
    }
}

# ==============================================================================
# FIXER SELECTION
# A fixer runs when it is wanted (-All, or named by -Check) and, with -Changed, when a file
# type it handles changed. When the changed files cannot be determined, every wanted fixer runs.
# ==============================================================================

$inputsChanged = @{}
foreach ($name in $fixers.Keys) { $inputsChanged[$name] = $true }
$formatInclude = @()
$fixFiles = @{ yamlfix = @(); lintmark = @() }   # files to pass to the fixer; empty means the whole repository
$fixReason = @{ yamlfix = ''; lintmark = '' }    # why a -Changed run covers the whole repository

if ($Changed) {
    $files = Get-ChangedFiles -Base $Base
    if ($null -eq $files) {
        Write-Host "Running all selected fixers: the changed files could not be determined."
        $fixReason.yamlfix = $fixReason.lintmark = 'the changed files could not be determined'
    } else {
        Write-Host "Changed files: $($files.Count)"

        # yamlfix: changed YAML files; the whole repository when its configuration or Python packages changed.
        # yamlfix has no exclude setting in .yamlfix.toml, so only the vendored folders are filtered out.
        if (Test-AnyMatch $files '^\.yamlfix\.toml$|^pip-requirements\.txt$') {
            $fixReason.yamlfix = '.yamlfix.toml or pip-requirements.txt changed'
        } elseif (Test-AnyMatch $files '\.ya?ml$') {
            $scope = Get-ScopedFiles $files '\.ya?ml$'
            if ($null -eq $scope.Files) { $fixReason.yamlfix = $scope.Reason }
            elseif ($scope.Files.Count -eq 0) {
                $inputsChanged.yamlfix = $false
                $fixers.yamlfix.Skip = 'no changed YAML files remain after exclusions'
            } else { $fixFiles.yamlfix = $scope.Files }
        } else {
            $inputsChanged.yamlfix = $false
        }

        # LintMark: changed Markdown files; the whole repository when .lintmark.yaml or .editorconfig changed.
        # The 'ignore' (and 'exclude') globs of .lintmark.yaml are applied here so explicit paths cannot bypass them.
        if (Test-AnyMatch $files '(^|/)\.lintmark\.yaml$|(^|/)\.editorconfig$') {
            $fixReason.lintmark = '.lintmark.yaml or .editorconfig changed'
        } elseif (Test-AnyMatch $files '\.md$') {
            $lintMarkIgnore = @(Get-YamlList '.lintmark.yaml' 'ignore') + @(Get-YamlList '.lintmark.yaml' 'exclude')
            $scope = Get-ScopedFiles $files '\.md$' $lintMarkIgnore
            if ($null -eq $scope.Files) { $fixReason.lintmark = $scope.Reason }
            elseif ($scope.Files.Count -eq 0) {
                $inputsChanged.lintmark = $false
                $fixers.lintmark.Skip = 'no changed Markdown files remain after ignores and deletions'
            } else { $fixFiles.lintmark = $scope.Files }
        } else {
            $inputsChanged.lintmark = $false
        }

        $formatScope = Get-DotnetFormatScope $files
        $inputsChanged.format = $formatScope.Run
        $formatInclude = $formatScope.Include

        # [PROJECT-SPECIFIC] Add input filters for repository-specific fixers here.

        # A change to this script or the shared module changes the selection logic itself, so every selected
        # fixer runs over the whole repository (no input filter could say what it affects)
        if (Test-AnyMatch $files '^(fix\.ps1|scripts/ProjectScripts\.psm1)$') {
            Write-Host "Running all selected fixers over the whole repository: fix.ps1 or scripts/ProjectScripts.psm1 changed."
            foreach ($name in $fixers.Keys) { $inputsChanged[$name] = $true }
            $fixFiles = @{ yamlfix = @(); lintmark = @() }
            $fixReason.yamlfix = $fixReason.lintmark = 'fix.ps1 or scripts/ProjectScripts.psm1 changed'
            $formatInclude = @()
        }
    }
}

$run = @{}
$skipReason = @{}
foreach ($name in $fixers.Keys) {
    $run[$name] = ($name -in $wanted) -and $inputsChanged[$name]
    if ($name -notin $wanted) { $skipReason[$name] = 'not selected by -Check' }
    else { $skipReason[$name] = $fixers[$name].Skip }
}
if (-not $run.format) { $formatInclude = @() }

if ($DryRun) {
    foreach ($name in $fixers.Keys) {
        Write-Host ("  {0,-12} {1}" -f $name, ($run[$name] ? 'run' : "skip ($($skipReason[$name]))"))
    }
    foreach ($name in 'yamlfix', 'lintmark') {
        if (-not $run[$name]) { continue }
        if ($fixFiles[$name].Count -gt 0) {
            Write-Host "  $name limited to $($fixFiles[$name].Count) file(s): $($fixFiles[$name] -join ' ')"
        } elseif ($fixReason[$name]) {
            Write-Host "  $name over the whole repository ($($fixReason[$name]))"
        } else {
            Write-Host "  $name over the whole repository"
        }
    }
    if ($formatInclude.Count -gt 0) { Write-Host "  dotnet format limited to: $($formatInclude -join ' ')" }
    Write-Host "Dry run: no fixer was run."
    exit 0
}

# ==============================================================================
# AUTO-FIX
# Applies the selected auto-fixers with progress output. Never fails - applies what it can and
# exits 0 so agents do not react to any output as a problem to solve.
# ==============================================================================

# --- YAML Auto-Fix ---
if ($run.yamlfix) {
    $yamlFiles = $fixFiles.yamlfix
    $wholeRepo = $yamlFiles.Count -eq 0
    if ($wholeRepo) { $yamlFiles = Get-AllYamlFiles }
    if (-not $wholeRepo) { Write-Host "Fixing: YAML ($($yamlFiles.Count) changed files)..." }
    else { Write-Host "Fixing: YAML..." }
    if (Initialize-PythonVenv -Silent) {
        # Always pass explicit files (never 'yamlfix .'), so vendored and generated YAML is never rewritten
        for ($i = 0; $i -lt $yamlFiles.Count; $i += $batchSize) {
            $batch = @($yamlFiles[$i..([Math]::Min($i + $batchSize, $yamlFiles.Count) - 1)])
            yamlfix @batch 2>$null
        }
        deactivate 2>$null
    }
    if (-not $wholeRepo) { Convert-ListedYamlLineEndings $yamlFiles } else { Convert-YamlLineEndings }
} else {
    Write-Host "Fixing: YAML... skipped ($($skipReason.yamlfix))"
}

# --- Markdown Auto-Fix ---
if ($run.lintmark) {
    $markdownFiles = $fixFiles.lintmark
    if ($markdownFiles.Count -gt 0) { Write-Host "Fixing: markdown ($($markdownFiles.Count) changed files)..." }
    else { Write-Host "Fixing: markdown..." }
    dotnet tool restore > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        if ($markdownFiles.Count -gt 0) {
            for ($i = 0; $i -lt $markdownFiles.Count; $i += $batchSize) {
                $batch = @($markdownFiles[$i..([Math]::Min($i + $batchSize, $markdownFiles.Count) - 1)])
                dotnet lintmark --fix @batch 2>$null
            }
        } else {
            dotnet lintmark --fix 2>$null
        }
    }
} else {
    Write-Host "Fixing: markdown... skipped ($($skipReason.lintmark))"
}

# [PROJECT-SPECIFIC] Add additional auto-fixers here.

# --- .NET Auto-Format ---
if ($run.format) {
    if ($formatInclude.Count -gt 0) {
        Write-Host "Fixing: dotnet format ($($formatInclude.Count) changed C# files)..."
    } else {
        Write-Host "Fixing: dotnet format..."
    }
    $slnFiles = @(Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue) +
                @(Get-ChildItem -Filter "*.slnx" -ErrorAction SilentlyContinue)
    if ($slnFiles.Count -gt 0) {
        $formatArgs = @()
        if ($formatInclude.Count -gt 0) { $formatArgs = @('--include') + $formatInclude }
        dotnet format @formatArgs 2>$null
    }
} else {
    Write-Host "Fixing: dotnet format... skipped ($($skipReason.format))"
}

# [PROJECT-SPECIFIC] Add additional language-specific auto-formatters here.
# Example (C/C++ with clang-format):
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" |
#       ForEach-Object { clang-format -i $_.FullName } 2>$null

Write-Host "Auto-fix complete."
exit 0
