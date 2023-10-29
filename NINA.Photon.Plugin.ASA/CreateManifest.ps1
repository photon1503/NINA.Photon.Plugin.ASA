[CmdletBinding(DefaultParameterSetName='NoArchive')]
param(
    [Parameter(Mandatory, HelpMessage="Path to the compiled plugin assembly (dll)")]
    [string]$file,

    [Parameter(HelpMessage="The installer url that should be put into the manifest")]
    [string]$installerUrl,

    [Parameter(Mandatory=$false, ParameterSetName='Archive', HelpMessage="If the assembly should be packed into a zip file")]
    [switch]$createArchive,

    [Parameter(Mandatory=$false, ParameterSetName='Archive', HelpMessage="If all files in the assembly's directory should be included in the zip file")]
    [switch]$includeAll,

    [Parameter(ParameterSetName='Archive', HelpMessage="Name of the zip archive")]
    [string]$archiveName

)

Write-Output "Generating manifest from assembly"
Write-Output $file
Write-Output "-------------"
Write-Output "-------------"

$installerType = "DLL"

## Create a zip archive if parameter is given and use that checksum instead
if($createArchive) {
    Write-Output "Creating zip archive"
    $installerType = "ARCHIVE"
	
    if(!$archiveName) {
        $archiveName = [io.path]::GetFileNameWithoutExtension($file)
    }
    $zipfile = $archiveName + ".zip"

    if(Test-Path $zipfile) {
        Remove-Item $zipfile
    }

	$compressFiles = $file
	
	if($includeAll) {
		$compressFiles = [System.IO.Path]::GetDirectoryName($file) + "\*"
	}
	
    Compress-Archive -Path $compressFiles -Destination $zipfile
    Write-Output "-------------"
    Write-Output "-------------"
    $checksum = Get-FileHash $zipfile
} else {
    ## For no archive calculate the hash of the dll instead
    $checksum = Get-FileHash $file
}

$bytes = [System.IO.File]::ReadAllBytes($file)
$assembly = [Reflection.Assembly]::Load($bytes)

$meta = [reflection.customattributedata]::GetCustomAttributes($assembly)
$manifest = [ordered]@{
    Name = ""
    Identifier = ""
    Version = @{}
    Author = ""
    Homepage = ""
    Repository = ""
    License = ""
    LicenseURL = ""
    ChangelogURL = ""
    Tags = ""
    MinimumApplicationVersion = @{}
    Descriptions = [ordered]@{
        ShortDescription = ""
        LongDescription = ""
        FeaturedImageURL = ""
        ScreenshotURL = ""
        AltScreenshotURL = ""
    }
    Installer = @{}
}


#Read Metadata out of assembly
foreach($val in $meta) {
	if($val.AttributeType -like "System.Reflection.AssemblyTitleAttribute") {
		$manifest["Name"] = $val.ConstructorArguments[0].Value
	}
	if($val.AttributeType -like "System.Runtime.InteropServices.GuidAttribute") {
		$manifest["Identifier"] = $val.ConstructorArguments[0].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyFileVersionAttribute") {
        $version = $val.ConstructorArguments[0].Value.Split(".");
		$manifest["Version"] = [ordered]@{
            Major = $version[0]
            Minor = $version[1]
            Patch = $version[2]
            Build = $version[3]
        }
	}
	if($val.AttributeType -like "System.Reflection.AssemblyCompanyAttribute") {
		$manifest["Author"] = $val.ConstructorArguments[0].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "Homepage" ) {
		$manifest["Homepage"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "Repository" ) {
		$manifest["Repository"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "License" ) {
		$manifest["License"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "LicenseURL" ) {
		$manifest["LicenseURL"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "ChangelogURL" ) {
		$manifest["ChangelogURL"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "Tags" ) {
        $manifest["Tags"] = $val.ConstructorArguments[1].Value.Split(",");
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "MinimumApplicationVersion" ) {
        $version = $val.ConstructorArguments[1].Value.Split(".");
		$manifest["MinimumApplicationVersion"] = [ordered]@{
            Major = $version[0]
            Minor = $version[1]
            Patch = $version[2]
            Build = $version[3]
        }
	}
	if($val.AttributeType -like "System.Reflection.AssemblyDescriptionAttribute") {
		$manifest["Descriptions"]["ShortDescription"] = $val.ConstructorArguments[0].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "LongDescription" ) {
		$manifest["Descriptions"]["LongDescription"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "FeaturedImageURL" ) {
		$manifest["Descriptions"]["FeaturedImageURL"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "ScreenshotURL" ) {
		$manifest["Descriptions"]["ScreenshotURL"] = $val.ConstructorArguments[1].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyMetadataAttribute" -And $val.ConstructorArguments[0].Value -like "AltScreenshotURL" ) {
		$manifest["Descriptions"]["AltScreenshotURL"] = $val.ConstructorArguments[1].Value
	}
}

#Installer property gen

$manifest["Installer"] = [ordered]@{
    URL = $installerUrl
    Type = $installerType
    Checksum = $checksum.Hash
    ChecksumType = $checksum.Algorithm
}

# Formats JSON with 4 spaces as indentation
function Format-Json([Parameter(Mandatory, ValueFromPipeline)][String] $json) {
    $indent = 0;
    ($json -Split "`n" | % {
        if ($_ -match '[\}\]]\s*,?\s*$') {
            # This line ends with ] or }, decrement the indentation level
            $indent--
        }
        $line = ('    ' * $indent) + $($_.TrimStart() -replace '":  (["{[])', '": $1' -replace ':  ', ': ')
        if ($_ -match '[\{\[]\s*$') {
            # This line ends with [ or {, increment the indentation level
            $indent++
        }
        $line
    }) -Join "`n"
}
$json = ConvertTo-Json $manifest | Format-Json 
$json | Out-File "manifest.json" -Encoding Utf8
Write-Output $json
Write-Output "-------------"
Write-Output "-------------"
Write-Output "Manifest JSON created"