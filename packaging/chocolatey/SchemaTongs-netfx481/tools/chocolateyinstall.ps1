$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$zipArchive = Join-Path $toolsDir -ChildPath 'SchemaTongs-net481.zip'

Get-ChocolateyUnzip $zipArchive $toolsDir

