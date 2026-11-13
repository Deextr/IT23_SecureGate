# Deploy SecureGate to MonsterASP from VS Code (Web Deploy / MSDeploy).
# Prereq: Web Deploy V3 — https://aka.ms/webdeploydownload
# MonsterASP: WebDeploy enabled on Websites → Deploy; .NET 10 InProcess on Scripting.

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$Csproj = Join-Path $ProjectRoot "SecureGate1.csproj"
$PublishProfile = Join-Path $ProjectRoot "securegate.runasp.net-WebDeploy.publishSettings"
$OutDir = Join-Path $ProjectRoot "artifacts\webdeploy-out"

if (-not (Test-Path $PublishProfile)) {
    throw "Publish profile not found: $PublishProfile"
}

[xml]$xml = Get-Content $PublishProfile
$profile = $xml.publishData.publishProfile
$site = $profile.msdeploySite
$hostName = $profile.publishUrl
$user = $profile.userName
$pwd = $profile.userPWD
$destUrl = "https://${hostName}:8172/msdeploy.axd?site=$site"

$msdeploy = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
if (-not (Test-Path $msdeploy)) {
    $msdeploy = "C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe"
}
if (-not (Test-Path $msdeploy)) {
    throw "Web Deploy (msdeploy.exe) is not installed."
}

Write-Host "Publishing Release build..."
dotnet publish $Csproj -c Release -o $OutDir --no-self-contained
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Uploading to MonsterASP ($site)..."
& $msdeploy -verb:sync `
  -source:contentPath="$OutDir" `
  -dest:contentPath="$site",computerName="$destUrl",userName="$user",password="$pwd",authtype="Basic",includeAcls="False" `
  -allowUntrusted `
  -enableRule:AppOffline `
  -skip:skipAction=Delete,objectName=dirPath,absolutePath=".*AppData.*" `
  -skip:skipAction=Delete,objectName=filePath,absolutePath=".*AppData.*" `
  -disableLink:AppPoolExtension `
  -disableLink:ContentExtension `
  -disableLink:CertificateExtension

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done. Site: $($profile.destinationAppUrl)"
