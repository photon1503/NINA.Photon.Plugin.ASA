$path='doc/Grid 58Stars.grd'
$lines=Get-Content $path | Where-Object { $_.Trim().Length -gt 0 }
$n=[int]$lines[0]
$raw=@()
$idx=1
for($i=0;$i -lt $n;$i++){
  $az=[double]::Parse($lines[$idx],[System.Globalization.CultureInfo]::InvariantCulture)
  $alt=[double]::Parse($lines[$idx+1],[System.Globalization.CultureInfo]::InvariantCulture)
  $onlySlew=($lines[$idx+3].Trim('"').ToLower() -eq 'true')
  $idx += 5
  if(-not $onlySlew){ $raw += [pscustomobject]@{az=$az;alt=$alt} }
}

function NormRad([double]$a){
  $two=2.0*[math]::PI
  while($a -lt 0){$a += $two}
  while($a -ge $two){$a -= $two}
  return $a
}

$transforms=@(
  @{name='A=az'; f={param($az) $az}},
  @{name='A=pi-az'; f={param($az) [math]::PI-$az}},
  @{name='A=az+pi'; f={param($az) $az+[math]::PI}},
  @{name='A=-az'; f={param($az) -$az}}
)
$latitudes=@(48.0,-48.0)

foreach($latDeg in $latitudes){
  $phi=[math]::PI*$latDeg/180.0
  foreach($t in $transforms){
    $decs=@()
    foreach($p in $raw){
      $A=NormRad((& $t.f $p.az))
      $h=$p.alt
      $sinDec=[math]::Sin($h)*[math]::Sin($phi) + [math]::Cos($h)*[math]::Cos($phi)*[math]::Cos($A)
      if($sinDec -gt 1){$sinDec=1}; if($sinDec -lt -1){$sinDec=-1}
      $decs += [math]::Asin($sinDec)*180/[math]::PI
    }
    $bins=$decs | Group-Object { [math]::Round($_,1) }
    $repBins=($bins | Where-Object Count -ge 2).Count
    $singleBins=($bins | Where-Object Count -eq 1).Count
    $maxCount=($bins | Measure-Object -Property Count -Maximum).Maximum
    $stdAll=[math]::Sqrt((($decs | ForEach-Object { ($_-($decs|Measure-Object -Average).Average)*($_-($decs|Measure-Object -Average).Average) } | Measure-Object -Sum).Sum)/$decs.Count)
    "lat=$latDeg $($t.name) bins=$($bins.Count) repBins=$repBins singleBins=$singleBins maxBinCount=$maxCount"
  }
}
