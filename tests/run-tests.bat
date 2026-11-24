@echo off
setlocal
cd /d "%~dp0.."

rem Runs the logic tests: hours of simulated use with a fake clock, no audio.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist bin mkdir bin

"%CSC%" /nologo /target:exe /out:bin\StudyMonitorTests.exe src\Engine.cs src\History.cs src\Messages.cs src\DefaultMessages.cs src\Settings.cs src\Format.cs tests\Tests.cs
if errorlevel 1 goto fail
bin\StudyMonitorTests.exe
if errorlevel 1 goto fail
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
echo.
echo Tests failed.
if /i not "%~1"=="nopause" pause
exit /b 1
