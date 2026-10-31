#Requires -Version 5.1
<#
.SYNOPSIS
  Push Morph to GitHub (public) and deploy VitePress docs to Vercel.

.NOTES
  Prerequisites: gh auth login, vercel login
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

Write-Host "== GitHub ==" -ForegroundColor Cyan
$login = gh api user --jq .login
$repo = "$login/morph"
$exists = $false
try {
  gh repo view $repo 2>$null | Out-Null
  $exists = $true
} catch { $exists = $false }

if (-not $exists) {
  gh repo create morph --public --source=. --remote=origin `
    --description "Entity-driven Backend-as-a-Service (BaaS) — .NET 9 host, Access policies, SDKs, Dev Panel" `
    --push
} else {
  if (-not (git remote get-url origin 2>$null)) {
    git remote add origin "https://github.com/$repo.git"
  }
  git push -u origin main
}

gh repo edit $repo --homepage "https://morph-docs.vercel.app" --add-topic baas,dotnet,opensource,entity-driven 2>$null
Write-Host "Repo: https://github.com/$repo"

Write-Host "== Vercel docs ==" -ForegroundColor Cyan
Set-Location "$root\apps\docs"
npx --yes vercel --prod --yes
Write-Host "Done. Set the GitHub repo website URL to your Vercel production domain if different."
