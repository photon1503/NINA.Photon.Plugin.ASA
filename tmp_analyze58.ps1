$path='doc/Grid 58Stars.grd'
$latDeg=48.0
$phi=[math]::PI*$latDeg/180.0
$lines=Get-Content $path | Where-Object { $_.Trim().Length -gt 0 }
$n=[int]$lines[0]
$pts=New-Object System.Collections.Generic.List[object]
$idx=1
for($i=0;$i -lt $n;$i++){
  $az=[double]::Parse($lines[$idx],[System.Globalization.CultureInfo]::InvariantCulture)
  $alt=[double]::Parse($lines[$idx+1],[System.Globalization.CultureInfo]::InvariantCulture)
  $isMouse=($lines[$idx+2].Trim('"').ToLower() -eq 'true')
  $onlySlew=($lines[$idx+3].Trim('"').ToLower() -eq 'true')
  $pier=[int]::Parse($lines[$idx+4],[System.Globalization.CultureInfo]::InvariantCulture)
  $idx += 5
  if($onlySlew){ continue }

  $sinH=[math]::Sin($alt); $cosH=[math]::Cos($alt)
  $sinA=[math]::Sin($az);  $cosA=[math]::Cos($az)

  $sinDec = $sinH*[math]::Sin($phi) + $cosH*[math]::Cos($phi)*$cosA
  if($sinDec -gt 1){$sinDec=1}; if($sinDec -lt -1){$sinDec=-1}
  $dec=[math]::Asin($sinDec)
  $cosDec=[math]::Cos($dec)
  if([math]::Abs($cosDec) -lt 1e-14){ $cosDec=1e-14 }

  $sinHA = -($sinA*$cosH)/$cosDec
  $cosHA = ($sinH - [math]::Sin($phi)*$sinDec)/([math]::Cos($phi)*$cosDec)
  $ha=[math]::Atan2($sinHA,$cosHA)

  $pts.Add([pscustomobject]@{
    DecDeg=$dec*180.0/[math]::PI
    HADeg=$ha*180.0/[math]::PI
    AzDeg=$az*180.0/[math]::PI
    AltDeg=$alt*180.0/[math]::PI
    Pier=$pier
    IsMouse=$isMouse
  }) | Out-Null
}

$pts=$pts | Sort-Object DecDeg
$clusters=New-Object System.Collections.Generic.List[object]
foreach($p in $pts){
  if($clusters.Count -eq 0){
    $clusters.Add((New-Object System.Collections.Generic.List[object])) | Out-Null
    $clusters[0].Add($p) | Out-Null
    continue
  }
  $last=$clusters[$clusters.Count-1]
  $mean=($last | Measure-Object -Property DecDeg -Average).Average
  if([math]::Abs($p.DecDeg-$mean) -le 0.75){
    $last.Add($p) | Out-Null
  } else {
    $nl=New-Object System.Collections.Generic.List[object]
    $nl.Add($p) | Out-Null
    $clusters.Add($nl) | Out-Null
  }
}

"points=$($pts.Count) rings=$($clusters.Count)"
"decMean count meanHAstep spread std"
$means=@()
foreach($c in $clusters){
  $decMean=($c | Measure-Object -Property DecDeg -Average).Average
  $means += $decMean
  $has=@($c | ForEach-Object HADeg | Sort-Object)
  $diffs=@()
  if($has.Count -gt 1){
    for($j=0;$j -lt $has.Count;$j++){
      $a=$has[$j]; $b=$has[($j+1)%$has.Count]
      $d=($b-$a)
      while($d -lt 0){$d+=360}
      while($d -ge 360){$d-=360}
      $diffs += $d
    }
    $mean=($diffs | Measure-Object -Average).Average
    $min=($diffs | Measure-Object -Minimum).Minimum
    $max=($diffs | Measure-Object -Maximum).Maximum
    $spread=$max-$min
    $sum=0.0
    foreach($v in $diffs){ $sum += ($v-$mean)*($v-$mean) }
    $std=[math]::Sqrt($sum/$diffs.Count)
  } else {
    $mean=0; $spread=0; $std=0
  }
  "{0,7:N3} {1,5} {2,10:N3} {3,8:N3} {4,8:N3}" -f $decMean,$has.Count,$mean,$spread,$std
}
"\nring mean-dec spacings:" 
for($i=1;$i -lt $means.Count;$i++){
  $sp=$means[$i]-$means[$i-1]
  Write-Host -NoNewline ("{0,7:N3} " -f $sp)
}
""
