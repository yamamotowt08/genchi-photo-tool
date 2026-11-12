#!/usr/bin/env python3
"""Windows 用のアプリ（EXE 1 つ）を作る。

    python3 build_app.py

1. build_standalone.py でツール本体（現地調査写真整理ツール.html）を作り直す
2. app/ の起動アプリ（.NET Framework 4.8 + WebView2）をビルドする。
   ツール本体と WebView2 の DLL は EXE の中に入る
3. dist/GenchiPhotoTool.exe に置く
"""
import shutil
import subprocess
import sys
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
print(f'dist/{EXE.name} を作りました（{EXE.stat().st_size // 1024} KB）')
