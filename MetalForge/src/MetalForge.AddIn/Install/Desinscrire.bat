@echo off
REM Desinscrit le complement MetalForge (SolidWorks ferme, en tant qu'administrateur).
set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set DLL=%~dp0MetalForge.AddIn.dll
if not exist "%DLL%" set DLL=%~dp0..\MetalForge.AddIn.dll
"%REGASM%" /u "%DLL%"
pause
