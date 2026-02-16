function Analyze-Grid([string]$path,[double]$latDeg){
  $phi=[math]::PI*$latDeg/180.0
  $lines=Get-Content $path | Where-Object { $_.Trim().Length -gt 0 }
  $n=[int]$lines[0]
  $pts=@(); $idx=1
  for($i=0;$i -lt $n;$i++){
    $az=[double]::Parse($lines[$idx],[System.Globalization.CultureInfo]::InvariantCulture)
    $alt=[double]::Parse($lines[$idx+1],[System.Globalization.CultureInfo]::InvariantCulture)
    $onlySlew=($lines[$idx+3].Trim('"').ToLower() -eq 'true')
    $idx += 5
    if($onlySlew){continue}
    $A=[math]::PI-$az; while($A -lt 0){$A+=2*[math]::PI}; while($A -ge 2*[math]::PI){$A-=2*[math]::PI}
    $sinDec=[math]::Sin($alt)*[math]::Sin($phi)+[math]::Cos($alt)*[math]::Cos($phi)*[math]::Cos($A)
    if($sinDec -gt 1){$sinDec=1}; if($sinDec -lt -1){$sinDec=-1}
    $dec=[math]::Asin($sinDec)*180/[math]::PI
    $cosDec=[math]::Cos([math]::Asin($sinDec)); if([math]::Abs($cosDec)-lt 1e-14){$cosDec=1e-14}
    $sinH=-([math]::Sin($A)*[math]::Cos($alt))/$cosDec
    $cosH=([math]::Sin($alt)-[math]::Sin($phi)*$sinDec)/([math]::Cos($phi)*$cosDec)
    $ha=[math]::Atan2($sinH,$cosH)*180/[math]::PI; if($ha -lt 0){$ha+=360}
    $pts += [pscustomobject]@{dec=$dec;ha=$ha}
  }
  $groups=$pts | Group-Object {[math]::Round($_.dec,0)} | Sort-Object {[double]$_.Name}
  "--- $path points=$($pts.Count) rounded-dec-rings=$($groups.Count) ---"
  "ringDec count nominalStep bestRMS"
  foreach($g in $groups){
    $N=[int]$g.Count; $step=360.0/$N
    $has=@($g.Group|ForEach-Object ha)
    if($N -le 1){"{0,6} {1,5} {2,11} {3,8}" -f $g.Name,$N,'n/a','n/a'; continue}
    $best=1e9
    for($phase=0;$phase -lt $step;$phase += 0.1){
      $sum=0.0
      foreach($ha in $has){
        $k=[math]::Round(($ha-$phase)/$step)
        $pred=$phase+$k*$step
        $d=$ha-$pred
        while($d -gt 180){$d-=360}; while($d -lt -180){$d+=360}
        $sum += $d*$d
      }
      $rms=[math]::Sqrt($sum/$N)
      if($rms -lt $best){$best=$rms}
    }
    "{0,6} {1,5} {2,11:N3} {3,8:N3}" -f $g.Name,$N,$step,$best
  }
}
Analyze-Grid 'doc/grid-90.grd' 48
Analyze-Grid 'doc/nina.grd' 48
