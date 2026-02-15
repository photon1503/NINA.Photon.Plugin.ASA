function Read-Grd($path){
  $l=Get-Content $path
  $n=[int]$l[0]
  $rows=@()
  $i=1
  for($k=0;$k -lt $n;$k++){
    $rows += [pscustomobject]@{
      az=[double]$l[$i]*180/[math]::PI
      alt=[double]$l[$i+1]*180/[math]::PI
      side=[int]$l[$i+4]
    }
    $i += 5
  }
  return $rows
}
function Segment-BySide($rows){
  $segs=@(); $start=0
  for($k=1;$k -lt $rows.Count;$k++){
    if($rows[$k-1].side -eq 0 -and $rows[$k].side -eq 1){
      $segs += [pscustomobject]@{A=$start;B=$k-1;Len=($k-$start)}
      $start = $k
    }
  }
  $segs += [pscustomobject]@{A=$start;B=($rows.Count-1);Len=($rows.Count-$start)}
  return $segs
}
$asa=Read-Grd 'doc/Grid 58Stars.grd'
$nina=Read-Grd 'doc/nina.grd'
'ASA count=' + $asa.Count + ' side0=' + (($asa|? side -eq 0).Count) + ' side1=' + (($asa|? side -eq 1).Count)
'NINA count=' + $nina.Count + ' side0=' + (($nina|? side -eq 0).Count) + ' side1=' + (($nina|? side -eq 1).Count)
''
'ASA segments len:'
(Segment-BySide $asa) | ForEach-Object { $_.Len } | Out-String
'NINA segments len:'
(Segment-BySide $nina) | ForEach-Object { $_.Len } | Out-String
''
'ASA alt min/max=' + [math]::Round(($asa|Measure-Object alt -Minimum).Minimum,2) + '/' + [math]::Round(($asa|Measure-Object alt -Maximum).Maximum,2)
'NINA alt min/max=' + [math]::Round(($nina|Measure-Object alt -Minimum).Minimum,2) + '/' + [math]::Round(($nina|Measure-Object alt -Maximum).Maximum,2)
