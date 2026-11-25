param([string]$Method = "", [string]$Log = "Logs\batch.log")
# Runs Unity 6000.6.7f1 in batch mode on this project, waits, and prints compile errors and Microduck logs.
#   powershell -ExecutionPolicy Bypass -File Tools\unity-batch.ps1 [-Method Namespace.Class.Method]
$root = Split-Path -Parent $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
$logPath = Join-Path $root $Log
New-Item -ItemType Directory -Force -Path (Split-Path $logPath) | Out-Null
$argList = @('-batchmode', '-nographics', '-projectPath', "`"$root`"", '-logFile', "`"$logPath`"")
if ($Method) { $argList += @('-executeMethod', $Method) }
$argList += '-quit'
$p = Start-Process -FilePath $unity -ArgumentList $argList -Wait -PassThru
"exit code: " + $p.ExitCode
Select-String -Path $logPath -Pattern 'error CS|\[Microduck\]|Exception|Scripts have compiler errors|executeMethod' | Select-Object -First 60 | ForEach-Object { $_.Line.Trim() }
