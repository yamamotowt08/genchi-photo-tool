#!/usr/bin/env python3
"""Windows 用のアプリ（EXE 1 つ）を作る。

    python3 build_app.py [--manual 操作マニュアル.docx]

1. build_standalone.py でツール本体（app/tool.html）を作り直す
2. app/ の起動アプリ（.NET Framework 4.8 + WebView2）をビルドする。
   ツール本体と WebView2 の DLL は EXE の中に入る
3. dist/ に、現地写真整理ツール.exe（と操作マニュアル）と、それらを入れた GenchiPhotoTool.zip を作る。
   GitHub のリリースには日本語のファイル名を置けないので、配るのは ZIP（名前は英数字）にする
"""
import argparse
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
EXE = HERE / 'app' / 'bin' / 'Release' / 'net48' / 'GenchiPhotoTool.exe'
DIST = HERE / 'dist'
APP_NAME = '現地写真整理ツール.exe'
MANUAL_NAME = '現地写真整理ツール_操作マニュアル.docx'
ZIP_NAME = 'GenchiPhotoTool.zip'


class ShiftJisName(zipfile.ZipInfo):
    """ZIP の中の日本語のファイル名を Shift_JIS で書く。
    日本語の Windows のエクスプローラーで展開しても、名前が文字化けしないように。"""
    def _encodeFilenameFlags(self):
        return self.filename.encode('cp932'), self.flag_bits & ~0x800


def run(cmd, cwd):
    print('>', ' '.join(cmd))
    subprocess.run(cmd, cwd=cwd, check=True)


ap = argparse.ArgumentParser()
ap.add_argument('--manual', type=Path, help='ZIP に一緒に入れる操作マニュアル（.docx）')
args = ap.parse_args()

run([sys.executable, 'build_standalone.py'], HERE)
run(['dotnet', 'build', '-c', 'Release', '--nologo'], HERE / 'app')

if DIST.exists():
    shutil.rmtree(DIST)
DIST.mkdir()
files = [(EXE, APP_NAME)]
if args.manual:
    files.append((args.manual, MANUAL_NAME))
for src, name in files:
    shutil.copy2(src, DIST / name)
with zipfile.ZipFile(DIST / ZIP_NAME, 'w') as z:
    for src, name in files:
        info = ShiftJisName.from_file(src, name)
        info.compress_type = zipfile.ZIP_DEFLATED
        with open(src, 'rb') as f:
            z.writestr(info, f.read())
for f in sorted(DIST.iterdir()):
    print(f'dist/{f.name} を作りました（{f.stat().st_size // 1024} KB）')
