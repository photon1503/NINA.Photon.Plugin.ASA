$ErrorActionPreference='Stop'
function ReadPts($p){
  $l=Get-Content $p
  $n=[int]$l[0]
  $pts=@()
  $i=1
  for($k=0;$k -lt $n;$k++){
    $az=[double]$l[$i]*180/[math]::PI
    $alt=[double]$l[$i+1]*180/[math]::PI
    $rho=90-$alt
    $rad=$az*[math]::PI/180
    $x=$rho*[math]::Sin($rad)
    $y=$rho*[math]::Cos($rad)
    $side=[int]$l[$i+4]
    $pts+=[pscustomobject]@{az=$az;alt=$alt;rho=$rho;x=$x;y=$y;side=$side}
    $i+=5
  }
  return $pts
}
$asa=ReadPts 'doc/Grid 195Stars.grd'
'count=' + $asa.Count
'y bins (rounded 1) top:'
$asa|Group-Object { [math]::Round($_.y,1)} | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object {"  $($_.Name):$($_.Count)"}
''
'x bins (rounded 1) top:'
$asa|Group-Object { [math]::Round($_.x,1)} | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object {"  $($_.Name):$($_.Count)"}
''
'sample sorted by y then x (first 60):'
$asa|Sort-Object y,x|Select-Object -First 60|ForEach-Object {"  x=$([math]::Round($_.x,1)) y=$([math]::Round($_.y,1)) az=$([math]::Round($_.az,1)) alt=$([math]::Round($_.alt,1)) side=$($_.side)"}
