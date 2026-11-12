#!/usr/bin/env python3
"""Windows 用のアプリ（起動アプリ＋ツール）を作って、配布用の ZIP にまとめる。

    python3 build_app.py

1. build_standalone.py で部品入りの HTML を作り直す
2. app/ の起動アプリ（.NET Framework 4.8 + WebView2）をビルドする
3. dist/現地調査写真整理ツール_アプリ.zip にまとめる
"""
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
NAME = '現地調査写真整理ツール_アプリ'
BIN = HERE / 'app' / 'bin' / 'Release' / 'net48'
DIST = HERE / 'dist'

README = """現地調査写真整理ツール（アプリ版）

■ 使い方
  このフォルダの「GenchiPhotoTool.exe」をダブルクリックすると、ツールが開きます。
  フォルダごと共有フォルダ（\\\\newjec\\dfs\\…）に置いたままでも使えます。

■ フォルダの中身（消したり移動したりしないでください）
  GenchiPhotoTool.exe ………………… 起動アプリ
  現地調査写真整理ツール_部品入り.html … ツール本体
  Microsoft.Web.WebView2.*.dll、runtimes … Edge の部品を使うためのファイル

■ ツールを新しい版にするとき
  「現地調査写真整理ツール_部品入り.html」を新しいものに置き換えるだけです。
  次にアプリを起動したときから、新しい版になります。

■ 必要なもの
  ・Windows 10 / 11（.NET Framework 4.8 と Microsoft Edge WebView2 ランタイムは、ふつう最初から入っています）
  ・インターネット接続（国土地理院の地図を読み込むため）

■ 作業用のファイル
  アプリは、各自のパソコンの次の場所に作業用のファイルを作ります（共有フォルダには書き込みません）。
  %LOCALAPPDATA%\\GenchiPhotoTool

■ 公開ページ
  https://github.com/yamamotowt08/genchi-photo-tool
"""


def run(cmd, cwd):
    print('>', ' '.join(cmd))
    subprocess.run(cmd, cwd=cwd, check=True)


run([sys.executable, 'build_standalone.py'], HERE)
run(['dotnet', 'build', '-c', 'Release', '--nologo'], HERE / 'app')

out = DIST / NAME
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)
for f in ['GenchiPhotoTool.exe', 'GenchiPhotoTool.exe.config',
          'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll']:
    shutil.copy2(BIN / f, out / f)
shutil.copytree(BIN / 'runtimes', out / 'runtimes')
shutil.copy2(HERE / '現地調査写真整理ツール_部品入り.html', out)
(out / '使い方.txt').write_text(README, encoding='utf-8-sig')

zip_path = DIST / f'{NAME}.zip'
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in sorted(out.rglob('*')):
        if p.is_file():
            z.write(p, Path(NAME) / p.relative_to(out))
print(f'{zip_path.relative_to(HERE)} を作りました（{zip_path.stat().st_size // 1024} KB）')
