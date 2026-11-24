# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Regenerates the pre-compiled stdlib resource, builds the solution in Release configuration, and
#   runs the unit tests, either all of them (-All, the full local build and test run) or only the
#   test projects a change affects (-Changed / -Project).
#
# USAGE:
#   pwsh ./build.ps1 -Help                     Print the usage text and exit 0
#   pwsh ./build.ps1 -All                      Full local build and all tests
#   pwsh ./build.ps1 -Changed [-Base <ref>]    Only the test projects affected by the changes since <ref>
#                                              (default origin/main, else main) plus uncommitted changes
#   pwsh ./build.ps1 -Project Core,Tool        Only the test projects affected by the named projects (full names
#                                              or the short names Core, Language, Stdlib, Tool, Tests, Tool.Tests)
#   Add -DryRun to any scope to print the selection without building anything.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
#   A changed project selects every project that references it, directly or transitively, from the
#   ProjectReference graph, and files linked into another project (Compile/None/EmbeddedResource items
#   that point outside the project folder) select that project too. Changes that affect the whole
#   build (solution, project, props/targets, global.json, .editorconfig, nuget.config, tool manifest,
#   this script, scripts/ProjectScripts.psm1) fall back to the full build; repository files tests read
#   at run time select their test projects through $RuntimeReads; other changes the build does not
#   read select nothing.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run. The reusable scoping
#   logic lives in scripts/ProjectScripts.psm1, which is delivered by the Agents release zip, is
#   shared across repositories, and must never be edited here. Slow or environment-dependent test
#   tiers (GUI automation, integration) may be opt-in switches.
#   With no arguments the script prints the usage text and exits 1; -All must still build and run
#   the standard test suite (agents rely on that), and never remove or weaken a test step to make
#   it pass.

