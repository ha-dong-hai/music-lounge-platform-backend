# Builds every diagram, then proves it.
#
# Two pipelines, chosen per diagram type:
#
#   gen_*.py    Fixed coordinates via dsl.py. Used for everything structural — the
#               context diagram, use cases, deployment, package, class and the ERDs.
#               An auto-layout engine is not constrained to keep an edge label off an
#               unrelated connector, so those diagrams are placed by hand and
#               dsl.validate() *proves* nothing overlaps before anything is written.
#               Emits both PNG and hand-editable .drawio.
#
#   src/*.puml  PlantUML. Kept only for sequence and activity diagrams, which it
#               lays out deterministically in columns and lanes, so the overlap problem
#               does not arise. Screen flows and state machines were moved off it after
#               it stacked two states so their captions printed over one another, and
#               routed a connector straight through a transition caption.
#
# Requires: Python with Pillow (fixed-coordinate pipeline), Java 17+ and Graphviz
# (PlantUML pipeline). All three are checked so a missing one fails loudly.
#
#   Usage:  pwsh -File diagrams/build.ps1            # build everything and validate
#           pwsh -File diagrams/build.ps1 -Svg       # also emit SVG from PlantUML

param([switch]$Svg)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$jar = 'J:\CI_CD\tools\plantuml.jar'
$graphvizBin = 'C:\Program Files\Graphviz\bin'

if (-not (Test-Path $jar)) { throw "PlantUML not found at $jar" }
if (-not (Test-Path (Join-Path $graphvizBin 'dot.exe'))) { throw "Graphviz 'dot' not found in $graphvizBin" }
if ($env:Path -notlike "*$graphvizBin*") { $env:Path = "$env:Path;$graphvizBin" }

$src = Join-Path $root 'src'
$out = Join-Path $root 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null

# ── 1. fixed-coordinate diagrams ────────────────────────────────────────────
Write-Host 'Drawing fixed-coordinate diagrams...'
$generators = @('gen_context.py', 'gen_architecture.py', 'gen_architecture_system.py', 'gen_usecases.py', 'gen_deployment.py',
                'gen_package.py', 'gen_class.py', 'gen_erd_core.py',
                'gen_erd_domains.py', 'gen_flows.py', 'gen_states.py')
foreach ($g in $generators) {
    & python (Join-Path $root $g)
    if ($LASTEXITCODE -ne 0) { throw "$g reported a geometry problem (exit $LASTEXITCODE)" }
}

# ── 2. PlantUML diagrams ────────────────────────────────────────────────────
# _style.puml is an include-only partial, never rendered on its own.
$files = Get-ChildItem -Path $src -Filter '*.puml' | Where-Object { $_.Name -ne '_style.puml' }
Write-Host ''
Write-Host "Rendering $($files.Count) PlantUML diagram(s)..."
Push-Location $src
try {
    $formats = @('-tpng')
    if ($Svg) { $formats += '-tsvg' }
    foreach ($fmt in $formats) {
        # PlantUML's default max render size is 4096px and it clips silently past
        # that — no error, no warning, just a cropped image.
        & java -DPLANTUML_LIMIT_SIZE=8192 -jar $jar $fmt -o $out @($files.FullName) 2>&1 |
            Where-Object { $_ -notmatch '^\s*$' } | ForEach-Object { Write-Host "  $_" }
        if ($LASTEXITCODE -ne 0) { throw "PlantUML failed for format $fmt (exit $LASTEXITCODE)" }
    }
}
finally { Pop-Location }

# ── 3. conformance gate ─────────────────────────────────────────────────────
# Rendering is not the same as being correct: validate.py re-checks every use case
# name, enum state and entity against the source of truth, and fails on the silent
# corruption modes. See STANDARDS.md for the full criteria.
Write-Host ''
Write-Host 'Validating...'
& python (Join-Path $root 'validate.py')
if ($LASTEXITCODE -ne 0) { throw "Diagram validation failed (exit $LASTEXITCODE)" }

Write-Host ''
Get-ChildItem $out -Filter '*.png' | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-34} {1,8:N0} bytes" -f $_.Name, $_.Length)
}
Write-Host "Done -> $out"
