$lat=48*[math]::PI/180
function AltAzFromDecHa($decDeg,$haDeg){
  $dec=$decDeg*[math]::PI/180
  $H=$haDeg*[math]::PI/180
  $sinAlt=[math]::Sin($lat)*[math]::Sin($dec)+[math]::Cos($lat)*[math]::Cos($dec)*[math]::Cos($H)
  $alt=[math]::Asin([math]::Max(-1.0,[math]::Min(1.0,$sinAlt)))
  $cosAlt=[math]::Cos($alt)
  if([math]::Abs($cosAlt) -lt 1e-9){$A=0}else{
    $sinA= -[math]::Cos($dec)*[math]::Sin($H)/$cosAlt
    $cosA=([math]::Sin($dec)-[math]::Sin($alt)*[math]::Sin($lat))/($cosAlt*[math]::Cos($lat))
    $A=[math]::Atan2($sinA,$cosA)
    if($A -lt 0){$A += 2*[math]::PI}
  }
  [pscustomobject]@{az=$A*180/[math]::PI;alt=$alt*180/[math]::PI}
}
$pts=@(); $raSpacing=10; $decSpacing=8; $idx=0
for($dec=90;$dec -ge -15;$dec-=$decSpacing){
  $n=[math]::Max(1,[int][math]::Round((360*[math]::Cos($dec*[math]::PI/180))/$raSpacing))
  for($i=0;$i -lt $n;$i++){
    $ha=-180 + (360.0*$i/$n) + (($idx%2)*180.0/$n)
    $p=AltAzFromDecHa $dec $ha
    if($p.alt -ge 0){$pts+=$p}
  }
  $idx++
}
'count=' + $pts.Count
$pts | Select-Object -First 20 | ForEach-Object { 'az=' + [math]::Round($_.az,2) + ' alt=' + [math]::Round($_.alt,2) }
