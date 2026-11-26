@echo off
setlocal
cd /d "%~dp0"

rem Builds bin\StudyMonitor.exe with the C# compiler that comes with Windows
rem (part of .NET Framework 4, built into Windows 10 and 11). Nothing to install.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Couldn't find the C# compiler that comes with Windows.
  echo It is part of .NET Framework 4.8, which Windows 10 and 11 include.
  goto fail
)

tasklist /fi "imagename eq StudyMonitor.exe" 2>nul | find /i "StudyMonitor.exe" >nul
if not errorlevel 1 (
  echo Study Monitor is running. Quit it first: right-click its tray icon, then Quit.
  goto fail
)

if not exist bin mkdir bin
set "ICON="
if exist app.ico set "ICON=/win32icon:app.ico"

"%CSC%" /nologo /target:winexe /optimize+ /out:bin\StudyMonitor.exe %ICON% /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs
if errorlevel 1 goto fail

echo.
echo Built bin\StudyMonitor.exe
echo Double-click it to start. You can move the bin folder anywhere you like.
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
echo.
echo Build failed.
if /i not "%~1"=="nopause" pause
exit /b 1
