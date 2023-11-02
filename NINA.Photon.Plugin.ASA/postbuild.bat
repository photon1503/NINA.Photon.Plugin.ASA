echo %1
echo %2
echo %3
if not exist "%localappdata%\NINA\Plugins" (
        mkdir  "%localappdata%\NINA\Plugins"
)
if not exist "%localappdata%\NINA\Plugin\ASA Tools" (
        mkdir  "%localappdata%\NINA\Plugins\ASA Tools"
)

xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Release\net7.0-windows\NINA.Photon.Plugin.ASA.dll "%localappdata%\NINA\Plugins\ASA Tools" /h/k/r/y
xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Release\net7.0-windows\NINA.Photon.Plugin.ASA.pdb "%localappdata%\NINA\Plugins\ASA Tools" /h/k/r/y
     
xcopy C:\Users\Gerald\.nuget\packages\antlr4.runtime.standard\4.11.1\Antlr4.Runtime.Standard.dll "%localappdata%\NINA\Plugins\ASA Tools" /h/k/r/y      