$ErrorActionPreference='Stop'
$ref='doc/Grid 58Stars.grd'
$cur='doc/nina.grd'

function Read-Grd($path){
  $l=Get-Content $path
  $n=[int]$l[0]
  $pts=@()
  $i=1
  for($k=0;$k -lt $n;$k++){
    $az=[double]$l[$i]
    $alt=[double]$l[$i+1]
    $side=[int]$l[$i+4]
    $pts += [pscustomobject]@{az=$az;alt=$alt;side=$side}
    $i+=5
  }
  return $pts
}

$h1=(Get-FileHash $ref -Algorithm SHA256).Hash
$h2=(Get-FileHash $cur -Algorithm SHA256).Hash
"hash ref=$h1"
"hash cur=$h2"
"hash equal=" + ($h1 -eq $h2)

$p1=Read-Grd $ref
$p2=Read-Grd $cur
"count ref=$($p1.Count) cur=$($p2.Count)"

# exact line-order comparison on az/alt/side tuples
$exactMismatch=0
for($i=0;$i -lt [Math]::Min($p1.Count,$p2.Count);$i++){
  if($p1[$i].az -ne $p2[$i].az -or $p1[$i].alt -ne $p2[$i].alt -or $p1[$i].side -ne $p2[$i].side){ $exactMismatch++ }
}
"exact tuple mismatches by index=$exactMismatch"

# order-independent comparison with tolerance
$tol=1e-12
$used=New-Object 'System.Collections.Generic.HashSet[int]'
$unmatched=0
$maxMinDist=0.0
for($i=0;$i -lt $p1.Count;$i++){
  $bestJ=-1; $bestD=[double]::PositiveInfinity
  for($j=0;$j -lt $p2.Count;$j++){
    if($used.Contains($j)){ continue }
    if($p1[$i].side -ne $p2[$j].side){ continue }
    $d=[math]::Abs($p1[$i].az-$p2[$j].az)+[math]::Abs($p1[$i].alt-$p2[$j].alt)
    if($d -lt $bestD){ $bestD=$d; $bestJ=$j }
  }
  if($bestJ -ge 0 -and $bestD -le $tol){
    $used.Add($bestJ) | Out-Null
    if($bestD -gt $maxMinDist){ $maxMinDist=$bestD }
  } else {
    $unmatched++
  }
}
"order-independent unmatched points=$unmatched"
"max matched L1 delta=$maxMinDist"
