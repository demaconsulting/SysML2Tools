# lint.ps1
#
# PURPOSE:
#   Runs all lint checks and reports failures. Exits 1 on error.
#   Used by CI/CD as the merge gate and by the lint-fix agent
#   during pre-PR cleanup.
#
#   To auto-fix formatting issues, run fix.ps1 instead.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points, update tool versions, and add optional parameters that focus
#   a run (for example -Project <name> in a multi-library repository).
#   Run with no arguments it must still run every check (CI relies on that), and never
#   remove or weaken a check to make it pass.

function Get-VenvActivateScript {
    if (Test-Path ".venv/Scripts/Activate.ps1") { return ".venv/Scripts/Activate.ps1" }
    if (Test-Path ".venv/bin/Activate.ps1") { return ".venv/bin/Activate.ps1" }
    return $null
}

function Initialize-PythonVenv {
    if (-not (Test-Path ".venv")) {
        python -m venv .venv
        if ($LASTEXITCODE -ne 0) { return $false }
    }

    $activateScript = Get-VenvActivateScript
    if (-not $activateScript) { return $false }
    & $activateScript
    if (-not (Get-Command deactivate -ErrorAction SilentlyContinue)) { return $false }

    $installSucceeded = $false
    try {
        pip install -r pip-requirements.txt --quiet --disable-pip-version-check
        $installSucceeded = $LASTEXITCODE -eq 0
        return $installSucceeded
    }
    finally {
        if (-not $installSucceeded -and (Get-Command deactivate -ErrorAction SilentlyContinue)) {
            deactivate 2>$null
        }
    }
}

$lintError = $false

# --- YAML ---
Write-Host "Linting: YAML..."
$skipPython = -not (Initialize-PythonVenv)
if ($skipPython) { $lintError = $true }

if (-not $skipPython) {
    yamllint .
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
    deactivate
}

# --- dotnet Tools ---
$skipDotnetTools = $false
dotnet tool restore > $null
if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetTools = $true }

# --- Spelling ---
Write-Host "Linting: spelling..."
if (-not $skipDotnetTools) {
    dotnet textlint
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
}

# --- Markdown ---
Write-Host "Linting: markdown..."
if (-not $skipDotnetTools) {
    dotnet lintmark
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
}

# [PROJECT-SPECIFIC] Add additional lint checks here.

# --- Compliance Tools ---
Write-Host "Linting: compliance tools..."
if (-not $skipDotnetTools) {
    dotnet reqstream --lint --enforce --requirements requirements.yaml
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    dotnet versionmark --lint
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    dotnet reviewmark --lint
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    # [PROJECT-SPECIFIC] SysML2 architecture model validation.
    if (Test-Path docs/sysml2) {
        dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
}

# [PROJECT-SPECIFIC] Add additional dotnet tool checks here.

# --- dotnet Format ---
Write-Host "Linting: dotnet format..."
$skipDotnetFormat = $false
dotnet restore > $null
if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetFormat = $true }

if (-not $skipDotnetFormat) {
    dotnet format --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
}

# [PROJECT-SPECIFIC] C/C++ clang-format check example:
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h","*.c" |
#       Where-Object { $_.FullName -notmatch '[/\\](thirdparty|third-party|3rd-party|generated)[/\\]' } |
#       ForEach-Object {
#           $result = clang-format --dry-run --Werror $_.FullName 2>&1
#           if ($LASTEXITCODE -ne 0) { Write-Output $result; $lintError = $true }
#       }

exit ($lintError ? 1 : 0)
