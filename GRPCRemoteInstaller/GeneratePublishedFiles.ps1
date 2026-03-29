param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string[]]$ExcludeFiles = @()
)

$ErrorActionPreference = "Stop"

function Get-HashId {
    param(
        [string]$Prefix,
        [string]$Value
    )

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant())
        $hashBytes = $sha1.ComputeHash($bytes)
        $hash = [System.BitConverter]::ToString($hashBytes).Replace("-", "").Substring(0, 16)
        return "$Prefix$hash"
    }
    finally {
        $sha1.Dispose()
    }
}

function Escape-Xml {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-DirectoryNode {
    param([string]$Name, [string]$RelativePath)

    return [ordered]@{
        Name = $Name
        RelativePath = $RelativePath
        Directories = [ordered]@{}
        Files = New-Object System.Collections.Generic.List[object]
    }
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $normalizedBasePath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $normalizedBasePath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $normalizedBasePath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($normalizedBasePath)
    $targetUri = [System.Uri]::new([System.IO.Path]::GetFullPath($TargetPath))
    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', '\')
}

function Add-IndentedLine {
    param(
        [System.Text.StringBuilder]$Builder,
        [int]$Indent,
        [string]$Text
    )

    [void]$Builder.Append((' ' * $Indent))
    [void]$Builder.AppendLine($Text)
}

function Write-Component {
    param(
        [System.Text.StringBuilder]$Builder,
        [int]$Indent,
        [hashtable]$File
    )

    Add-IndentedLine -Builder $Builder -Indent $Indent -Text "<Component Id=`"$($File.ComponentId)`" Guid=`"*`" Bitness=`"always64`">"
    Add-IndentedLine -Builder $Builder -Indent ($Indent + 2) -Text "<File Id=`"$($File.FileId)`" Source=`"$(Escape-Xml $File.FullPath)`" KeyPath=`"yes`" />"
    Add-IndentedLine -Builder $Builder -Indent $Indent -Text "</Component>"
}

function Write-DirectoryContents {
    param(
        [System.Text.StringBuilder]$Builder,
        [hashtable]$Node,
        [int]$Indent
    )

    foreach ($file in $Node.Files) {
        Write-Component -Builder $Builder -Indent $Indent -File $file
    }

    foreach ($childName in $Node.Directories.Keys) {
        $child = $Node.Directories[$childName]
        Add-IndentedLine -Builder $Builder -Indent $Indent -Text "<Directory Id=`"$($child.DirectoryId)`" Name=`"$(Escape-Xml $child.Name)`">"
        Write-DirectoryContents -Builder $Builder -Node $child -Indent ($Indent + 2)
        Add-IndentedLine -Builder $Builder -Indent $Indent -Text "</Directory>"
    }
}

$publishDir = [System.IO.Path]::GetFullPath($PublishDir)
$outputPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Publish directory '$publishDir' does not exist."
}

$root = New-DirectoryNode -Name "" -RelativePath ""
$componentIds = New-Object System.Collections.Generic.List[string]
$excludedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($item in $ExcludeFiles) {
    if (-not [string]::IsNullOrWhiteSpace($item)) {
        [void]$excludedSet.Add($item)
    }
}

$files = Get-ChildItem -LiteralPath $publishDir -File -Recurse |
    Sort-Object FullName |
    Where-Object {
        $relative = Get-RelativePath -BasePath $publishDir -TargetPath $_.FullName
        return -not $excludedSet.Contains($relative)
    }

foreach ($file in $files) {
    $relativePath = Get-RelativePath -BasePath $publishDir -TargetPath $file.FullName
    $directoryPath = [System.IO.Path]::GetDirectoryName($relativePath)
    $segments = @()
    if (-not [string]::IsNullOrWhiteSpace($directoryPath)) {
        $segments = $directoryPath -split '[\\/]'
    }

    $node = $root
    $builtPath = ""
    foreach ($segment in $segments) {
        $builtPath = if ([string]::IsNullOrEmpty($builtPath)) { $segment } else { "$builtPath\$segment" }
        if (-not $node.Directories.Contains($segment)) {
            $child = New-DirectoryNode -Name $segment -RelativePath $builtPath
            $child.DirectoryId = Get-HashId -Prefix "dir_" -Value $builtPath
            $node.Directories[$segment] = $child
        }

        $node = $node.Directories[$segment]
    }

    $componentId = Get-HashId -Prefix "cmp_" -Value $relativePath
    $fileId = Get-HashId -Prefix "fil_" -Value $relativePath
    $fileRecord = [ordered]@{
        RelativePath = $relativePath
        FullPath = $file.FullName
        ComponentId = $componentId
        FileId = $fileId
    }

    $node.Files.Add($fileRecord)
    $componentIds.Add($componentId)
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$builder = New-Object System.Text.StringBuilder
Add-IndentedLine -Builder $builder -Indent 0 -Text '<?xml version="1.0" encoding="utf-8"?>'
Add-IndentedLine -Builder $builder -Indent 0 -Text '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">'
Add-IndentedLine -Builder $builder -Indent 2 -Text '<Fragment>'
Add-IndentedLine -Builder $builder -Indent 4 -Text '<ComponentGroup Id="PublishedFiles">'
foreach ($componentId in $componentIds) {
    Add-IndentedLine -Builder $builder -Indent 6 -Text "<ComponentRef Id=`"$componentId`" />"
}
Add-IndentedLine -Builder $builder -Indent 4 -Text '</ComponentGroup>'
Add-IndentedLine -Builder $builder -Indent 2 -Text '</Fragment>'
Add-IndentedLine -Builder $builder -Indent 2 -Text '<Fragment>'
Add-IndentedLine -Builder $builder -Indent 4 -Text '<DirectoryRef Id="INSTALLFOLDER">'
Write-DirectoryContents -Builder $builder -Node $root -Indent 6
Add-IndentedLine -Builder $builder -Indent 4 -Text '</DirectoryRef>'
Add-IndentedLine -Builder $builder -Indent 2 -Text '</Fragment>'
Add-IndentedLine -Builder $builder -Indent 0 -Text '</Wix>'

[System.IO.File]::WriteAllText($outputPath, $builder.ToString(), [System.Text.UTF8Encoding]::new($false))
