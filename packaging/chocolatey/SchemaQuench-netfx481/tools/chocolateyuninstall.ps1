$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Uninstall-ChocolateyZipPackage -Packagename $env:ChocolateyPackageName -ZipFileName 'SchemaQuench-net481.zip'
