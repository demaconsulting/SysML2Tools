<#
.SYNOPSIS
    Draws the README hero banner, docs/images/hero.png, from docs/images/hero.svg, or checks that it is current.

.DESCRIPTION
    hero.svg is the 1600x640 README banner, kept beside the hero.png it draws (matching base names). The files it
    embeds are in hero/, such as hero/dc-logo-dark.svg, the DEMA Consulting "DC" monogram redrawn as vector paths
    in colors for a dark background. They follow the shared DEMA Consulting hero of
    CanvasNet and BuildMark. The banner is drawn with "dotnet canvasnet" from the tool manifest.

    Without -Check it draws the banner over docs/images/hero.png. Commit the changed hero.png with the change to
    the SVG that made it differ.

    With -Check it draws the banner into a temporary folder and compares it with the committed hero.png using
    "canvasnet compare ... --threshold rmse=1 --threshold diff-ratio=0.001". It changes nothing in the repository,
    and exits 1 when the committed PNG is missing, has another size, or differs by more than the tolerance. The
    tolerance is not zero on purpose: a drawing is not bit-for-bit the same on every operating system (a font or
    a last-bit difference in floating point may move a few anti-aliased pixels), but any change of text, color,
    or layout is far above it.

    Run it from anywhere; paths are taken from the repository the script is in.

.PARAMETER Check
    Compare a fresh drawing with the committed hero.png instead of replacing it.
#>
[CmdletBinding()]
param(
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = 'docs/images/hero.svg'
$committed = 'docs/images/hero.png'

Push-Location $repo
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE"
    }

    if (-not $Check) {
        dotnet canvasnet render $source --strict --width 1600 -o $committed
        if ($LASTEXITCODE -ne 0) {
            throw "canvasnet render failed with exit code $LASTEXITCODE"
        }
        exit 0
    }

    if (-not (Test-Path -LiteralPath $committed)) {
        Write-Host "FAIL  ${committed}: the committed image is missing (run docs/images/render-hero.ps1)"
        exit 1
    }

    $work = Join-Path ([System.IO.Path]::GetTempPath()) ('render-hero-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    $fresh = Join-Path $work 'hero.png'
    $failed = $false
    try {
        dotnet canvasnet render $source --strict --width 1600 -o $fresh | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "canvasnet render failed with exit code $LASTEXITCODE"
        }

        $json = dotnet canvasnet compare $committed $fresh --threshold 'rmse=1' --threshold 'diff-ratio=0.001' `
            --side-by-side (Join-Path $work 'compare.png') --json 2>$null
        $exit = $LASTEXITCODE
        $result = $null
        try { $result = ($json | Out-String) | ConvertFrom-Json } catch { $result = $null }

        if ($exit -eq 0) {
            Write-Host ("ok    {0}: rmse {1:N3}, differing pixels {2:P3}" -f `
                    $committed, $result.metrics.rmse, $result.metrics.diffPixelRatio)
            Write-Host 'The committed hero image is current.'
        }
        else {
            $failed = $true
            $reason = if ($result -and $result.PSObject.Properties['error'] -and $result.error) {
                $result.error.message
            }
            else {
                "it differs from a fresh drawing of $source (canvasnet compare exited with code $exit)"
            }
            Write-Host "FAIL  ${committed}: $reason"
            if ($result -and $result.PSObject.Properties['metrics']) {
                Write-Host ("      rmse {0:N3}, differing pixels {1:P3}" -f `
                        $result.metrics.rmse, $result.metrics.diffPixelRatio)
            }
            Write-Host '      If the change is intended, run: pwsh docs/images/render-hero.ps1 and commit the image.'
            Write-Host "      The fresh image and a side-by-side comparison are kept in $work"
        }
    }
    finally {
        if (-not $failed) {
            Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    exit ($failed ? 1 : 0)
}
finally {
    Pop-Location
}
