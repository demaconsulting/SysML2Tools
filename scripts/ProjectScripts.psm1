# ProjectScripts.psm1
#
# SHARED ACROSS DEMA REPOSITORIES. This module is delivered by the Agents release zip; its source of truth
# is src/scripts/ProjectScripts.psm1 in the Agents repository, and every copy must be byte-identical to that one.
# NEVER EDIT IT IN A CONSUMING REPOSITORY: a local edit is lost at the next sync and makes
# the scripts of that repository differ from every other one.
#
# Repository-specific behavior belongs in the [PROJECT-SPECIFIC] blocks of build.ps1,
# lint.ps1, and fix.ps1 (for example the $RuntimeReads map of build.ps1, extra checks, or
# extra fixers). If something here has to change for every repository, change it in the Agents repository.
#
# PURPOSE:
#   Holds the repository-agnostic logic the three project scripts share, so the scripts stay
#   small and their scoping contract is identical everywhere:
#     - usage text and scope-argument validation (no arguments is an error, -All excludes the rest)
#     - changed-file discovery (merge-base with a base reference, plus uncommitted files)
#     - Python virtual environment setup for yamllint and yamlfix
#     - dotnet format scoping and ReviewMark pattern building
#     - the .NET project graph: ProjectReference closure, linked items, and short project names with aliases
#
# Each script imports it with:
#   Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force
#
# All functions assume the current directory is the repository root (the scripts run there).

# ==============================================================================
# USAGE AND SCOPE ARGUMENTS
# ==============================================================================

function Write-ScriptUsage {
    <#
    .SYNOPSIS
        Prints the standard usage text of a project script to the host.
    .DESCRIPTION
        Gives build.ps1, lint.ps1, and fix.ps1 one identical, short, self-documenting usage layout
        (purpose, options, examples, workflow), so a caller - especially an AI agent - can decide
        what to run without reading the script or any other document.
    .PARAMETER Script
        The script file name, for example 'build.ps1'.
    .PARAMETER Purpose
        One line describing what the script does.
    .PARAMETER Options
        Ordered dictionary of option text (for example '-Base <ref>') to a one-line description.
    .PARAMETER Examples
        Arguments of the example invocations (the 'pwsh ./<script>' prefix is added).
    .PARAMETER ChecksLabel
        Heading for the optional name list, for example 'Checks'.
    .PARAMETER Checks
        Optional list of valid names for -Check, derived by the script from its own check table.
    .PARAMETER IterateWith
        The scope switches to iterate with, shown in the final workflow line.
    #>
    param(
        [Parameter(Mandatory)][string]$Script,
        [Parameter(Mandatory)][string]$Purpose,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Options,
        [Parameter(Mandatory)][string[]]$Examples,
        [string]$ChecksLabel,
        [string[]]$Checks,
        [string]$IterateWith = '-Changed'
    )

    Write-Host "$Script - $Purpose"
    Write-Host "A scope is required: with no arguments this text is printed and the script exits 1."
    Write-Host ""
    $width = ($Options.Keys | ForEach-Object { $_.Length } | Measure-Object -Maximum).Maximum
    foreach ($key in $Options.Keys) {
        Write-Host ("  {0,-$width}  {1}" -f $key, $Options[$key])
    }
    if ($Checks) {
        Write-Host ""
        Write-Host "${ChecksLabel}: $($Checks -join ', ')"
    }
    Write-Host ""
    Write-Host "Examples:"
    foreach ($example in $Examples) { Write-Host "  pwsh ./$Script $example" }
    Write-Host ""
    Write-Host "Workflow: iterate with $IterateWith; run -All once before handing work back or opening a pull request."
}

