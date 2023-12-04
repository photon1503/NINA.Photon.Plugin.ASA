pwsh.exe

.\CreateNET7Manifest.ps1 `
    -createArchive `
    -includeAll `
    -appendVersionToArchive  `
    -file "C:\Users\Gerald\AppData\Local\NINA\Plugins\3.0.0\ASA Tools\NINA.Photon.Plugin.ASA.dll" `
    -installerUrl https://github.com/photon1503/NINA.Photon.Plugin.ASA/releases/download/3.0.1.1/NINA.Photon.Plugin.ASA.3.0.1.1.zip 
                  
