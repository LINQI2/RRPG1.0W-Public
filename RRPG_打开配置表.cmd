@echo off
rem ============================================================================
rem  RRPG Static Configuration - Producer entry 1/3 : Open authoring folder
rem  (Wave 0 V0.1 = Migration V0.1 Implementation Choice, NOT a permanent UI
rem   Authority; may be replaced by an Editor Window later.)
rem  ASCII-only on purpose: Chinese guidance is printed by the tools themselves.
rem ============================================================================
chcp 65001 >nul
setlocal
set "REPO=%~dp0"
set "CFG=%REPO%Development\Config"

if not exist "%CFG%" mkdir "%CFG%"

echo.
echo === RRPG Static Configuration : authoring folder ===
echo.
echo   Folder : Development\Config
echo   Rule   : XLSX is the ONLY human authoring authority.
echo            Do not edit the generated mirror or any generated asset.
echo.
if exist "%CFG%\*.xlsx" (
  echo   Status : authoring workbooks found.
) else (
  echo   Status : Production Toolchain Ready / no migrated domain yet.
  echo            ^(The compiler entry will report the same state.^)
)
echo.
start "" "%CFG%"
exit /b 0
