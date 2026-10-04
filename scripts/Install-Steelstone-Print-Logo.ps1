# Copy your Steelstone logo JPG into the backend print folder (local + git deploy to server).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$destDir = Join-Path $root "ConstFire.Backend\Data\Print"
$destFile = Join-Path $destDir "steelstone-logo-blue.jpg"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

$source = $args[0]
if ([string]::IsNullOrWhiteSpace($source)) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
    $dialog.Title = "Select Steelstone logo for PDF header"
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        Write-Host "Cancelled."
        exit 1
    }
    $source = $dialog.FileName
}

if (-not (Test-Path $source)) { throw "File not found: $source" }
Copy-Item -Path $source -Destination $destFile -Force
Write-Host "Logo installed:"
Write-Host "  $destFile"
Write-Host ""
Write-Host "Next: git add ConstFire.Backend/Data/Print/steelstone-logo-blue.jpg"
Write-Host "      git commit -m ""Add Steelstone PDF header logo"""
Write-Host "      git push origin main   (pipeline will upload to SmarterASP server)"
