@echo off
cd "marvin"
dotnet run -- %*
set exitcode=%errorlevel%
cd %~dp0
exit /b %exitcode%