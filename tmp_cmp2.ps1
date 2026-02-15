$ref='doc/Grid 58Stars.grd'; $cur='doc/nina.grd'
$rl=Get-Content $ref; $cl=Get-Content $cur
"ref points=$($rl[0]) cur points=$($cl[0])"
$max= [Math]::Min($rl.Count,$cl.Count)
$diff=0; $maxDelta=0.0
for($i=1;$i -lt $max;$i++){
  $a=$rl[$i].Trim(); $b=$cl[$i].Trim();
  if($a -ne $b){
    $isNumA=$a -match '^-?\d+(\.\d+)?([Ee][+-]?\d+)?$'; $isNumB=$b -match '^-?\d+(\.\d+)?([Ee][+-]?\d+)?$'
    if($isNumA -and $isNumB){ $d=[math]::Abs([double]$a-[double]$b); if($d -gt $maxDelta){$maxDelta=$d} }
    $diff++
  }
}
"line-diffs=$diff max-num-delta=$maxDelta"
