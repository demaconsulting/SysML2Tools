# lint.ps1
#
# PURPOSE:
#   Runs the lint checks and reports failures. Exits 1 on error.
#   Used by CI/CD as the merge gate (-All) and by the lint-fix agent during pre-PR cleanup.
#
#   To auto-fix formatting issues, run fix.ps1 instead.
#
# USAGE:
#   pwsh ./lint.ps1 -Help                     Print the usage text and exit 0
#   pwsh ./lint.ps1 -All                      All checks (CI merge gate)
#   pwsh ./lint.ps1 -Changed [-Base <ref>]    Only the checks whose inputs changed since <ref>
#                                             (default origin/main, else main) plus uncommitted changes;
#                                             a selected check runs over the whole repository (read-only),
#                                             except dotnet format, which is limited to the changed .cs files
#                                             unless project or build configuration files changed
#   pwsh ./lint.ps1 -Check lintmark,textlint  Only the named checks, in full; with -Changed, only the named
#                                             checks whose inputs changed
#   Add -DryRun to any scope to print which checks would run without running them.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific lint checks.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points, update tool versions, and add optional parameters that focus a run.
#   To add a check, add it to the $checks table (its name becomes a valid -Check value and
#   appears in the usage text), then add its input filter and its run step at the
#   [PROJECT-SPECIFIC] points. The reusable scoping logic lives in scripts/ProjectScripts.psm1,
#   which is delivered by the Agents release zip, is shared across repositories, and must never
#   be edited here.
#   With no arguments the script prints the usage text and exits 1; -All must still run every
#   check (CI relies on that), and never remove or weaken a check to make it pass.

