#!/usr/bin/env python3
"""外部の部品がいらない版（部品入り）を作る。

現地調査写真整理ツール.html が cdnjs から読み込んでいる Leaflet と JSZip を、
vendor/ に置いたファイルの中身に置き換えて、1 つの HTML にまとめる。
ツールを直したら、このスクリプトを実行して部品入りの版も作り直す。

    python3 build_standalone.py
"""
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / '現地調査写真整理ツール.html'
OUT = HERE / '現地調査写真整理ツール_部品入り.html'
CDN = 'https://cdnjs.cloudflare.com/ajax/libs'

LEAFLET_NOTICE = """/* Leaflet 1.9.4 https://leafletjs.com
 * (c) 2010-2023 Vladimir Agafonkin, (c) 2010-2011 CloudMade
 * BSD-2-Clause License https://github.com/Leaflet/Leaflet/blob/main/LICENSE */
"""


def vendor(name):
    text = (HERE / 'vendor' / name).read_text(encoding='utf-8')
    if '</script' in text or '</style' in text:
        raise SystemExit(f'{name} に </script> などが含まれていて、そのままでは HTML に入れられません')
    return text


def replace_once(html, old, new):
    if html.count(old) != 1:
        raise SystemExit(f'置き換える場所が見つかりません: {old}')
    return html.replace(old, new)


html = SRC.read_text(encoding='utf-8')
html = replace_once(html, f'<link rel="stylesheet" href="{CDN}/leaflet/1.9.4/leaflet.min.css">',
                    '<style>\n' + vendor('leaflet.min.css') + '\n</style>')
html = replace_once(html, f'<script src="{CDN}/leaflet/1.9.4/leaflet.min.js"></script>',
                    '<script>\n' + LEAFLET_NOTICE + vendor('leaflet.min.js') + '\n</script>')
html = replace_once(html, f'<script src="{CDN}/jszip/3.10.1/jszip.min.js"></script>',
                    '<script>\n' + vendor('jszip.min.js') + '\n</script>')
if CDN in html:
    raise SystemExit('まだ外部の部品を読み込む場所が残っています')
OUT.write_text(html, encoding='utf-8')
print(f'{OUT.name} を作りました（{OUT.stat().st_size // 1024} KB）')
