@echo off
REM Automated test runner for WowClassicGrindBot
REM Usage: run-tests.bat [TestCategory] [Configuration]
REM
REM TestCategory options: All, E2E, MockWoWClient, PixelEncoding, InterfaceCompliance
REM Configuration options: Debug, Release

set CATEGORY=%1
if "%CATEGORY%"=="" set CATEGORY=All

set CONFIG=%2
if "%CONFIG%"=="" set CONFIG=Debug

echo.
echo ========================================
echo WowClassicGrindBot Test Harness (Batch)
echo ========================================
echo Category: %CATEGORY%
echo Configuration: %CONFIG%
echo.

REM Build solution
echo Building solution...
dotnet build MasterOfPuppets.sln -c %CONFIG%
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    exit /b 1
)
echo [SUCCESS] Build completed
echo.

REM Set test filter
set FILTER=
if "%CATEGORY%"=="E2E" set FILTER=--filter "FullyQualifiedName~EndToEnd"
if "%CATEGORY%"=="MockWoWClient" set FILTER=--filter "FullyQualifiedName~MockWoWClient"
if "%CATEGORY%"=="PixelEncoding" set FILTER=--filter "FullyQualifiedName~PixelEncoding"
if "%CATEGORY%"=="InterfaceCompliance" set FILTER=--filter "FullyQualifiedName~InterfaceCompliance"

REM Run tests
echo Running tests...
dotnet test CoreUnitTests\CoreUnitTests.csproj -c %CONFIG% --no-build %FILTER% -v q
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Tests failed!
    exit /b 1
)

echo.
echo ========================================
echo [SUCCESS] All tests passed!
echo ========================================
exit /b 0
