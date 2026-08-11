Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$png = [System.Drawing.Image]::FromFile("Resources/icon/icon_256x256.png")
$icon = [System.Drawing.Icon]::FromHandle($png.GetHicon())
$fs = [System.IO.FileStream]::new("Resources/Reficio.ico", [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()
$png.Dispose()
Write-Host "Icon created: Resources/Reficio.ico"