param(
    # Full local build and all tests.
    [switch]$All,

    # Build and test only the projects affected by the changed files.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Build and test only the projects affected by these projects (full or short names, e.g. Core, Tool).
    [string[]]$Project,

    # Print the selection and exit without building.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# [PROJECT-SPECIFIC] Repository files that tests read at run time (through the repository root, not through a
# project item), mapped to the test project that reads them. Keep in step with the tests. A pattern maps to one
# project, so a file read by both test projects appears under two equivalent patterns.
$RuntimeReads = [ordered]@{
    '^test/SysMLModels/'       = 'DemaConsulting.SysML2Tools.Tests'
    '^test/SysMLModels(/|$)'   = 'DemaConsulting.SysML2Tools.Tool.Tests'
    '^docs/gallery/'           = 'DemaConsulting.SysML2Tools.Tests'
    '^docs/gallery(/|$)'       = 'DemaConsulting.SysML2Tools.Tool.Tests'
    '^docs/sysml2/'            = 'DemaConsulting.SysML2Tools.Tool.Tests'
}

# [PROJECT-SPECIFIC] Short -Project names are the full project names without this prefix (Core, Language, Stdlib,
# Tool, Tests, Tool.Tests); the shared Resolve-ProjectName cannot derive them because the StdlibGen tool under src/
# has the shortest name there.
$ProjectPrefix = 'DemaConsulting.SysML2Tools.'

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'build.ps1' -IterateWith '-Changed (or -Project)' `
        -Purpose 'build the solution (Release) and run the unit tests.' `
        -Options ([ordered]@{
            '-All'             = 'Full local build and all tests; cannot be combined with another scope.'
            '-Changed'         = 'Only the test projects affected by files changed since the base, plus uncommitted files.'
            '-Base <ref>'      = 'Base reference for -Changed (default origin/main, else main).'
            '-Project <names>' = 'Only the test projects affected by these projects (full or short names, e.g. Core,Tool).'
            '-DryRun'          = 'Print what would be built, tested, and skipped, then exit 0 without running anything.'
            '-Help'            = 'Print this text and exit 0.'
        }) `
        -ChecksLabel 'Projects (-Project names; short names drop the DemaConsulting.SysML2Tools. prefix)' `
        -Checks @(try { (Get-ProjectGraph).Keys } catch { }) `
        -Examples @('-Changed', '-Project Core', '-Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Project = [bool]$Project
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

$buildError = $false

# [PROJECT-SPECIFIC] Regenerate the pre-compiled stdlib.json.gz resource before building, so
# it can never silently go stale after editing stdlib source files. This runs as a plain,
# sequential step here - not as a build-time dependency of Stdlib.csproj - to avoid
# MSBuild-in-MSBuild coordination with projects it shares dependencies with. StdlibGen only
# overwrites its output when content actually changed, so this is a cheap no-op on most runs.
# A failure here stops the build: building against a stale or missing resource is meaningless.
function Update-StdlibResource {
    Write-Host "Regenerating stdlib.json.gz..."
    $stdlibDir = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Stdlib'
    $stdlibOutput = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Resources\stdlib.json.gz'
    $stdlibGenProject = Join-Path $PSScriptRoot 'src\Tools\StdlibGen\StdlibGen.csproj'
    dotnet run --project $stdlibGenProject --configuration Release -- --stdlib-dir $stdlibDir --output $stdlibOutput
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# ==============================================================================
# SCOPED BUILD (-Changed / -Project)
# Selects the affected test projects; falls back to the full build when needed.
# ==============================================================================

$scoped = $false
if (-not $All) {
    $graph = Get-ProjectGraph
    $selected = [System.Collections.Generic.HashSet[string]]::new()
    $fullReason = $null

    $names = @($Project | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($Project -and $names.Count -eq 0) {
        Write-Host "Error: no project named in '$($Project -join ' ')'. Known projects: $($graph.Keys -join ', ')"
        exit 1
    }
    foreach ($single in $names) {
        # [PROJECT-SPECIFIC] Map a short name (Core, Tool.Tests) to the full project name before resolving it
        $mapped = if ($graph.Contains($single)) { $single } elseif ($graph.Contains("$ProjectPrefix$single")) { "$ProjectPrefix$single" } else { $single }
        $resolved = Resolve-ProjectName $graph $mapped
        if (-not $resolved) {
            Write-Host "Unknown project '$single'. Known projects: $($graph.Keys -join ', ')"
            exit 1
        }
        [void]$selected.Add($resolved)
    }

    if ($Changed) {
        $changedFiles = Get-ChangedFiles -Base $Base
        if ($null -eq $changedFiles) {
            $fullReason = 'the changed files could not be determined'
        } else {
            Write-Host "Changed files: $($changedFiles.Count)"
            $fullReason = Add-ChangedProjects -Graph $graph -Files $changedFiles -Selected $selected -RuntimeReads $RuntimeReads
        }
    }

    if ($fullReason) {
        Write-Host "Full build required: $fullReason."
    } else {
        # [PROJECT-SPECIFIC] StdlibGen produces the embedded stdlib resource but nothing references it, so a
        # change to it selects the Stdlib project (and through it everything that embeds the resource)
        if ($selected.Contains('StdlibGen') -and $graph.Contains("${ProjectPrefix}Stdlib")) {
            [void]$selected.Add("${ProjectPrefix}Stdlib")
        }

        Expand-ReferencingProjects -Graph $graph -Selected $selected

        # Test projects are built and tested; other leaf projects (tools/) are built only
        $tests = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'test/*' })
        $tools = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'tools/*' })
        $skipped = @($graph.Values | Where-Object { $_.Directory -like 'test/*' -and -not $selected.Contains($_.Name) })
        if ($tests.Count -eq 0 -and $tools.Count -eq 0) {
            Write-Host "Nothing to build: no changed file is read by the build."
            exit 0
        }

        Write-Host "Affected test projects (built and tested):"
        $tests | ForEach-Object { Write-Host "  $($_.Name)" }
        if ($tools.Count -gt 0) {
            Write-Host "Affected tool projects (build only):"
            $tools | ForEach-Object { Write-Host "  $($_.Name)" }
        }
        if ($DryRun) {
            Write-Host "Skipped test projects ($($skipped.Count)): $(($skipped | ForEach-Object { $_.Name }) -join ', ')"
            Write-Host "Dry run: nothing was built or tested (stdlib.json.gz would be regenerated first)."
            exit 0
        }

        $scoped = $true
        Update-StdlibResource
        foreach ($entry in $tests + $tools) {
            Write-Host "Building $($entry.Name)..."
            dotnet build $entry.Path --configuration Release
            if ($LASTEXITCODE -ne 0) { $buildError = $true }
        }

        # Tests run on Microsoft.Testing.Platform (global.json), so the VSTest '--logger trx' option
        # used by the template does not apply here.
        if (-not $buildError) {
            foreach ($entry in $tests) {
                Write-Host "Running tests: $($entry.Name)..."
                dotnet test $entry.Path --no-build --configuration Release
                if ($LASTEXITCODE -ne 0) { $buildError = $true }
            }
        } else {
            Write-Host "Skipping tests: the build failed."
        }
    }
}

if ($DryRun -and -not $scoped) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }

# ==============================================================================
# FULL BUILD (-All, or a scoped run that needs everything)
# ==============================================================================

if (-not $scoped) {
    Update-StdlibResource

    # Each step runs only if the steps before it succeeded, so tests never run against stale binaries
    Write-Host "Restoring dependencies..."
    dotnet restore
    if ($LASTEXITCODE -ne 0) { $buildError = $true }

    if (-not $buildError) {
        Write-Host "Building..."
        dotnet build --no-restore --configuration Release
        if ($LASTEXITCODE -ne 0) { $buildError = $true }
    }

    # Tests run on Microsoft.Testing.Platform (global.json), so the VSTest '--logger trx' option
    # used by the template does not apply here.
    if (-not $buildError) {
        Write-Host "Running tests..."
        dotnet test --no-build --configuration Release
        if ($LASTEXITCODE -ne 0) { $buildError = $true }
    } else {
        Write-Host "Skipping tests: the build failed."
    }

    # [PROJECT-SPECIFIC] Add additional build steps here (e.g., packaging, publishing).
    # Guard steps that depend on the build with: if (-not $buildError) { ... }
}

if (-not $buildError) {
    Write-Host "Build and tests completed successfully!"
}

exit ($buildError ? 1 : 0)
