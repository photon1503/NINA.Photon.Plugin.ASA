$path='doc/Grid 58Stars.grd'
$latDeg=48.0
$phi=[math]::PI*$latDeg/180.0
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

  # ASA grd convention to astronomical azimuth used by the transform
  $A=[math]::PI - $az
  while($A -lt 0){$A += 2*[math]::PI}
  while($A -ge 2*[math]::PI){$A -= 2*[math]::PI}

  $sinDec=[math]::Sin($alt)*[math]::Sin($phi) + [math]::Cos($alt)*[math]::Cos($phi)*[math]::Cos($A)
  if($sinDec -gt 1){$sinDec=1}; if($sinDec -lt -1){$sinDec=-1}
  $dec=[math]::Asin($sinDec)
  $cosDec=[math]::Cos($dec); if([math]::Abs($cosDec)-lt 1e-14){$cosDec=1e-14}
  $sinH = -([math]::Sin($A)*[math]::Cos($alt))/$cosDec
  $cosH = ([math]::Sin($alt)-[math]::Sin($phi)*$sinDec)/([math]::Cos($phi)*$cosDec)
  $ha=[math]::Atan2($sinH,$cosH)*180/[math]::PI
  if($ha -lt 0){$ha += 360}

  $pts += [pscustomobject]@{dec=[math]::Asin($sinDec)*180/[math]::PI;ha=$ha}
}

# cluster declinations by 0.3 deg to recover ring sets
$ordered=$pts | Sort-Object dec
$rings=@()
foreach($p in $ordered){
  if($rings.Count -eq 0){$rings += ,@($p); continue}
  $last=$rings[$rings.Count-1]
  $mean=($last|Measure-Object dec -Average).Average
  if([math]::Abs($p.dec-$mean) -le 0.3){
    $rings[$rings.Count-1]=@($last+$p)
  } else {
    $rings += ,@($p)
  }
}

"points=$($pts.Count) rings=$($rings.Count)"
"dec(mean) count HA spacing stats"
$decMeans=@()
foreach($r in $rings){
  $m=($r|Measure-Object dec -Average).Average
  $decMeans += $m
  $has=@($r|ForEach-Object ha|Sort-Object)
  $diffs=@()
  if($has.Count -gt 1){
    for($i=0;$i -lt $has.Count;$i++){
      $d=$has[($i+1)%$has.Count]-$has[$i]
      while($d -lt 0){$d+=360}
      while($d -ge 360){$d-=360}
      $diffs += $d
    }
    $mean=($diffs|Measure-Object -Average).Average
    $min=($diffs|Measure-Object -Minimum).Minimum
    $max=($diffs|Measure-Object -Maximum).Maximum
    $spread=$max-$min
    $sum=0.0; foreach($v in $diffs){$sum+=($v-$mean)*($v-$mean)}
    $std=[math]::Sqrt($sum/$diffs.Count)
    "{0,8:N3} {1,5} step~{2,7:N3} spread={3,7:N3} std={4,7:N3}" -f $m,$has.Count,$mean,$spread,$std
  } else {
    "{0,8:N3} {1,5} step~   n/a" -f $m,$has.Count
  }
}
"\ndeclination spacing between ring means:"
for($i=1;$i -lt $decMeans.Count;$i++){
  Write-Host -NoNewline ("{0,7:N3} " -f ($decMeans[$i]-$decMeans[$i-1]))
}
""
