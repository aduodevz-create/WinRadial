@echo off
:: WinRadial Launcher — runs the app using the locally installed .NET SDK
:: No PATH configuration needed.

set DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe

if not exist "%DOTNET%" (
    echo ERROR: .NET SDK not found at %DOTNET%
    echo Please install it: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Starting WinRadial...
"%DOTNET%" run --project "%~dp0src\WinRadial"
