@echo off
setlocal

dotnet run --project "%~dp0src\Kusto.Cli.Mcp" -- %*
exit /b %ERRORLEVEL%
