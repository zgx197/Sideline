@echo off
:: Lattice LUT 生成脚本 (Windows)
:: 用法: generate-luts.bat [输出目录]

setlocal EnableDelayedExpansion

:: 确定脚本所在目录
set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%\..\..\..\.."

:: 默认输出目录
if "%~1"=="" (
    set "OUTPUT_DIR=%PROJECT_ROOT%\godot\scripts\lattice\Math\Generated"
) else (
    set "OUTPUT_DIR=%~1"
)

echo ==========================================
echo Lattice LUT Generator
echo ==========================================
echo Output: %OUTPUT_DIR%
echo.

:: 检查 dotnet
where dotnet >nul 2>nul
if errorlevel 1 (
    echo Error: dotnet CLI not found in PATH
    exit /b 1
)

:: 运行生成器
cd /d "%SCRIPT_DIR%\LutGenerator"
dotnet run -- "%OUTPUT_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: LUT generation failed!
    exit /b 1
)

echo.
echo ==========================================
echo LUT generation completed successfully!
echo ==========================================

endlocal
