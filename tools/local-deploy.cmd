@echo off
REM StarPixelSurge Windows local deploy & verification (wrapper around local-deploy.ps1).
REM Usage:
REM   tools\local-deploy.cmd                 (default: all)
REM   tools\local-deploy.cmd build           (build runnable StarPixelSurge.exe)
REM   tools\local-deploy.cmd validate        (config database validation)
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=all
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-deploy.ps1" -Target %TARGET%
exit /b %ERRORLEVEL%
