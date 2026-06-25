# OMG SysML v2 Models

This folder contains SysML v2 model files from the OMG reference implementation
repository, used as ground-truth test inputs for the SysML2Tools parser.

## Source

- **Repository**: [Systems-Modeling/SysML-v2-Release](https://github.com/Systems-Modeling/SysML-v2-Release)
- **Tag**: `2026-04`
- **Subtree**: `sysml/src/` (examples, training, and validation models only;
  the standard library is embedded separately in `src/DemaConsulting.SysML2Tools/Stdlib/`)

## License

Copyright © 2019–2024 Model Driven Solutions, Inc. and other contributors.
Distributed under the [Eclipse Public License 2.0 (EPL-2.0)](https://www.eclipse.org/legal/epl-2.0/).

## Folder and File Name Transformations

The upstream repository uses spaces in folder and file names. All spaces have
been removed using the following rule:

> A space followed by an alphanumeric character is removed and that character
> is uppercased (CamelCase join). Any remaining spaces (e.g., before a hyphen)
> are then stripped.

Examples:

| Upstream name | Stored as |
| --- | --- |
| `Vehicle Example/` | `VehicleExample/` |
| `Simple Tests/` | `SimpleTests/` |
| `1a-Parts Tree.sysml` | `1a-PartsTree.sysml` |
| `SysML v2 Spec Annex A SimpleVehicleModel.sysml` | `SysMLV2SpecAnnexASimpleVehicleModel.sysml` |
| `7a-Variant Configuration - General Concept.sysml` | `7a-VariantConfiguration-GeneralConcept.sysml` |
| `15_07-System of Units and Scales.sysml` | `15_07-SystemOfUnitsAndScales.sysml` |

File contents are unchanged.

## Regenerating

To update to a newer OMG release tag, run from the repository root:

```pwsh
$tag     = '2026-04'   # update to new tag
$outRoot = 'test\SysMLModels\OMG'

function Convert-SpacesToCamel($name) {
    $r = [regex]::Replace($name, ' ([a-zA-Z0-9])', { $args[0].Groups[1].Value.ToUpper() })
    $r.Replace(' ', '')
}

$baseRaw = "https://raw.githubusercontent.com/Systems-Modeling/SysML-v2-Release/$tag"
$resp = Invoke-RestMethod "https://api.github.com/repos/Systems-Modeling/SysML-v2-Release/git/trees/$tag`?recursive=1"
$files = $resp.tree | Where-Object { $_.path -like 'sysml/src/*' -and $_.path -like '*.sysml' }

Remove-Item $outRoot\examples, $outRoot\training, $outRoot\validation -Recurse -Force -ErrorAction SilentlyContinue

foreach ($file in $files) {
    $relPath  = $file.path.Substring('sysml/src/'.Length)
    $newParts = ($relPath -split '/') | ForEach-Object { Convert-SpacesToCamel $_ }
    $dest     = Join-Path $outRoot ($newParts -join '\')
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
    Invoke-WebRequest -Uri "$baseRaw/$($file.path.Replace(' ','%20'))" -OutFile $dest -UseBasicParsing
}
```

Update the **Tag** entry above and commit the result.
