param([string[]]$Commands, [int]$TimeoutSec = 180)
# Sends commands to the open Unity editor (see Assets/MicroduckVR/Editor/Tooling/RemoteCommands.cs) and prints its log until idle.
#   powershell -ExecutionPolicy Bypass -File Tools\editor-cmd.ps1 -Commands "refresh","method Ns.Class.Method"
$Commands = (($Commands -join '|') -split '\|') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$root = Split-Path -Parent $PSScriptRoot
$log = Join-Path $root "Logs\microduck_cmd.log"
$before = if (Test-Path $log) { (Get-Content $log).Count } else { 0 }
[IO.File]::WriteAllLines((Join-Path $root "Temp\microduck_cmd.txt"), @("#id " + [guid]::NewGuid().ToString()) + $Commands)
$deadline = (Get-Date).AddSeconds($TimeoutSec); $lastCount = -1; $stable = 0
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Milliseconds 1500
  $lines = if (Test-Path $log) { Get-Content $log } else { @() }
  $new = $lines | Select-Object -Skip $before
  $pending = Test-Path (Join-Path $root "Temp\microduck_cmd.txt")
  $busy = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -match 'microduck' } | ForEach-Object { $_.CPU }
  if (-not $pending -and $new.Count -eq $lastCount) { $stable++ } else { $stable = 0 }
  $lastCount = $new.Count
  if (-not $pending -and $stable -ge 3 -and ($new -match '(^|\s)(ok|FAILED|compile finished|unknown|not found)')) { break }
}
$new | Where-Object { $_.Trim() -ne '' }