function Test-ScopeArguments {
    <#
    .SYNOPSIS
        Validates the scope switches of a project script and returns the problems found.
    .DESCRIPTION
        Enforces the shared contract in one place: a scope must be chosen (so an un-updated caller
        fails loudly instead of silently running everything or nothing), -All cannot be combined
        with another scope, and -Base only makes sense with -Changed. Returns an empty array when
        the arguments are valid; the script prints the messages, the usage text, and exits 1.
    .PARAMETER Scopes
        Ordered dictionary of scope switch name (All, Changed, Project, Check) to whether it was
        supplied. Pass only the scopes the script offers; 'All' and 'Changed' are required keys.
    .PARAMETER Base
        The -Base value, validated to be used only together with -Changed.
    #>
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Scopes,
        [string]$Base
    )

    $problems = @()
    $chosen = @($Scopes.Keys | Where-Object { $Scopes[$_] })
    $names = ($Scopes.Keys | ForEach-Object { "-$_" }) -join ', '

    if ($chosen.Count -eq 0) {
        $problems += "No scope selected. Choose one or more of: $names."
    }
    if ($Scopes['All'] -and $chosen.Count -gt 1) {
        $others = ($chosen | Where-Object { $_ -ne 'All' } | ForEach-Object { "-$_" }) -join ', '
        $problems += "-All cannot be combined with another scope ($others)."
    }
    if ($Base -and -not $Scopes['Changed']) {
        $problems += "-Base is only valid together with -Changed."
    }
    return , $problems
}

function Resolve-NameList {
    <#
    .SYNOPSIS
        Splits, trims, and validates a list of names against the valid names.
    .DESCRIPTION
        Accepts names given as separate values or comma-separated (-Check a,b and -Check a -Check b
        both work), matches them case-insensitively, and returns the canonical spelling. An unknown
        name prints an error that lists the valid names and returns $null, so the script can exit 1.
        A value that names nothing (only delimiters or blanks, such as -Check ',') is an error too,
        handled the same way: otherwise the scope would be accepted and the script would run no
        check and exit 0.
    .PARAMETER Names
        The names as supplied by the caller.
    .PARAMETER Valid
        The valid names (derived by the script from its own table).
    .PARAMETER Label
        Singular noun used in the error message, for example 'check'.
    #>
    param(
        [string[]]$Names,
        [Parameter(Mandatory)][string[]]$Valid,
        [Parameter(Mandatory)][string]$Label
    )

    $resolved = [System.Collections.Generic.List[string]]::new()
    $tokens = @($Names | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($null -ne $Names -and @($Names).Count -gt 0 -and $tokens.Count -eq 0) {
        Write-Host "No $Label named in '$(@($Names) -join ' ')'. Valid ${Label}s: $($Valid -join ', ')"
        return $null
    }
    foreach ($name in $tokens) {
        $match = $Valid | Where-Object { $_ -eq $name } | Select-Object -First 1
        if (-not $match) {
            Write-Host "Unknown $Label '$name'. Valid ${Label}s: $($Valid -join ', ')"
            return $null
        }
        if (-not $resolved.Contains($match)) { $resolved.Add($match) }
    }
    return , $resolved.ToArray()
}

# ==============================================================================
# CHANGED FILES
# ==============================================================================

function Get-ChangedFiles {
    <#
    .SYNOPSIS
        Lists the repository files changed since the merge-base with a base reference.
    .DESCRIPTION
        Implements the scope of -Changed identically for every script: committed changes since the
        merge-base with the base reference, plus staged, unstaged, and untracked files. The base
        defaults to origin/main and falls back to main. Returns $null when the files cannot be
        determined, so the caller can fall back to running everything rather than nothing.
    .PARAMETER Base
        Git reference to compare against (default: origin/main, else main).
    #>
    param([string]$Base)

    # Default to origin/main, falling back to the local main branch
    if (-not $Base) {
        $Base = 'origin/main'
        git rev-parse --verify --quiet "$Base^{commit}" > $null 2>&1
        if ($LASTEXITCODE -ne 0) { $Base = 'main' }
    }
    git rev-parse --verify --quiet "$Base^{commit}" > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Cannot resolve base reference '$Base'."
        return $null
    }
    Write-Host "Changes since: $Base (plus uncommitted and untracked files)"

    # Committed changes since the merge-base, then staged, unstaged, and untracked files
    $committed = @(git -c core.quotePath=false diff --name-only --no-renames "$Base...HEAD")
    if ($LASTEXITCODE -ne 0) { return $null }
    $working = @(git -c core.quotePath=false status --porcelain --untracked-files=all --no-renames)
    if ($LASTEXITCODE -ne 0) { return $null }

    $files = $committed + @($working | ForEach-Object { $_.Substring(3).Trim('"') })
    return , @($files | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)
}

