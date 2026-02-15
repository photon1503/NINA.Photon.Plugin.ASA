$ErrorActionPreference='Stop'
$ref='doc/Grid 58Stars.grd'
$cur='doc/nina.grd'
$refLines=Get-Content $ref
$curLines=Get-Content $cur
"ref lines=$($refLines.Count) cur lines=$($curLines.Count)"
if($refLines.Count -ne $curLines.Count){ "line-count-diff"; exit 0 }
$diffs=@()
for($i=0;$i -lt $refLines.Count;$i++){
  $a=$refLines[$i].Trim(); $b=$curLines[$i].Trim()
  if($a -ne $b){
    $isNumA = $a -match '^-?\d+(\.\d+)?([Ee][+-]?\d+)?$'
    $isNumB = $b -match '^-?\d+(\.\d+)?([Ee][+-]?\d+)?$'
    if($isNumA -and $isNumB){
      $da=[double]$a; $db=[double]$b; $d=[math]::Abs($da-$db)
      $diffs += [pscustomobject]@{line=($i+1);type='num';delta=$d;a=$a;b=$b}
    } else {
      $diffs += [pscustomobject]@{line=($i+1);type='text';delta=$null;a=$a;b=$b}
    }
  }
}
"diff count=$($diffs.Count)"
if($diffs.Count -gt 0){
  "top diffs:" 
  $diffs | Select-Object -First 20 | ForEach-Object {
    if($_.type -eq 'num'){ "L$($_.line): delta=$($_.delta) a=$($_.a) b=$($_.b)" }
    else { "L$($_.line): text a=$($_.a) b=$($_.b)" }
  }
  $num=$diffs|Where-Object type -eq 'num'
  if($num.Count -gt 0){ "max numeric delta=" + (($num|Measure-Object delta -Maximum).Maximum) }
}
