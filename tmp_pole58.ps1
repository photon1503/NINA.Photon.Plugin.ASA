$path='doc/Grid 58Stars.grd'
$latDeg=48.0
$lat=[math]::PI*$latDeg/180.0
$lines=Get-Content $path | Where-Object { $_.Trim().Length -gt 0 }
$n=[int]$lines[0]
$pts=@()
$idx=1
for($i=0;$i -lt $n;$i++){
  $az=[double]::Parse($lines[$idx],[System.Globalization.CultureInfo]::InvariantCulture)
  $alt=[double]::Parse($lines[$idx+1],[System.Globalization.CultureInfo]::InvariantCulture)
  $onlySlew=($lines[$idx+3].Trim('"').ToLower() -eq 'true')
  $idx += 5
  if($onlySlew){ continue }

  $x=[math]::Cos($alt)*[math]::Sin($az)
  $y=[math]::Cos($alt)*[math]::Cos($az)
  $z=[math]::Sin($alt)
  $pts += [pscustomobject]@{x=$x;y=$y;z=$z;az=$az*180/[math]::PI;alt=$alt*180/[math]::PI}
}

# local north celestial pole direction (az=0, alt=lat)
$px=0.0
$py=[math]::Cos($lat)
$pz=[math]::Sin($lat)

# basis around pole
$e1x=1.0; $e1y=0.0; $e1z=0.0  # east, orthogonal to pole here
$e2x=$py*$e1z - $pz*$e1y
$e2y=$pz*$e1x - $px*$e1z
$e2z=$px*$e1y - $py*$e1x
$norm=[math]::Sqrt($e2x*$e2x+$e2y*$e2y+$e2z*$e2z)
$e2x/=$norm; $e2y/=$norm; $e2z/=$norm

$enriched=@()
foreach($p in $pts){
  $dot=$p.x*$px + $p.y*$py + $p.z*$pz
  if($dot -gt 1){$dot=1}; if($dot -lt -1){$dot=-1}
  $dist=[math]::Acos($dot)*180/[math]::PI
  $u1=$p.x*$e1x + $p.y*$e1y + $p.z*$e1z
  $u2=$p.x*$e2x + $p.y*$e2y + $p.z*$e2z
  $theta=[math]::Atan2($u2,$u1)*180/[math]::PI
  if($theta -lt 0){$theta += 360}
  $enriched += [pscustomobject]@{dist=$dist;theta=$theta;az=$p.az;alt=$p.alt}
}

# cluster by ring-distance tolerance
$sorted=$enriched | Sort-Object dist
$rings=@()
foreach($p in $sorted){
  if($rings.Count -eq 0){ $rings += ,@($p); continue }
  $last=$rings[$rings.Count-1]
  $mean=($last | Measure-Object -Property dist -Average).Average
  if([math]::Abs($p.dist-$mean) -le 1.2){
    $rings[$rings.Count-1] = @($last + $p)
  } else {
    $rings += ,@($p)
  }
}

"points=$($enriched.Count) rings=$($rings.Count)"
"ringDistMean count meanAngularStep spread std"
$means=@()
foreach($r in $rings){
  $m=($r|Measure-Object dist -Average).Average
  $means+=$m
  $ths=@($r|ForEach-Object theta|Sort-Object)
  if($ths.Count -gt 1){
    $diffs=@()
    for($i=0;$i -lt $ths.Count;$i++){
      $d=($ths[($i+1)%$ths.Count]-$ths[$i]); while($d -lt 0){$d+=360}; while($d -ge 360){$d-=360}; $diffs+=$d
    }
    $mean=($diffs|Measure-Object -Average).Average
    $min=($diffs|Measure-Object -Minimum).Minimum
    $max=($diffs|Measure-Object -Maximum).Maximum
    $spread=$max-$min
    $sum=0.0; foreach($v in $diffs){$sum += ($v-$mean)*($v-$mean)}
    $std=[math]::Sqrt($sum/$diffs.Count)
  } else { $mean=0; $spread=0; $std=0 }
  "{0,8:N3} {1,5} {2,12:N3} {3,8:N3} {4,8:N3}" -f $m,$ths.Count,$mean,$spread,$std
}
"\nring-distance spacings:"
for($i=1;$i -lt $means.Count;$i++){
  Write-Host -NoNewline ("{0,8:N3} " -f ($means[$i]-$means[$i-1]))
}
""
