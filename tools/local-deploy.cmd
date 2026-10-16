@echo off
REM StarPixelSurge Windows local deploy & verification (wrapper around local-deploy.ps1).
REM Usage: tools\local-deploy.cmd [all^|compile^|validate^|simulation^|udp]
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=all
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-deploy.ps1" -Target %TARGET%
exit /b %ERRORLEVEL%
