$ErrorActionPreference = 'Stop' # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Uninstall-ChocolateyZipPackage -Packagename $env:ChocolateyPackageName -ZipFileName 'SchemaTongs-net481.zip'
