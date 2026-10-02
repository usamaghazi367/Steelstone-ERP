$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not (Test-Path node_modules)) { npm install }
node .\generate-all-module-configs.mjs
