@echo off
setlocal
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%app\TarkovBenchmarkWizard.ps1"
endlocal
