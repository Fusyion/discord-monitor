@echo off
setlocal
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /out:DiscordMicMonitor.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  DiscordMicMonitor.cs
if errorlevel 1 (
  echo Build FAILED.
  exit /b 1
)
echo Built DiscordMicMonitor.exe
