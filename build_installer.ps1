# Ensure Velopack CLI tool is installed
Write-Host "Installing Velopack CLI..."
dotnet tool update -g vpk

$publishDir = "$PSScriptRoot\publish"
$releasesDir = "$PSScriptRoot\Releases"

# Clean previous publish
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (!(Test-Path $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir | Out-Null }

Write-Host "Publishing WinRadial..."
# We explicitly set SelfContained to false so the binaries are tiny.
# Velopack will handle installing the .NET runtime on the user's PC!
dotnet publish "$PSScriptRoot\src\WinRadial\WinRadial.csproj" -c Release -r win-x64 --self-contained false -p:EnableCompressionInSingleFile=false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed."
    pause
    exit 1
}

Write-Host "Packaging with Velopack..."
# Pack into a Setup.exe that automatically requires and installs .NET 8 Desktop Runtime
vpk pack -u WinRadial -v 1.0.0 -p $publishDir -e WinRadial.exe -o $releasesDir -f net8.0-x64-desktop

Write-Host "----------------------------------------------------"
Write-Host "Success! The installer has been generated."
Write-Host "You can find it at: $releasesDir\WinRadial-Setup-1.0.0.exe"
Write-Host "----------------------------------------------------"
pause
