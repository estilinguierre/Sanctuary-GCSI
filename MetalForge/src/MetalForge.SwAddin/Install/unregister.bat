@echo off
REM Désinscrit le complément MetalForge (à lancer en tant qu'administrateur, SolidWorks fermé).
set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set DLL=%~dp0MetalForge.SwAddin.dll
if not exist "%DLL%" set DLL=%~dp0..\MetalForge.SwAddin.dll
"%REGASM%" /u "%DLL%"
pause
