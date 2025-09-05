if not exist "%localappdata%\NINA\Plugins" (
        mkdir  "%localappdata%\NINA\Plugins"
)

xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Debug\net8.0-windows\NINA.Photon.Plugin.ASA.dll "%localappdata%\NINA\Plugins\3.0.0\ASA Tools\" /h/i/c/k/e/r/y
xcopy C:\git\NINA.Photon.Plugin.ASA\NINA.Photon.Plugin.ASA\bin\Debug\net8.0-windows\NINA.Photon.Plugin.ASA.pdb "%localappdata%\NINA\Plugins\3.0.0\ASA Tools\" /h/i/c/k/e/r/y


exit 0