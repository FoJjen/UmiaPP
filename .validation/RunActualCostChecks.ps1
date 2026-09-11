$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('UmiaPP-ActualCosts-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
	$project = [System.Security.SecurityElement]::Escape((Join-Path $root 'UmiaPP.csproj'))
	$xml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWPF>true</UseWPF><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="' + $project + '" /></ItemGroup></Project>'
	Set-Content -Path (Join-Path $temp 'ActualCostChecks.csproj') -Value $xml -Encoding UTF8
	Copy-Item (Join-Path $PSScriptRoot 'ActualCostChecks.cs.txt') (Join-Path $temp 'Program.cs')
	dotnet build (Join-Path $temp 'ActualCostChecks.csproj') --verbosity quiet --tl:off -p:UseAppHost=false
	if ($LASTEXITCODE -ne 0) { throw 'Actual-cost regression harness build failed.' }
	dotnet (Join-Path $temp 'bin\Debug\net10.0-windows\ActualCostChecks.dll')
	if ($LASTEXITCODE -ne 0) { throw 'Actual-cost regression checks failed.' }
}
finally {
	Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
