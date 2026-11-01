# Original vector sources. No third-party images or resampling of 1C icons.
$ErrorActionPreference='Stop'
$icons=[ordered]@{
 'new'='<path d="M14 3H5v18h14V8l-5-5Z M14 3v5h5 M9 12v6 M6 15h6"/>'
 'open'='<path d="M3 8V5h7l3 3h8v3 M3 8v12h16l3-9H8l-3 9"/>'
 'save'='<path d="M4 3h13l4 4v14H3V3h1Z M7 3v6h9V3 M7 21v-8h10v8"/>'
 'search'='<circle cx="10" cy="10" r="6"/><path d="m15 15 6 6"/>'
 'undo'='<path d="m8 4-5 5 5 5 M3 9h11a6 6 0 0 1 0 12h-3"/>'
 'redo'='<path d="m16 4 5 5-5 5 M21 9H10a6 6 0 0 0 0 12h3"/>'
 'run'='<path d="m7 3 14 9-14 9V3Z"/>'
 'stop'='<rect x="5" y="5" width="14" height="14" rx="2"/>'
 'settings'='<path d="M4 6h16 M4 12h16 M4 18h16"/><rect x="7" y="3" width="4" height="6" rx="1"/><rect x="14" y="9" width="4" height="6" rx="1"/><rect x="8" y="15" width="4" height="6" rx="1"/>'
 'code'='<path d="m8 6-6 6 6 6 M16 6l6 6-6 6 M14 3l-4 18"/>'
 'filter'='<path d="M3 4h18l-7 8v7l-4 2v-9L3 4Z"/>'
 'add'='<circle cx="12" cy="12" r="9"/><path d="M12 7v10 M7 12h10"/>'
}
$destination=Join-Path $PSScriptRoot 'icons'
foreach($theme in @('dark','light')){
 $folder=Join-Path $destination $theme;New-Item -ItemType Directory -Force $folder|Out-Null
 $color=if($theme-eq'dark'){'#d5d9df'}else{'#343941'}
 foreach($entry in $icons.GetEnumerator()){
  $svg='<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><g fill="none" stroke="'+$color+'" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">'+$entry.Value+'</g></svg>'
  [IO.File]::WriteAllText((Join-Path $folder ($entry.Key+'.svg')),$svg,[Text.UTF8Encoding]::new($false))
 }
}
$sheet='<svg xmlns="http://www.w3.org/2000/svg" width="960" height="410" viewBox="0 0 960 410"><rect width="960" height="410" rx="10" fill="#1e1e1e"/><text x="28" y="40" font-family="Segoe UI,sans-serif" font-size="22" fill="#dadada">Dark1C · Original vector icon set</text>'
$index=0
foreach($entry in $icons.GetEnumerator()){
 $x=36+($index%6)*155;$y=76+[math]::Floor($index/6)*155
 $sheet+='<g transform="translate('+$x+','+$y+')"><rect width="132" height="132" rx="10" fill="#252525"/><g transform="translate(34 24) scale(2.6)" fill="none" stroke="#d5d9df" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">'+$entry.Value+'</g><text x="66" y="116" text-anchor="middle" font-family="Segoe UI,sans-serif" font-size="14" fill="#adb2bb">'+$entry.Key+'</text></g>'
 $index++
}
$sheet+='</svg>'
[IO.File]::WriteAllText((Join-Path $destination 'preview.svg'),$sheet,[Text.UTF8Encoding]::new($false))
