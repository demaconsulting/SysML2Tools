# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Regenerates the pre-compiled stdlib resource, builds the solution in Release
#   configuration, and runs all unit tests.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run (for example
#   -Project <name> in a multi-library repository). Slow or environment-dependent test tiers
#   (GUI automation, integration) may be opt-in switches.
#   Run with no arguments it must still build and run the standard test suite (agents rely
#   on that), and never remove or weaken a test step to make it pass.

$buildError = $false

# [PROJECT-SPECIFIC] Regenerate the pre-compiled stdlib.json.gz resource before building, so
# it can never silently go stale after editing stdlib source files. This runs as a plain,
# sequential step here - not as a build-time dependency of Stdlib.csproj - to avoid
# MSBuild-in-MSBuild coordination with projects it shares dependencies with. StdlibGen only
# overwrites its output when content actually changed, so this is a cheap no-op on most runs.
# A failure here stops the build: building against a stale or missing resource is meaningless.
Write-Host "Regenerating stdlib.json.gz..."
$stdlibDir = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Stdlib'
$stdlibOutput = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Resources\stdlib.json.gz'
$stdlibGenProject = Join-Path $PSScriptRoot 'src\Tools\StdlibGen\StdlibGen.csproj'
dotnet run --project $stdlibGenProject --configuration Release -- --stdlib-dir $stdlibDir --output $stdlibOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Restoring dependencies..."
dotnet restore
if ($LASTEXITCODE -ne 0) { $buildError = $true }

Write-Host "Building..."
dotnet build --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { $buildError = $true }

# Tests run on Microsoft.Testing.Platform (global.json), so the VSTest '--logger trx' option
# used by the template does not apply here.
Write-Host "Running tests..."
dotnet test --no-build --configuration Release
if ($LASTEXITCODE -ne 0) { $buildError = $true }

# [PROJECT-SPECIFIC] Add additional build steps here (e.g., packaging, publishing).

exit ($buildError ? 1 : 0)
