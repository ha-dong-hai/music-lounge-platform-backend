<#
Regenerates the "backend-only" branch (an orphan, generated branch - never commit into it
directly) so it mirrors just the backend-relevant paths of $SourceRef. Each run produces at
most one new commit; nothing is force-pushed or rewritten.
#>
param(
    [string]$SourceRef = "master",
    [string]$BranchName = "backend-only",
    [switch]$Push
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Whitelist = @(
    "src",
    "tests",
    "services/panorama-stitcher",
    "MusicLounge.sln",
    "Directory.Build.props",
    "README-SETUP.md",
    ".github/workflows/ci.yml",
    ".gitignore"
)

function Invoke-Git {
    # Do not redirect stderr with 2>&1: in PowerShell 5.1 that wraps each stderr line from a
    # native exe in a terminating ErrorRecord even on exit code 0 (git writes routine progress
    # output, e.g. from "worktree add", to stderr). Let stderr pass through to the console as-is
    # and rely on $LASTEXITCODE for success/failure.
    param([string[]]$GitArgs, [string]$WorkDir = $RepoRoot)
    $result = & git -C $WorkDir @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE (see output above)"
    }
    return $result
}

# The export reads $SourceRef from git's object database (not the working tree), so uncommitted
# local changes never leak into it - but warn anyway in case the caller expected them included.
$dirty = Invoke-Git @("status", "--porcelain")
if ($dirty) {
    Write-Host "Note: working tree has uncommitted changes; they will NOT be part of this export (only the committed $SourceRef is used)."
}

$SourceSha = (Invoke-Git @("rev-parse", "--short", $SourceRef)) | Select-Object -First 1

$WorktreePath = "$RepoRoot-worktree-$BranchName"
if (Test-Path $WorktreePath) {
    throw "Worktree path already exists: $WorktreePath (remove it or clean up a previous failed run with 'git worktree remove')"
}

$localBranchExists = $(& git -C $RepoRoot show-ref --verify --quiet "refs/heads/$BranchName"; $LASTEXITCODE -eq 0)
$remoteBranchExists = $(& git -C $RepoRoot show-ref --verify --quiet "refs/remotes/origin/$BranchName"; $LASTEXITCODE -eq 0)

try {
    if ($localBranchExists) {
        Invoke-Git @("worktree", "add", $WorktreePath, $BranchName)
    }
    elseif ($remoteBranchExists) {
        Invoke-Git @("worktree", "add", $WorktreePath, "-b", $BranchName, "origin/$BranchName")
    }
    else {
        # --orphan gives an empty working tree with no checkout of $SourceRef at all. That
        # matters on Windows: a plain detached checkout of $SourceRef here would materialize
        # fontend/'s deeply-nested Stitch export folders and blow past MAX_PATH, even though
        # none of that content is in the whitelist we're about to check out below.
        Invoke-Git @("worktree", "add", "--orphan", "-b", $BranchName, $WorktreePath)
    }

    # Wipe and re-checkout the whitelist from scratch every run (rather than an incremental
    # "git checkout $SourceRef -- <path>") because that command never deletes files that were
    # removed upstream - only a full wipe reliably mirrors deletions.
    $tracked = Invoke-Git @("ls-files") -WorkDir $WorktreePath
    if ($tracked) {
        Invoke-Git @("rm", "-rf", "--quiet", ".") -WorkDir $WorktreePath
    }

    foreach ($path in $Whitelist) {
        $exists = $(& git -C $RepoRoot cat-file -e "${SourceRef}:${path}" 2>$null; $LASTEXITCODE -eq 0)
        if ($exists) {
            Invoke-Git @("checkout", $SourceRef, "--", $path) -WorkDir $WorktreePath
        }
    }

    Invoke-Git @("add", "-A") -WorkDir $WorktreePath

    $staged = Invoke-Git @("diff", "--cached", "--name-only") -WorkDir $WorktreePath
    if (-not $staged) {
        Write-Host "No changes since last export - nothing to commit."
    }
    else {
        $commitMessage = "Backend-only export from $SourceRef@$SourceSha (auto-generated, do not edit directly)"
        Invoke-Git @("commit", "--quiet", "-m", $commitMessage) -WorkDir $WorktreePath
        Write-Host "Committed: $commitMessage"

        if ($Push) {
            Invoke-Git @("push", "-u", "origin", $BranchName) -WorkDir $WorktreePath
            Write-Host "Pushed $BranchName to origin."
        }
        else {
            Write-Host "Dry run (no -Push): review the commit in $WorktreePath, then re-run with -Push to publish."
        }
    }
}
finally {
    if (-not $Push -and (Test-Path $WorktreePath)) {
        Write-Host "Worktree left in place for review: $WorktreePath"
        Write-Host "Remove it later with: git worktree remove `"$WorktreePath`""
    }
    elseif (Test-Path $WorktreePath) {
        Invoke-Git @("worktree", "remove", $WorktreePath)
    }
}
