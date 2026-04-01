$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$zipArchive = Join-Path $toolsDir -ChildPath 'SchemaTongs-net10.0.zip'

Get-ChocolateyUnzip $zipArchive $toolsDir

