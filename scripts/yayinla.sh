#!/usr/bin/env bash
# Yeni sürümü yayınlar (BRIEF §7.3): kurulu 1.0 programları en geç 4 saatte görür, arkada indirir, yeniden açılışta kurar.
#   1) Directory.Build.props: InformationalVersion (görünen sürüm) ve FileVersion'ın son parçası (yapı) artırılır
#   2) powershell -ExecutionPolicy Bypass -File scripts/kurulum.ps1     3) bash scripts/yayinla.sh
# Dosyalar kurumsalmuzik.net/indir/kmp/'ye gider (sunucuda /var/www/indir/kmp). Sitedeki ve e-postalardaki sabit indirme
# adresi /indir/Kurumsal-Muzik-Player-Kurulum.exe de son 1.0 sürümüne çevrilir (karar Onur, 25 Eki 2026).
# 2.x'in /indir/latest.yml'ine ve eski adlı VM-Radio-Player-2.0-Kurulum.exe'ye DOKUNULMAZ.
# Aynı yapı ikinci kez yüklenmez. Önce kurulum dosyası, en son latest.json: programlar dosya yerine oturmadan görmez.
set -euo pipefail
cd "$(dirname "$0")/.."

HOST=${KM_HOST:-onur@kurumsalmuzik-vps.tail8660b4.ts.net}
KEY=~/.ssh/kurumsalmuzik_vps
DIR=/var/www/indir/kmp

V=$(sed -n 's:.*<InformationalVersion>\(.*\)</InformationalVersion>.*:\1:p' Directory.Build.props)
BUILD=$(sed -n 's:.*<FileVersion>[0-9]*\.[0-9]*\.[0-9]*\.\([0-9]*\)</FileVersion>.*:\1:p' Directory.Build.props)
EXE="Kurumsal-Muzik-Player-Kurulum-$V.exe"
[ -n "$V" ] && [ -n "$BUILD" ] || { echo "Sürüm ya da yapı okunamadı (Directory.Build.props)"; exit 1; }
[ -f "dist/$EXE" ] || { echo "Eksik: dist/$EXE (önce scripts/kurulum.ps1)"; exit 1; }

SHA=$(sha256sum "dist/$EXE" | cut -d' ' -f1)
SIZE=$(stat -c %s "dist/$EXE")
printf '{"version":"%s","build":%s,"file":"%s","sha256":"%s","size":%s}\n' "$V" "$BUILD" "$EXE" "$SHA" "$SIZE" > dist/latest.json

VAR=$(ssh -i $KEY -o BatchMode=yes -o ConnectTimeout=15 $HOST "test -e $DIR/$EXE && echo var || echo yok") || {
  echo "Sunucuya bağlanılamadı (Tailscale açık mı? Değilse ofisten KM_HOST=onur@2.24.171.101 ile)."; exit 1; }
[ "$VAR" = "yok" ] || { echo "$V zaten yayınlanmış. Yeni sürüm ve yapı numarası verin."; exit 1; }

echo "== $V (yapı $BUILD) yükleniyor"
TMP=/tmp/kmp-$BUILD
ssh -i $KEY $HOST "rm -rf $TMP && mkdir -p $TMP"
scp -i $KEY "dist/$EXE" dist/latest.json $HOST:$TMP/
ssh -i $KEY $HOST "set -e; sudo mkdir -p $DIR; cd $DIR; sudo install -m 644 '$TMP/$EXE' .; sudo install -m 644 $TMP/latest.json latest.json.yeni; sudo mv latest.json.yeni latest.json; sudo cp '$EXE' ../Kurumsal-Muzik-Player-Kurulum.exe.yeni; sudo mv ../Kurumsal-Muzik-Player-Kurulum.exe.yeni ../Kurumsal-Muzik-Player-Kurulum.exe; rm -rf $TMP"

echo "== kontrol"
curl -fsS "https://kurumsalmuzik.net/indir/kmp/latest.json?t=$(date +%s)"
echo
echo "sabit adres: $(curl -sI https://kurumsalmuzik.net/indir/Kurumsal-Muzik-Player-Kurulum.exe | grep -i content-length | tr -d '')"
echo
echo "== yayında: $V (kurulu 1.0 programları en geç 4 saat içinde indirir, yeniden açılışta kurar)"
