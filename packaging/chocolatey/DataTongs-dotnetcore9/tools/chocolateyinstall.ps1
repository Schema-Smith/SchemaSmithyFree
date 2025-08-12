$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$zipArchive = Join-Path $toolsDir -ChildPath 'DataTongs-net9.0.zip'

Get-ChocolateyUnzip $zipArchive $toolsDir

