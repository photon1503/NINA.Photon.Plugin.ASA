$path='doc/Grid 58Stars.grd'
$latDeg=48.0
$phi=[math]::PI*$latDeg/180.0
$lines=Get-Content $path | Where-Object { $_.Trim().Length -gt 0 }
$n=[int]$lines[0]
$pts=@(); $idx=1
for($i=0;$i -lt $n;$i++){
  $az=[double]::Parse($lines[$idx],[System.Globalization.CultureInfo]::InvariantCulture)
  $alt=[double]::Parse($lines[$idx+1],[System.Globalization.CultureInfo]::InvariantCulture)
  $onlySlew=($lines[$idx+3].Trim('"').ToLower() -eq 'true')
  $idx+=5
  if($onlySlew){continue}
  $A=[math]::PI-$az; while($A -lt 0){$A += 2*[math]::PI}; while($A -ge 2*[math]::PI){$A -= 2*[math]::PI}
  $sinDec=[math]::Sin($alt)*[math]::Sin($phi)+[math]::Cos($alt)*[math]::Cos($phi)*[math]::Cos($A)
  if($sinDec -gt 1){$sinDec=1}; if($sinDec -lt -1){$sinDec=-1}
  $dec=[math]::Asin($sinDec)
  $cosDec=[math]::Cos($dec); if([math]::Abs($cosDec)-lt 1e-14){$cosDec=1e-14}
  $sinH=-([math]::Sin($A)*[math]::Cos($alt))/$cosDec
  $cosH=([math]::Sin($alt)-[math]::Sin($phi)*$sinDec)/([math]::Cos($phi)*$cosDec)
  $ha=[math]::Atan2($sinH,$cosH)*180/[math]::PI; if($ha -lt 0){$ha+=360}
  $pts += [pscustomobject]@{dec=[math]::Round([math]::Asin($sinDec)*180/[math]::PI,0);ha=$ha}
}

$rings=$pts | Group-Object dec | Sort-Object {[double]$_.Name}
"ring  N  nominalStep  bestPhase  latticeResidualRMS  maxResidual"
foreach($g in $rings){
  $N=[int]$g.Count
  $step=360.0/$N
  $has=@($g.Group | ForEach-Object ha)
  $bestPhase=0.0; $bestRms=1e9; $bestMax=1e9
  for($phase=0.0;$phase -lt $step;$phase += 0.05){
    $errs=@()
    foreach($ha in $has){
      $k=[math]::Round(($ha-$phase)/$step)
      $pred=$phase + $k*$step
      $d=$ha-$pred
      while($d -gt 180){$d-=360}; while($d -lt -180){$d+=360}
      $errs += [math]::Abs($d)
    }
    $rms=[math]::Sqrt((($errs | ForEach-Object {$_*$_} | Measure-Object -Sum).Sum)/$errs.Count)
    $mx=($errs|Measure-Object -Maximum).Maximum
    if($rms -lt $bestRms){$bestRms=$rms; $bestPhase=$phase; $bestMax=$mx}
  }
  "{0,4} {1,3} {2,11:N3} {3,10:N3} {4,18:N3} {5,12:N3}" -f $g.Name,$N,$step,$bestPhase,$bestRms,$bestMax
}
