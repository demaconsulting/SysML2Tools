# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs all unit tests.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# MODIFICATION POLICY:
#   Only modify this file to add project-specific operations at the designated
#   [PROJECT-SPECIFIC] extension points.

$ErrorActionPreference = 'Stop'

# [PROJECT-SPECIFIC] Regenerate the pre-compiled stdlib.json.gz resource before building, so
# it can never silently go stale after editing stdlib source files. This runs as a plain,
# sequential step here - not as a build-time dependency of Stdlib.csproj - to avoid
# MSBuild-in-MSBuild coordination with projects it shares dependencies with. StdlibGen only
# overwrites its output when content actually changed, so this is a cheap no-op on most runs.
Write-Host "Regenerating stdlib.json.gz..."
$stdlibDir = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Stdlib'
$stdlibOutput = Join-Path $PSScriptRoot 'src\DemaConsulting.SysML2Tools.Stdlib\Resources\stdlib.json.gz'
$stdlibGenProject = Join-Path $PSScriptRoot 'src\Tools\StdlibGen\StdlibGen.csproj'
dotnet run --project $stdlibGenProject --configuration Release -- --stdlib-dir $stdlibDir --output $stdlibOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building project..."
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional build steps here.

Write-Host "Running unit tests..."
dotnet test --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional test or post-build steps here.

Write-Host "Build and tests completed successfully!"