function Test-AnyMatch {
    <#
    .SYNOPSIS
        Tests whether any file matches a regular expression.
    .DESCRIPTION
        The one-line test every per-check input filter uses to decide whether a check's inputs changed.
    .PARAMETER Files
        Repository-relative file paths with forward slashes.
    .PARAMETER Pattern
        Regular expression matched against each path.
    #>
    param([string[]]$Files, [string]$Pattern)
    return @($Files | Where-Object { $_ -match $Pattern }).Count -gt 0
}

function Convert-GlobToRegex {
    <#
    .SYNOPSIS
        Converts a glob to an anchored regular expression.
    .DESCRIPTION
        Lets scripts match changed files against globs from tool configuration (MSBuild items,
        ReviewMark patterns). '**' spans folders; '*' and '?' stay within one folder.
    .PARAMETER Glob
        The glob with forward slashes.
    #>
    param([string]$Glob)

    $regex = [regex]::Escape($Glob)
    $regex = $regex -replace '\\\*\\\*/', '(.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*' -replace '\\\?', '[^/]'
    return "^$regex$"
}

function Get-ReviewMarkPattern {
    <#
    .SYNOPSIS
        Builds one regular expression from the 'needs-review' globs of .reviewmark.yaml.
    .DESCRIPTION
        Lets -Changed run ReviewMark only when a file it tracks changed. '!' excludes are ignored,
        which can only run the check more often, never less. Returns a match-all pattern when no
        globs are found, so the check is never skipped by mistake.
    #>
    $globs = @()
    $inList = $false
    foreach ($line in Get-Content .reviewmark.yaml -ErrorAction SilentlyContinue) {
        if ($line -match '^needs-review:') { $inList = $true; continue }
        if ($inList -and $line -match '^\s+-\s+["'']?([^"''#\s]+)') {
            if (-not $Matches[1].StartsWith('!')) { $globs += $Matches[1] }
        } elseif ($inList -and $line -match '^\S') { break }
    }
    if ($globs.Count -eq 0) { return '.' }
    return (($globs | ForEach-Object { Convert-GlobToRegex $_ }) -join '|')
}

function Get-DotnetFormatScope {
    <#
    .SYNOPSIS
        Decides whether dotnet format runs for the changed files, and on which files.
    .DESCRIPTION
        Shared by lint.ps1 and fix.ps1 so both limit dotnet format identically: only the changed,
        still-existing .cs files, unless a project or build configuration file changed (then the whole
        solution, because that can change the formatting of any file). Over $MaxFiles files the whole
        solution is cheaper than a long command line.
    .PARAMETER Files
        Changed files from Get-ChangedFiles.
    .PARAMETER MaxFiles
        Largest number of .cs files passed to --include.
    .OUTPUTS
        Object with Run (bool) and Include (the .cs files to pass to --include; empty means everything).
    #>
    param([string[]]$Files, [int]$MaxFiles = 200)

    $projectChanged = @($Files | Where-Object { Test-BuildConfigFile $_ }).Count -gt 0
    $csFiles = @($Files | Where-Object { $_ -match '\.cs$' -and (Test-Path -LiteralPath $_) })
    $include = @()
    if (-not $projectChanged -and $csFiles.Count -gt 0 -and $csFiles.Count -le $MaxFiles) { $include = $csFiles }
    return [pscustomobject]@{
        Run     = $projectChanged -or $csFiles.Count -gt 0
        Include = $include
    }
}

# ==============================================================================
# PYTHON AND YAML
# ==============================================================================

function Get-VenvActivateScript {
    <#
    .SYNOPSIS
        Returns the path of the virtual environment's PowerShell activation script, or $null.
    .DESCRIPTION
        The layout differs between Windows (.venv/Scripts) and Linux/macOS (.venv/bin).
    #>
    if (Test-Path ".venv/Scripts/Activate.ps1") { return ".venv/Scripts/Activate.ps1" }  # Windows
    if (Test-Path ".venv/bin/Activate.ps1") { return ".venv/bin/Activate.ps1" }          # Linux/macOS
    return $null
}

