@echo off
REM Enregistre le complément MetalForge dans SolidWorks (à lancer en tant qu'administrateur).
setlocal
set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set DLL=%~dp0MetalForge.SwAddin.dll
if not exist "%DLL%" set DLL=%~dp0..\MetalForge.SwAddin.dll
if not exist "%DLL%" (
  echo DLL introuvable : compilez d'abord la solution ^(Release x64^).
  pause & exit /b 1
)
"%REGASM%" /codebase "%DLL%"
if errorlevel 1 (
  echo Echec de l'enregistrement. Lancez ce fichier avec "Executer en tant qu'administrateur".
) else (
  echo MetalForge enregistre. Demarrez SolidWorks : Outils ^> Complements ^> MetalForge.
)
pause
