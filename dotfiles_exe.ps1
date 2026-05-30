# @ai_gen(gemini 3 flash extended, https://gemini.google.com/u/2/app/97e369a4d6e2e003)
# this script searches for the native windows c# compiler caches its location and forwards all arguments directly to the compiled executable
$script_dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cache_file = Join-Path $script_dir ".csc_cache"
$cs_file = Join-Path $script_dir "dotfiles.cs"
$exe_file = Join-Path $script_dir "dotfiles.exe"

$compiler_path = $null

if (Test-Path -LiteralPath $cache_file) {
    $cached_path = Get-Content -LiteralPath $cache_file -Raw
    if ($cached_path -and (Test-Path -LiteralPath $cached_path.Trim())) {
        $compiler_path = $cached_path.Trim()
    } else {
        Remove-Item -LiteralPath $cache_file -Force
    }   
}

if (-not $compiler_path) {
    $search_paths = @(
        "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe",
        "$env:SystemRoot\Microsoft.NET\Framework64\v3.5\csc.exe",
        "$env:SystemRoot\Microsoft.NET\Framework\v2.0.50727\csc.exe"
    )
    foreach ($path in $search_paths) {
        if (Test-Path -LiteralPath $path) {
            $compiler_path = $path
            $compiler_path | Out-File -LiteralPath $cache_file -NoNewline -Force
            break
        }
    }
}

if (-not $compiler_path) {
    Write-Host "could not locate a valid csc.exe compiler path on this system"
    exit 1
}

$compile_output = & $compiler_path /out:$exe_file $cs_file 2>&1
$compile_status = $LastExitCode

if ($compile_status -ne 0) {
    foreach ($line in $compile_output) {
        $text = $line.ToString().Trim()
        if (-not $text) {
            continue
        }
        # filter out the verbose microsoft banner noise from the compiler output stream
        if ($text -match "^Microsoft \(R\)" -or $text -match "^for C#" -or $text -match "^Copyright" -or $text -match "^This compiler is provided") {
            continue
        }
        Write-Host $text
    }
    exit 1
}

& $exe_file $args