param(
    # Run every check (what CI runs).
    [switch]$All,

    # Run only the checks whose inputs changed.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Run only the named checks (see the usage text for the valid names).
    [string[]]$Check,

    # Print which checks would run and exit without running them.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# ==============================================================================
# CHECK TABLE
# Every check in run order: its -Check name, the title shown in progress output, and the
# reason shown when -Changed skips it. [PROJECT-SPECIFIC] Add repository-specific checks here.
# ==============================================================================

$checks = [ordered]@{
    yamllint    = @{ Title = 'YAML';             Skip = 'no YAML files changed' }
    textlint    = @{ Title = 'spelling';         Skip = 'no spell-checked files changed' }
    lintmark    = @{ Title = 'markdown';         Skip = 'no Markdown files changed' }
    reqstream   = @{ Title = 'reqstream';        Skip = 'no requirements files changed' }
    versionmark = @{ Title = 'versionmark';      Skip = '.versionmark.yaml not changed' }
    reviewmark  = @{ Title = 'reviewmark';       Skip = 'no reviewed files changed' }
    sysml2tools = @{ Title = 'sysml2tools';      Skip = 'no SysML2 model files changed' }
    format      = @{ Title = 'dotnet format';    Skip = 'no C# or project files changed' }
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'lint.ps1' -IterateWith '-Changed (or -Check)' `
        -Purpose 'run the lint checks (the CI merge gate); exits 1 if any check fails.' `
        -Options ([ordered]@{
            '-All'           = 'Every check (what CI runs); cannot be combined with another scope.'
            '-Changed'       = 'Only the checks whose inputs changed (since the base, plus uncommitted); they run repository-wide.'
            '-Base <ref>'    = 'Base reference for -Changed (default origin/main, else main).'
            '-Check <names>' = 'Only the named checks, in full; with -Changed, only the named checks whose inputs changed.'
            '-DryRun'        = 'Print which checks would run or be skipped, then exit 0 without running anything.'
            '-Help'          = 'Print this text and exit 0.'
        }) `
        -ChecksLabel 'Checks' -Checks @($checks.Keys) `
        -Examples @('-Changed', '-Check lintmark,textlint', '-Check format -Changed -DryRun', '-All')
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

$wanted = @($checks.Keys)
if ($Check) {
    $wanted = Resolve-NameList -Names $Check -Valid @($checks.Keys) -Label 'check'
    if ($null -eq $wanted) { exit 1 }
}

# ==============================================================================
# CHECK SELECTION
# A check runs when it is wanted (-All, or named by -Check) and, with -Changed, when a file
# it reads changed. When the changed files cannot be determined, every wanted check runs.
# ==============================================================================

$inputsChanged = @{}
foreach ($name in $checks.Keys) { $inputsChanged[$name] = $true }
$formatInclude = @()

if ($Changed) {
    $files = Get-ChangedFiles -Base $Base
    if ($null -eq $files) {
        Write-Host "Running all selected checks: the changed files could not be determined."
    } else {
        Write-Host "Changed files: $($files.Count)"

        # A tool manifest change can change any dotnet tool, so it runs them all
        $toolManifest = Test-AnyMatch $files '^\.config/dotnet-tools\.json$'

        # yamllint: YAML files (including .yamllint.yaml) and its Python requirements
        $inputsChanged.yamllint = Test-AnyMatch $files '\.ya?ml$|^pip-requirements\.txt$'

        # TextLint: the file types in the 'files' setting of .textlint.yaml (and that YAML file itself)
        $inputsChanged.textlint = $toolManifest -or (Test-AnyMatch $files '\.(md|yaml|yml|json|cs|cpp|hpp|h|txt)$')

        # LintMark: Markdown files, .lintmark.yaml, and .editorconfig (LintMark reads both)
        $inputsChanged.lintmark = $toolManifest -or (Test-AnyMatch $files '\.md$|(^|/)\.lintmark\.yaml$|(^|/)\.editorconfig$')

        # ReqStream: the requirements tree
        $inputsChanged.reqstream = $toolManifest -or (Test-AnyMatch $files '^requirements\.yaml$|^docs/reqstream/')

        # VersionMark: its definition file
        $inputsChanged.versionmark = $toolManifest -or (Test-AnyMatch $files '^\.versionmark\.yaml$')

        # ReviewMark: its definition and the files its 'needs-review' patterns track
        $inputsChanged.reviewmark = $toolManifest -or (Test-AnyMatch $files "^\.reviewmark\.yaml$|$(Get-ReviewMarkPattern)")

        # SysML2Tools: the architecture model
        $inputsChanged.sysml2tools = $toolManifest -or (Test-AnyMatch $files '^docs/sysml2/')

        # dotnet format: the changed .cs files only, or everything when project or build configuration changed
        $formatScope = Get-DotnetFormatScope $files
        $inputsChanged.format = $formatScope.Run
        $formatInclude = $formatScope.Include

        # [PROJECT-SPECIFIC] Add input filters for repository-specific checks here.

        # A change to this script or the shared module changes the selection logic itself, so every selected
        # check runs over the whole repository (no input filter could say what it affects)
        if (Test-AnyMatch $files '^(lint\.ps1|scripts/ProjectScripts\.psm1)$') {
            Write-Host "Running all selected checks over the whole repository: lint.ps1 or scripts/ProjectScripts.psm1 changed."
            foreach ($name in $checks.Keys) { $inputsChanged[$name] = $true }
            $formatInclude = @()
        }
    }
}

$run = @{}
$skipReason = @{}
foreach ($name in $checks.Keys) {
    $run[$name] = ($name -in $wanted) -and $inputsChanged[$name]
    if ($name -notin $wanted) { $skipReason[$name] = 'not selected by -Check' }
    else { $skipReason[$name] = $checks[$name].Skip }
}
if (-not $run.format) { $formatInclude = @() }

function Write-Skipped {
    param([string]$Name)
    Write-Host "Linting: $($checks[$Name].Title)... skipped ($($skipReason[$Name]))"
}

if ($DryRun) {
    foreach ($name in $checks.Keys) {
        Write-Host ("  {0,-12} {1}" -f $name, ($run[$name] ? 'run' : "skip ($($skipReason[$name]))"))
    }
    if ($formatInclude.Count -gt 0) { Write-Host "  dotnet format limited to: $($formatInclude -join ' ')" }
    Write-Host "Dry run: no check was run."
    exit 0
}

# ==============================================================================
# LINT CHECKS
# Runs the selected lint checks. Exits 1 if any check fails.
# ==============================================================================

$lintError = $false

# --- PYTHON SECTION ---
# Sets up a virtual environment and runs yamllint.
$skipPython = $true
if ($run.yamllint) {
    Write-Host "Linting: YAML..."
    $skipPython = -not (Initialize-PythonVenv)
    if ($skipPython) { $lintError = $true }

    if (-not $skipPython) {
        yamllint .
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
        deactivate
    }
} else {
    Write-Skipped 'yamllint'
}

# [PROJECT-SPECIFIC] Add additional Python-based lint checks here.
# Example:
#   if (-not $skipPython) {
#       flake8 src/
#       if ($LASTEXITCODE -ne 0) { $lintError = $true }
#   }

# --- DOTNET TOOLS RESTORE ---
# Restores the local dotnet tools; required by TextLint, LintMark, and the compliance tools.
$skipDotnetTools = $true
if ($run.textlint -or $run.lintmark -or $run.reqstream -or $run.versionmark -or $run.reviewmark -or $run.sysml2tools) {
    $skipDotnetTools = $false
    dotnet tool restore > $null
    if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetTools = $true }
}

# --- SPELLING SECTION ---
# Runs TextLint using the files configured in .textlint.yaml.
if ($run.textlint) {
    Write-Host "Linting: spelling..."
    if (-not $skipDotnetTools) {
        dotnet textlint
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'textlint'
}

# --- MARKDOWN SECTION ---
# Runs LintMark using the settings in .lintmark.yaml.
if ($run.lintmark) {
    Write-Host "Linting: markdown..."
    if (-not $skipDotnetTools) {
        dotnet lintmark
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'lintmark'
}

# [PROJECT-SPECIFIC] Add additional lint checks here.

# --- DOTNET LINTING SECTION ---
# Runs compliance tools: reqstream, versionmark, reviewmark, sysml2tools.
if ($run.reqstream) {
    Write-Host "Linting: reqstream..."
    if (-not $skipDotnetTools) {
        dotnet reqstream --lint --enforce --requirements requirements.yaml
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'reqstream'
}

if ($run.versionmark) {
    Write-Host "Linting: versionmark..."
    if (-not $skipDotnetTools) {
        dotnet versionmark --lint
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'versionmark'
}

if ($run.reviewmark) {
    Write-Host "Linting: reviewmark..."
    if (-not $skipDotnetTools) {
        dotnet reviewmark --lint
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'reviewmark'
}

if ($run.sysml2tools) {
    Write-Host "Linting: sysml2tools..."
    # [PROJECT-SPECIFIC] SysML2 architecture model validation.
    # --strict-unresolved reports unresolved references as errors (non-zero exit code). Other
    # diagnostics, such as duplicate root packages or import cycles, are still only warnings that
    # do not change the exit code, so the output is captured and any remaining warning line also
    # fails lint: the model must lint with zero errors and zero warnings.
    if (-not $skipDotnetTools -and (Test-Path docs/sysml2)) {
        $sysmlOutput = dotnet sysml2tools lint --strict-unresolved 'docs/sysml2/**/*.sysml' 2>&1 | ForEach-Object { "$_" }
        $sysmlExit = $LASTEXITCODE
        $sysmlOutput | Write-Host
        if ($sysmlExit -ne 0) { $lintError = $true }
        if ($sysmlOutput -match ': warning: ') {
            Write-Host "sysml2tools lint reported warnings (treated as errors)."
            $lintError = $true
        }
    }
} else {
    Write-Skipped 'sysml2tools'
}

# [PROJECT-SPECIFIC] Add additional dotnet tool lint checks here.
# Example:
#   if (-not $skipDotnetTools) {
#       dotnet custom-tool --lint
#       if ($LASTEXITCODE -ne 0) { $lintError = $true }
#   }

# --- DOTNET FORMATTING SECTION ---
# Verifies C# code formatting matches .editorconfig rules.
if ($run.format) {
    if ($formatInclude.Count -gt 0) {
        Write-Host "Linting: dotnet format ($($formatInclude.Count) changed C# files)..."
    } else {
        Write-Host "Linting: dotnet format..."
    }
    $skipDotnetFormat = $false
    dotnet restore > $null
    if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetFormat = $true }

    if (-not $skipDotnetFormat) {
        $formatArgs = @('--verify-no-changes', '--no-restore')
        if ($formatInclude.Count -gt 0) { $formatArgs += @('--include') + $formatInclude }
        dotnet format @formatArgs
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
} else {
    Write-Skipped 'format'
}

# [PROJECT-SPECIFIC] Add additional format verification checks here.
# Example (clang-format check for C/C++):
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" | ForEach-Object {
#       $result = clang-format --dry-run --Werror $_.FullName 2>&1
#       if ($LASTEXITCODE -ne 0) { Write-Output $result; $lintError = $true }
#   }

exit ($lintError ? 1 : 0)
