@echo off
setlocal

dotnet run --project "%~dp0src\McpToolName.Mcp" -- %*
exit /b %ERRORLEVEL%
