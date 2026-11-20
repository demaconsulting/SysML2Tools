# fix.ps1
#
# PURPOSE:
#   Applies all available auto-fixers with progress output. Always exits 0.
#   Run this after making changes to automatically handle formatting
#   so agents and developers do not need to respond to lint output.
#   Handles: dotnet format, LintMark (markdown), yamlfix, YAML line endings.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points, update tool versions, and add optional parameters that focus
#   a run (for example -Project <name> in a multi-library repository).
#   Run with no arguments it must still apply every fixer, and it always exits 0.

function Get-VenvActivateScript {
    if (Test-Path ".venv/Scripts/Activate.ps1") { return ".venv/Scripts/Activate.ps1" }
    if (Test-Path ".venv/bin/Activate.ps1") { return ".venv/bin/Activate.ps1" }
    return $null
}

function Initialize-PythonVenv {
    param([switch]$Silent)

    if (-not (Test-Path ".venv")) {
        if ($Silent) { python -m venv .venv 2>$null } else { python -m venv .venv }
        if ($LASTEXITCODE -ne 0) { return $false }
    }

    $activateScript = Get-VenvActivateScript
    if (-not $activateScript) { return $false }
    if ($Silent) { & $activateScript 2>$null } else { & $activateScript }
    if (-not (Get-Command deactivate -ErrorAction SilentlyContinue)) { return $false }

    $installSucceeded = $false
    try {
        if ($Silent) {
            pip install -r pip-requirements.txt --quiet --disable-pip-version-check 2>$null
        } else {
            pip install -r pip-requirements.txt --quiet --disable-pip-version-check
        }
        $installSucceeded = $LASTEXITCODE -eq 0
        return $installSucceeded
    }
    finally {
        if (-not $installSucceeded -and (Get-Command deactivate -ErrorAction SilentlyContinue)) {
            deactivate 2>$null
        }
    }
}

function Normalize-YamlLineEndings {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    Get-ChildItem -Recurse -Include "*.yaml", "*.yml" |
        Where-Object { $_.FullName -notmatch '[/\\](\.git|node_modules|\.venv|thirdparty|third-party|3rd-party|\.agent-logs|generated)[/\\]' } |
        ForEach-Object {
            $raw = [System.IO.File]::ReadAllText($_.FullName)
            $fixed = $raw.Replace("`r`n", "`n")
            if ($raw -ne $fixed) {
                [System.IO.File]::WriteAllText($_.FullName, $fixed, $utf8NoBom)
            }
        }
}

# --- YAML Auto-Fix ---
Write-Host "Fixing: YAML..."
if (Initialize-PythonVenv -Silent) {
    yamlfix . 2>$null
    deactivate 2>$null
}
Normalize-YamlLineEndings

# --- Markdown Auto-Fix ---
Write-Host "Fixing: markdown..."
dotnet tool restore > $null 2>&1
if ($LASTEXITCODE -eq 0) {
    dotnet lintmark --fix 2>$null
}

# [PROJECT-SPECIFIC] Add additional auto-fixers here.
# C/C++ example:
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" |
#       ForEach-Object { clang-format -i $_.FullName } 2>$null

# --- .NET Auto-Format ---
Write-Host "Fixing: dotnet format..."
$slnFiles = @(Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue) +
            @(Get-ChildItem -Filter "*.slnx" -ErrorAction SilentlyContinue)
if ($slnFiles.Count -gt 0) {
    dotnet restore > $null 2>&1
    dotnet format 2>$null
}

Write-Host "Auto-fix complete."
exit 0
