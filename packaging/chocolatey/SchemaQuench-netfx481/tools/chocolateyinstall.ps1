$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$zipArchive = Join-Path $toolsDir -ChildPath 'SchemaQuench-net481.zip'

Get-ChocolateyUnzip $zipArchive $toolsDir

