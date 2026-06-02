#Requires -Version 5
<#
  PreToolUse(Bash) guard for Claude Code.
  Reads the hook JSON from stdin, inspects tool_input.command, and exits 2 to
  BLOCK clearly dangerous commands. Exit 0 lets the command through.
  Fail-open: if the input can't be parsed, do not block.
#>
$ErrorActionPreference = 'Continue'

$raw = [Console]::In.ReadToEnd()
$cmd = ''
try {
    $json = $raw | ConvertFrom-Json
    $cmd = [string]$json.tool_input.command
}
catch {
    exit 0
}

if ([string]::IsNullOrWhiteSpace($cmd)) { exit 0 }

# Dangerous patterns (regex, case-insensitive).
$patterns = @(
    'rm\s+-rf',                 # recursive force delete
    '>\s*\.env(\s|$)',          # overwrite .env (secrets)
    '>>\s*\.env(\s|$)',         # append to .env
    'git\s+push\s+.*--force(?!-with-lease)', # force push (allow the safe --force-with-lease)
    'git\s+push\s+.*\s-f(\s|$)',# force push (short flag)
    '--no-verify'               # bypass hooks
)

foreach ($p in $patterns) {
    if ($cmd -imatch $p) {
        [Console]::Error.WriteLine("guard-bash: BLOCKED dangerous command (matched /$p/): $cmd")
        exit 2
    }
}

exit 0
