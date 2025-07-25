if not exist "%localappdata%\NINA\Plugins" (
        mkdir  "%localappdata%\NINA\Plugins"
)

xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Debug\net8.0-windows\NINA.Photon.Plugin.ASA.dll "%localappdata%\NINA\Plugins\3.0.0\ASA Tools\" /h/k/r/y

xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Debug\net8.0-windows\NINA.Photon.Plugin.ASA.pdb "%localappdata%\NINA\Plugins\3.0.0\ASA Tools\" /h/k/r/y
rem xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Release\net8.0-windows\CSharpFITS.dll "%localappdata%\NINA\Plugins\3.0.0\ASA Tools" /h/k/r/y
     
rem xcopy C:\Users\Gerald\.nuget\packages\antlr4.runtime.standard\4.11.1\lib\net45\Antlr4.Runtime.Standard.dll "%localappdata%\NINA\Plugins\3.0.0\ASA Tools" /h/k/r/y      


exit 0