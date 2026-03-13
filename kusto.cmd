@echo off
setlocal

dotnet run --project "%~dp0src\Kusto.Cli" -- %*
exit /b %ERRORLEVEL%
