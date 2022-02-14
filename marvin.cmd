@echo off
cd "build"
dotnet run -- %*
set exitcode=%errorlevel%
cd %~dp0
exit /b %exitcode%