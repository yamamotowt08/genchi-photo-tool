#!/usr/bin/env python3
"""Windows 用のアプリ（EXE 1 つ）を作る。

    python3 build_app.py

1. build_standalone.py でツール本体（app/tool.html）を作り直す
2. app/ の起動アプリ（.NET Framework 4.8 + WebView2）をビルドする。
   ツール本体と WebView2 の DLL は EXE の中に入る
3. dist/GenchiPhotoTool.exe と、それを ZIP にした dist/GenchiPhotoTool.zip を作る
"""
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
EXE = HERE / 'app' / 'bin' / 'Release' / 'net48' / 'GenchiPhotoTool.exe'
DIST = HERE / 'dist'


def run(cmd, cwd):
    print('>', ' '.join(cmd))
    subprocess.run(cmd, cwd=cwd, check=True)


run([sys.executable, 'build_standalone.py'], HERE)
run(['dotnet', 'build', '-c', 'Release', '--nologo'], HERE / 'app')

if DIST.exists():
    shutil.rmtree(DIST)
DIST.mkdir()
shutil.copy2(EXE, DIST / EXE.name)
# ダウンロードのとき EXE より警告が出にくいよう、ZIP にしたものも作る
with zipfile.ZipFile(DIST / 'GenchiPhotoTool.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    z.write(EXE, EXE.name)
for f in sorted(DIST.iterdir()):
    print(f'dist/{f.name} を作りました（{f.stat().st_size // 1024} KB）')
