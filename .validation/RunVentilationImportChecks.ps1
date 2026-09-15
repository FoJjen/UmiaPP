$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('UmiaPP-ImportChecks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
	Copy-Item (Join-Path $PSScriptRoot 'VentilationImportChecks.cs.txt') (Join-Path $temp 'Program.cs')
	$parser = [System.Security.SecurityElement]::Escape((Join-Path $root 'Services\BluebeamXmlParserService.cs'))
	$rules = [System.Security.SecurityElement]::Escape((Join-Path $root 'Services\StandardVentilationArticleRules.cs'))
	$models = [System.Security.SecurityElement]::Escape((Join-Path $root 'Models\BluebeamModels.cs'))
	$project = Join-Path $temp 'ImportChecks.csproj'
	@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
	<OutputType>Exe</OutputType>
	<TargetFramework>net10.0</TargetFramework>
	<ImplicitUsings>enable</ImplicitUsings>
	<Nullable>enable</Nullable>
	<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
	<Compile Include="Program.cs" />
	<Compile Include="$parser" Link="BluebeamXmlParserService.cs" />
	<Compile Include="$rules" Link="StandardVentilationArticleRules.cs" />
	<Compile Include="$models" Link="BluebeamModels.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path $project -Encoding UTF8
	& dotnet run --project $project --configuration Release
	if ($LASTEXITCODE -ne 0) {
		throw "Ventilation import checks failed (exit code $LASTEXITCODE)."
	}
}
finally {
	Remove-Item -Path $temp -Recurse -Force
}
