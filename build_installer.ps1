$publishDir = "$PSScriptRoot\publish"
$releasesDir = "$PSScriptRoot\Releases"

# Clean previous publish
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (!(Test-Path $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir | Out-Null }

Write-Host "Publishing WinRadial as a standalone portable executable..."
# We rely on the .csproj settings (PublishSingleFile=true, SelfContained=true, EnableCompressionInSingleFile=true)
dotnet publish "$PSScriptRoot\src\WinRadial\WinRadial.csproj" -c Release -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed."
    pause
    exit 1
}

Write-Host "Copying executable to Releases directory..."
Copy-Item "$publishDir\WinRadial.exe" -Destination "$releasesDir\WinRadial.exe" -Force

Write-Host "----------------------------------------------------"
Write-Host "Success! The portable executable has been generated."
Write-Host "You can find it at: $releasesDir\WinRadial.exe"
Write-Host "----------------------------------------------------"
pause
