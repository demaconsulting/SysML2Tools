# fix.ps1
#
# PURPOSE:
#   Applies all available auto-fixers with progress output. Always exits 0.
#   Run this after making changes to automatically handle formatting
#   so agents and developers do not need to respond to lint output.
#   Handles: dotnet format, markdownlint, yamlfix, YAML line endings.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific auto-fix operations.
#
# MODIFICATION POLICY:
#   Only modify this file to add project-specific operations at the designated
#   [PROJECT-SPECIFIC] extension points, or to update tool versions as needed.

# ==============================================================================
# HELPER FUNCTIONS
# ==============================================================================

function Get-VenvActivateScript {
    if (Test-Path ".venv/Scripts/Activate.ps1") { return ".venv/Scripts/Activate.ps1" }  # Windows
    if (Test-Path ".venv/bin/Activate.ps1") { return ".venv/bin/Activate.ps1" }          # Linux/macOS
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
        Where-Object { $_.FullName -notmatch '[/\\](\.git|node_modules|\.venv|thirdparty|third-party|3rd-party|\.agent-logs)[/\\]' } |
        ForEach-Object {
            $raw = [System.IO.File]::ReadAllText($_.FullName)
            $fixed = $raw.Replace("`r`n", "`n")
            if ($raw -ne $fixed) {
                [System.IO.File]::WriteAllText($_.FullName, $fixed, $utf8NoBom)
            }
        }
}

# ==============================================================================
# AUTO-FIX
# Applies all auto-fixers with progress output. Never fails — applies what it can and
# exits 0 so agents do not react to any output as a problem to solve.
# ==============================================================================

# --- YAML Auto-Fix ---
Write-Host "Fixing: YAML..."
if (Initialize-PythonVenv -Silent) {
    yamlfix . 2>$null
    deactivate 2>$null
}
Normalize-YamlLineEndings

# --- Markdown Auto-Fix ---
Write-Host "Fixing: markdown..."
$env:PUPPETEER_SKIP_DOWNLOAD = "true"
npm install --silent 2>$null
if ($LASTEXITCODE -eq 0) {
    npx markdownlint-cli2 --fix "**/*.md" 2>$null
}

# [PROJECT-SPECIFIC] Add additional auto-fixers here.
# Example (Prettier for TypeScript/JSON):
#   npx prettier --write "src/**/*.{ts,json}" 2>$null

# --- .NET Auto-Format ---
Write-Host "Fixing: dotnet format..."
$slnFiles = @(Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue) +
            @(Get-ChildItem -Filter "*.slnx" -ErrorAction SilentlyContinue)
if ($slnFiles.Count -gt 0) {
    dotnet format 2>$null
}

# [PROJECT-SPECIFIC] Add additional language-specific auto-formatters here.
# Example (C/C++ with clang-format):
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" |
#       ForEach-Object { clang-format -i $_.FullName } 2>$null

Write-Host "Auto-fix complete."
exit 0
