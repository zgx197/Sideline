@echo off
chcp 65001 >nul
title Lattice 测试框架

echo.
echo ========================================
echo   Lattice 测试快速运行工具
echo ========================================
echo.

:: 如果没有参数，显示帮助
if "%~1"=="" goto :HELP

:: 解析参数
if /I "%~1"=="quick" goto :QUICK
if /I "%~1"=="all" goto :ALL
if /I "%~1"=="detail" goto :DETAIL
if /I "%~1"=="list" goto :LIST
if /I "%~1"=="math" goto :MATH
if /I "%~1"=="det" goto :DET
if /I "%~1"=="rob" goto :ROB
if /I "%~1"=="cov" goto :COV
if /I "%~1"=="help" goto :HELP
if /I "%~1"=="-h" goto :HELP
if /I "%~1"=="/?" goto :HELP

echo 未知命令: %~1
goto :HELP

:QUICK
echo [快速测试] 运行所有测试（简要输出）...
dotnet test --verbosity minimal
goto :END

:ALL
echo [完整测试] 运行所有测试（详细输出）...
dotnet test --verbosity normal
goto :END

:DETAIL
echo [详细测试] 运行所有测试（完整输出）...
dotnet test --verbosity detailed
goto :END

:LIST
echo [列出测试] 所有可用测试...
dotnet test --list-tests
goto :END

:MATH
echo [数学测试] 仅运行 FP 数学测试...
dotnet test --filter "FullyQualifiedName~Math" --verbosity normal
goto :END

:DET
echo [确定性测试] 仅运行确定性测试...
dotnet test --filter "FullyQualifiedName~Determinism" --verbosity normal
goto :END

:ROB
echo [健壮性测试] 仅运行健壮性测试...
dotnet test --filter "FullyQualifiedName~Robustness" --verbosity normal
goto :END

:COV
echo [覆盖率] 生成测试覆盖率报告...
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
goto :END

:HELP
echo 用法: test.bat [命令]
echo.
echo 可用命令:
echo   quick   - 快速运行所有测试（默认）
echo   all     - 完整运行并显示详细输出
echo   detail  - 超详细输出（用于调试）
echo   list    - 列出所有测试用例
echo   math    - 仅运行数学测试
echo   det     - 仅运行确定性测试
echo   rob     - 仅运行健壮性测试
echo   cov     - 生成覆盖率报告
echo   help    - 显示此帮助
echo.
echo 示例:
echo   test.bat quick
echo   test.bat det
echo   test.bat rob
goto :END

:END
echo.
if %ERRORLEVEL% == 0 (
    echo [✓] 测试通过！
) else (
    echo [✗] 测试失败！
)
echo.
pause