function Initialize-PythonVenv {
    <#
    .SYNOPSIS
        Creates, activates, and populates the .venv virtual environment from pip-requirements.txt.
    .DESCRIPTION
        yamllint and yamlfix are Python tools; both scripts need the same environment. Returns $true
        with the environment left active (the caller runs 'deactivate' afterwards), or $false with
        it deactivated. -Silent hides all tool output, for fix.ps1, which must never look like a problem.
    .PARAMETER Silent
        Suppress the output of venv creation, activation, and pip.
    #>
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

function Convert-YamlLineEndings {
    <#
    .SYNOPSIS
        Rewrites CRLF line endings in all YAML files to LF (UTF-8 without a BOM).
    .DESCRIPTION
        yamlfix can leave CRLF endings on Windows, which yamllint then rejects and which changes
        ReviewMark fingerprints. Vendored and generated folders are skipped.
    #>
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

# ==============================================================================
# .NET PROJECT GRAPH
# ==============================================================================

function Resolve-RepoPath {
    <#
    .SYNOPSIS
        Combines a repository-relative directory with a relative path, collapsing '.' and '..'.
    .DESCRIPTION
        Needed to find where a project item that points outside its own folder (a linked file) lands.
    .PARAMETER Directory
        Repository-relative directory of the project.
    .PARAMETER RelativePath
        Path relative to that directory, as written in the project file.
    #>
    param([string]$Directory, [string]$RelativePath)

    $segments = [System.Collections.Generic.List[string]]::new()
    foreach ($segment in "$Directory/$RelativePath" -replace '\\', '/' -split '/') {
        if ($segment -eq '..') { if ($segments.Count -gt 0) { $segments.RemoveAt($segments.Count - 1) } }
        elseif ($segment -and $segment -ne '.') { $segments.Add($segment) }
    }
    return $segments -join '/'
}

# Project item types whose Include is a name or a package, not a file path, so they can never link a file in from
# outside the project folder. Every other item type is treated as a file item (Compile, None, Content,
# EmbeddedResource, AvaloniaResource, Page, Resource, COMFileReference, Analyzer, AdditionalFiles, ...).
# A Reference may name its file in a HintPath instead; Get-ProjectGraph reads that.
$script:NonFileItems = @(
    'PackageReference', 'PackageVersion', 'GlobalPackageReference', 'PackageDownload', 'DotNetCliToolReference',
    'FrameworkReference', 'Reference', 'SDKReference', 'COMReference', 'Using', 'InternalsVisibleTo',
    'AssemblyAttribute', 'ProjectCapability', 'TrimmerRootAssembly'
)

# Project properties whose value is a file path (relative to the project folder)
$script:FileProperties = @(
    'ApplicationIcon', 'Win32Icon', 'ApplicationManifest', 'Win32Manifest', 'PackageIcon', 'PackageReadmeFile',
    'PackageLicenseFile', 'AssemblyOriginatorKeyFile'
)

function Get-LinkedFilePattern {
    <#
    .SYNOPSIS
        Returns the regular expression of a project file path that points outside the project folder.
    .DESCRIPTION
        Used by Get-ProjectGraph for item Include/Update values and file-valued properties. Understands
        $(MSBuildThisFileDirectory) and $(MSBuildProjectDirectory); a path with any other MSBuild property
        cannot be resolved here and yields nothing. A path inside the project folder yields nothing too.
    .PARAMETER Directory
        Repository-relative directory of the project.
    .PARAMETER Path
        The path as written in the project file; may be a glob and may use either slash.
    #>
    param([string]$Directory, [string]$Path)

    # A project at the repository root has no folder to point out of
    if (-not $Directory) { return }

    $part = ($Path -replace '\$\(MSBuildThisFileDirectory\)', '' -replace '\$\(MSBuildProjectDirectory\)[\\/]?', '').Trim()
    if (-not $part -or $part.Contains('$(')) { return }

    $resolved = Resolve-RepoPath $Directory $part
    if (-not $resolved.StartsWith("$Directory/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return Convert-GlobToRegex $resolved
    }
}

function Get-ItemMetadata {
    <#
    .SYNOPSIS
        Reads one metadata value of a project item, written either as an attribute or as a child element.
    .PARAMETER Item
        The item XML element.
    .PARAMETER Name
        The metadata name, for example CopyToOutputDirectory.
    #>
    param($Item, [string]$Name)

    $value = $Item.GetAttribute($Name)
    if (-not $value) { $value = $Item.SelectSingleNode($Name)?.InnerText }
    return "$value".Trim()
}

function Get-ProjectGraph {
    <#
    .SYNOPSIS
        Reads every .csproj under the given root folders into a project graph.
    .DESCRIPTION
        For each project records its folder, its ProjectReference names, and the files outside its
        folder that it uses (linked files, as regular expressions). This is the
        input of the scoped build: a changed file selects its project, every project referencing it,
        and every project that links the file in. Convention: src/ holds libraries, test/ holds test
        projects, and tools/ holds projects that are only built.

        A linked file is any project item of any type (Compile, None, Content, EmbeddedResource,
        AvaloniaResource, Page, Resource, ...) whose Include or Update path, or any of the file-valued
        properties ApplicationIcon, Win32Icon, ApplicationManifest, PackageIcon, PackageReadmeFile,
        PackageLicenseFile, and AssemblyOriginatorKeyFile, resolves to a place outside the project
        folder. Items that name no file (PackageReference, Using, ...) are ignored, and so are files a
        project only packs into its NuGet package (a None item with Pack=true and no copy to the output;
        CopyToOutputDirectory counts only as Always, PreserveNewest, or IfDifferent). A Reference links
        the file named by its HintPath.
        Wildcards are supported, and $(MSBuildThisFileDirectory) and $(MSBuildProjectDirectory) are
        understood; any other MSBuild property in a path is left unresolved and ignored.

        Projects are keyed by file name because ProjectReference entries are matched by file name.
        When two scanned projects share a file name (for example src/Foo/Foo.csproj and
        test/Foo/Foo.csproj) the graph would be ambiguous, so the function throws instead.
    .PARAMETER Root
        Folders to scan for projects (missing folders are ignored). The default is src, test, and tools.
        A repository with projects elsewhere (samples/, demo/, benchmarks/) passes its own list, kept in
        one $ProjectRoots variable in build.ps1, for example @('src', 'test', 'tools', 'demo'); the list
        is explicit on purpose, because fixture projects that are test inputs or opt-in suites must stay out.
    .OUTPUTS
        An ordered dictionary of project name to project record.
    .NOTES
        Throws when two scanned projects share a file name; the error names both files. Remediation:
        rename one project, or exclude the folder holding the duplicate (for example sample or fixture
        projects that are test inputs) from the scan in the repository's build.ps1.
    #>
    param([string[]]$Root = @('src', 'test', 'tools'))

    $graph = [ordered]@{}
    $files = Get-ChildItem -Path $Root -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' } |
        Sort-Object FullName
    foreach ($file in $files) {
        $path = [System.IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName) -replace '\\', '/'
        $directory = Split-Path $path -Parent
        $directory = $directory -replace '\\', '/'
        [xml]$xml = Get-Content -Raw $file.FullName

        $references = @()
        $links = @()
        foreach ($item in $xml.SelectNodes('/Project/ItemGroup/*')) {
            $include = $item.GetAttribute('Include')
            if ($item.LocalName -eq 'ProjectReference' -and $include) {
                $references += [System.IO.Path]::GetFileNameWithoutExtension(($include -replace '\\', '/'))
                continue
            }
            # Items that name no file (packages, namespaces, assembly metadata) cannot link a file in
            if ($item.LocalName -in $script:NonFileItems) {
                # An assembly reference by file: <Reference Include="X"><HintPath>..\lib\X.dll</HintPath></Reference>
                if ($item.LocalName -eq 'Reference') { $links += Get-LinkedFilePattern $directory (Get-ItemMetadata $item 'HintPath') }
                continue
            }

            # Files only packed into the NuGet package (README.md, Icon.png, licenses) do not affect tests;
            # only Always, PreserveNewest, and IfDifferent copy a file to the output ('Never' does not)
            $pack = Get-ItemMetadata $item 'Pack'
            $copy = (Get-ItemMetadata $item 'CopyToOutputDirectory') -in @('Always', 'PreserveNewest', 'IfDifferent')
            if ($item.LocalName -eq 'None' -and $pack -eq 'true' -and -not $copy) { continue }

            foreach ($spec in @($include, $item.GetAttribute('Update'))) {
                foreach ($part in $spec -split ';') {
                    $links += Get-LinkedFilePattern $directory $part
                }
            }
        }

        # File-valued properties (an icon or manifest above the project folder) link a file in just like an item
        foreach ($property in $xml.SelectNodes('/Project/PropertyGroup/*')) {
            if ($property.LocalName -in $script:FileProperties) {
                $links += Get-LinkedFilePattern $directory $property.InnerText
            }
        }

        # Project names are the file names without extension (ProjectReference items are matched by that name),
        # so two projects with the same name would silently overwrite each other and hide one from the scoped
        # build. Fail clearly instead; a repository with intentional duplicates (for example sample projects
        # used as test fixtures) passes a narrower -Root.
        $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ($graph.Contains($name)) {
            throw "Duplicate project name '$name': '$($graph[$name].Path)' and '$path'. Project names must be unique " +
                "(rename one) or the folder holding the duplicate must be left out of the scanned -Root."
        }
        $graph[$name] = [pscustomobject]@{
            Name       = $name
            Path       = $path
            Directory  = $directory
            References = $references
            Links      = $links
        }
    }
    return $graph
}

function Get-RootProjectPrefix {
    <#
    .SYNOPSIS
        Returns the longest common dotted prefix of the library projects (the product's root name).
    .DESCRIPTION
        Takes the projects under src/ (every project when there are none) and keeps the leading name
        segments they all share: DemaConsulting.Foo, DemaConsulting.Foo.Core, DemaConsulting.Foo.Cli
        give DemaConsulting.Foo. Projects whose name has no dot (a helper tool under src/) are ignored
        unless that leaves nothing. Returns an empty string when the names share no leading segment.
    .PARAMETER Graph
        Project graph from Get-ProjectGraph.
    #>
    param($Graph)

    $names = @($Graph.Keys | Where-Object { $Graph[$_].Directory -like 'src/*' })
    if ($names.Count -eq 0) { $names = @($Graph.Keys) }
    $dotted = @($names | Where-Object { $_.Contains('.') })
    if ($dotted.Count -gt 0) { $names = $dotted }

    $prefix = $null
    foreach ($name in $names) {
        $segments = $name -split '\.'
        if ($null -eq $prefix) { $prefix = $segments; continue }
        $length = 0
        while ($length -lt $prefix.Count -and $length -lt $segments.Count -and $prefix[$length] -eq $segments[$length]) { $length++ }
        $prefix = @($prefix | Select-Object -First $length)
    }
    return ($prefix -join '.')
}

function Resolve-ProjectName {
    <#
    .SYNOPSIS
        Resolves a full or short project name to the name used in the project graph.
    .DESCRIPTION
        Lets callers type '-Project Pdf' or '-Project Tool.Tests'. The name is resolved in this order:
          1. an exact project name (case-insensitive);
          2. a caller-supplied alias (-Aliases), whose target is itself resolved by the other rules;
          3. a unique suffix match on dot-separated segments: 'Pdf' matches X.Y.Pdf, 'Pdf.Rendering'
             matches X.Y.Pdf.Rendering, 'Tool.Tests' matches X.Y.Tool.Tests, but 'df' matches nothing.
             When several projects match, the one named <root prefix>.<name> wins (the short name relative
             to the product's root name, see Get-RootProjectPrefix); otherwise the name is ambiguous;
          4. 'Core', when no project ends in '.Core': the project equal to the root prefix.
        Returns $null when nothing matches. Throws an error that lists the candidates when the name is
        ambiguous, and when an alias points at a project that does not exist.
    .PARAMETER Graph
        Project graph from Get-ProjectGraph.
    .PARAMETER Name
        The name as typed by the caller.
    .PARAMETER Aliases
        Optional map of alias to project name for projects whose names no rule can derive, for example
        @{ Ots = 'OtsSoftwareTests'; App = 'DemaConsulting.Foo' }. Kept as data in the repository's build.ps1.
    #>
    param($Graph, [string]$Name, [System.Collections.IDictionary]$Aliases = @{})

    if (-not $Name -or -not $Name.Trim()) { return $null }
    $Name = $Name.Trim()
    $keys = @($Graph.Keys)

    # 1. Exact name
    $exact = $keys | Where-Object { $_ -eq $Name } | Select-Object -First 1
    if ($exact) { return $exact }

    # 2. Alias (resolved without aliases, so an alias cannot loop)
    $aliasKey = if ($Aliases) { @($Aliases.Keys) | Where-Object { $_ -ieq $Name } | Select-Object -First 1 }
    if ($aliasKey) {
        $target = Resolve-ProjectName $Graph ([string]$Aliases[$aliasKey])
        if (-not $target) {
            throw "Alias '$aliasKey' points at '$($Aliases[$aliasKey])', which is not a project. Known projects: $($keys -join ', ')"
        }
        return $target
    }

    # 3. Suffix match on whole dot-separated segments
    $found = @($keys | Where-Object { $_.EndsWith(".$Name", [System.StringComparison]::OrdinalIgnoreCase) })
    $prefix = Get-RootProjectPrefix $Graph
    if ($found.Count -gt 1 -and $prefix) {
        $qualified = @($found | Where-Object { $_ -eq "$prefix.$Name" })
        if ($qualified.Count -eq 1) { $found = $qualified }
    }
    if ($found.Count -gt 1) {
        throw "Ambiguous project name '$Name': it matches $($found -join ', '). Use a longer name."
    }
    if ($found.Count -eq 1) { return $found[0] }

    # 4. 'Core' with no .Core project is the project that carries the root name itself
    if ($Name -eq 'Core' -and $prefix) {
        return $keys | Where-Object { $_ -eq $prefix } | Select-Object -First 1
    }
    return $null
}

function Get-ProjectNameList {
    <#
    .SYNOPSIS
        Lists the names a caller may pass to -Project: the projects, the aliases, and 'Core'.
    .DESCRIPTION
        For -Help text and 'unknown project' messages. 'Core' is listed when it resolves (and is not
        already a project name or alias). Short suffixes such as 'Pdf' also work but are not listed one
        by one; the full names already show them.
    .PARAMETER Graph
        Project graph from Get-ProjectGraph.
    .PARAMETER Aliases
        The same alias map passed to Resolve-ProjectName.
    #>
    param($Graph, [System.Collections.IDictionary]$Aliases = @{})

    $list = [System.Collections.Generic.List[string]]::new()
    foreach ($name in @($Graph.Keys) + @(if ($Aliases) { $Aliases.Keys })) {
        if ($name -and -not $list.Contains($name)) { $list.Add($name) }
    }
    $core = try { Resolve-ProjectName $Graph 'Core' $Aliases } catch { $null }
    if ($core -and -not $list.Contains('Core')) { $list.Add('Core') }
    return $list.ToArray()
}

function Test-BuildConfigFile {
    <#
    .SYNOPSIS
        Tests whether a file is a solution, project, or build configuration file.
    .DESCRIPTION
        The single definition of the files that can change the build or formatting result of every
        project: solution and project files, props/targets, Directory.Build.* and Directory.Packages.props,
        the SDK pin, package sources, and formatting configuration. Test-BuildWideFile (full build) and
        Get-DotnetFormatScope (whole-solution dotnet format) both use it, so they cannot drift apart.
    .PARAMETER File
        Repository-relative file path with forward slashes.
    #>
    param([string]$File)

    $leaf = Split-Path $File -Leaf
    return ($leaf -match '^(Directory\.Build\.(props|targets|rsp)|Directory\.Packages\.props|global\.json|nuget\.config|\.editorconfig|\.globalconfig)$') -or
        ($File -match '\.(sln|slnx|slnf|csproj|props|targets)$')
}

function Test-BuildWideFile {
    <#
    .SYNOPSIS
        Tests whether a changed file affects the whole build.
    .DESCRIPTION
        Build configuration files (see Test-BuildConfigFile), the tool manifest, and the project scripts
        themselves can change the result for every project, so any of them changing forces the full
        build instead of a scoped one.
    .PARAMETER File
        Repository-relative file path with forward slashes.
    #>
    param([string]$File)

    return (Test-BuildConfigFile $File) -or
        ($File -in @('.config/dotnet-tools.json', 'build.ps1', 'scripts/ProjectScripts.psm1'))
}

function Add-ChangedProjects {
    <#
    .SYNOPSIS
        Adds the projects that own, link, or read the changed files to a selection.
    .DESCRIPTION
        Maps each changed file to its owning project (folder prefix) and to every project that links it
        in. Files in the RuntimeReads map (files that tests read at run time through the repository root,
        not through a project item) select their test project. Returns the reason for a full build when
        a changed file affects the whole build, otherwise $null.
    .PARAMETER Graph
        Project graph from Get-ProjectGraph.
    .PARAMETER Files
        Changed files from Get-ChangedFiles.
    .PARAMETER Selected
        HashSet of project names that receives the selected projects.
    .PARAMETER RuntimeReads
        Dictionary of file regular expression to test project name; repository-specific, kept in build.ps1.
    #>
    param(
        $Graph,
        [string[]]$Files,
        [System.Collections.Generic.HashSet[string]]$Selected,
        [System.Collections.IDictionary]$RuntimeReads = @{}
    )

    foreach ($file in $Files) {
        if (Test-BuildWideFile $file) { return "$file affects the whole build" }
        foreach ($pattern in $RuntimeReads.Keys) {
            if ($file -match $pattern -and $Graph.Contains($RuntimeReads[$pattern])) { [void]$Selected.Add($RuntimeReads[$pattern]) }
        }
        foreach ($entry in $Graph.Values) {
            $owned = $file.StartsWith("$($entry.Directory)/", [System.StringComparison]::OrdinalIgnoreCase)
            $linked = @($entry.Links | Where-Object { $file -match $_ }).Count -gt 0
            if ($owned -or $linked) { [void]$Selected.Add($entry.Name) }
        }
    }
    return $null
}

function Expand-ReferencingProjects {
    <#
    .SYNOPSIS
        Adds every project that references a selected project, directly or transitively.
    .DESCRIPTION
        A change to a library can break everything that depends on it, so the selection is closed over
        the ProjectReference graph (breadth-first, each project added once).
    .PARAMETER Graph
        Project graph from Get-ProjectGraph.
    .PARAMETER Selected
        HashSet of project names, extended in place.
    #>
    param($Graph, [System.Collections.Generic.HashSet[string]]$Selected)

    $queue = [System.Collections.Generic.Queue[string]]::new([string[]]@($Selected))
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        foreach ($entry in $Graph.Values) {
            if ($entry.References -contains $current -and $Selected.Add($entry.Name)) { $queue.Enqueue($entry.Name) }
        }
    }
}

Export-ModuleMember -Function @(
    'Write-ScriptUsage', 'Test-ScopeArguments', 'Resolve-NameList',
    'Get-ChangedFiles', 'Test-AnyMatch', 'Convert-GlobToRegex', 'Get-ReviewMarkPattern', 'Get-DotnetFormatScope',
    'Get-VenvActivateScript', 'Initialize-PythonVenv', 'Convert-YamlLineEndings',
    'Resolve-RepoPath', 'Get-LinkedFilePattern', 'Get-ProjectGraph', 'Get-RootProjectPrefix', 'Resolve-ProjectName', 'Get-ProjectNameList',
    'Test-BuildConfigFile', 'Test-BuildWideFile',
    'Add-ChangedProjects', 'Expand-ReferencingProjects'
)
