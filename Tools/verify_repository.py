"""Verify public repository structure and portfolio packaging without Unity dependencies."""
from pathlib import Path
import base64
import re
import struct
import subprocess
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def main():
    for rel in ['README.md', 'LICENSE', 'Packages/manifest.json', 'Packages/packages-lock.json',
                'ProjectSettings/ProjectVersion.txt', 'Assets/Scenes/Cocoon_OnboardingVR.unity',
                'docs/setup.md', 'docs/architecture.md', 'docs/interaction.md', 'docs/third-party-assets.md']:
        require((ROOT / rel).is_file(), 'Missing file: ' + rel)
    version = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text()
    require('2022.3.53f1' in version, 'Unexpected Unity version')

    files = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
    files = [x for x in files if x]
    require(files, 'No tracked files; run git add before validation')
    for rel in files:
        require(not rel.startswith(('Assets/JapaneseCity/', 'Library/', 'Temp/', 'Logs/', 'UserSettings/', 'Recordings/')), 'Excluded content tracked: ' + rel)
        require(not rel.endswith(('.keystore', '.jks', '.pfx', '.pem', '.mp4', '.apk')), 'Unexpected private or generated artifact: ' + rel)
        require((ROOT / rel).stat().st_size < 100 * 1024 * 1024 or rel.endswith('.obj'), 'Large non-model file: ' + rel)

    markdowns = [ROOT / 'README.md', *sorted((ROOT / 'docs').rglob('*.md'))]
    links = 0
    for doc in markdowns:
        text = doc.read_text(encoding='utf8')
        require(not re.search(r'[A-Z]:[/\\]', text), 'Local drive path in public documentation: ' + str(doc))
        require('github.com/Acerinka/Cocoon)' not in text, 'Old private repository link')
        targets = re.findall(r'\]\(([^)]+)\)|(?:src|href)="([^"]+)"', text)
        for pair in targets:
            target = next(s for s in pair if s).strip('<>')
            if target.startswith(('http://', 'https://', '#', 'mailto:')):
                continue
            path = urllib.parse.unquote(target.split('#', 1)[0])
            require((doc.parent / path).exists(), 'Broken link in ' + doc.name + ': ' + target)
            links += 1

    portfolio = ROOT / 'docs/portfolio'
    pages = sorted((portfolio / 'svg').glob('*.svg'))
    require(len(pages) == 9, 'Portfolio must have nine SVG pages')
    ns = {'s': 'http://www.w3.org/2000/svg'}
    text_nodes = 0
    for path in pages:
        root = ET.parse(path).getroot()
        require((root.get('width'), root.get('height'), root.get('viewBox')) == ('2560', '1440', '0 0 2560 1440'), 'Incorrect SVG canvas')
        images = root.findall('.//s:image', ns)
        require(len(images) == 1, 'Expected one embedded background per page')
        data = images[0].get('{http://www.w3.org/1999/xlink}href', '')
        require(data.startswith('data:image/png;base64,'), 'Background is not embedded PNG')
        raw = base64.b64decode(data.split(',', 1)[1])
        require(raw[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', raw[16:24]) == (3840, 2160), 'Background is not 4K')
        texts = root.findall('.//s:text', ns)
        require(texts and not root.findall('.//s:path', ns), 'Text must be editable, not outlines')
        for node in texts:
            require(node.get('font-family') == 'Inter', 'Unexpected English font')
            require(not re.search(r'[\u4e00-\u9fff]', ''.join(node.itertext())), 'Untranslated authored text')
        text_nodes += len(texts)
        require((portfolio / 'pages' / path.name.replace('.svg', '.jpg')).is_file(), 'Missing page preview')
    require(text_nodes == 243, 'Unexpected editable text count')
    cover = ET.parse(portfolio / 'svg/01-cover.svg').getroot()
    cover_links = {node.get('href') or node.get('{http://www.w3.org/1999/xlink}href')
                   for node in cover.findall('.//s:a', ns)}
    require(cover_links == {'https://github.com/Acerinka/Cocoon-Cabin',
                            'https://youtu.be/s00BhWtERXI',
                            'https://youtu.be/bH3W_9BMuKw'}, 'Missing or incorrect cover links')
    with zipfile.ZipFile(portfolio / 'Cocoon-Cabin-Figma-EN.zip') as archive:
        require(archive.testzip() is None, 'Corrupt Figma ZIP')
        for path in pages:
            require(archive.read('SVG_2K/' + path.name) == path.read_bytes(), 'Figma ZIP differs from SVG source')
    require((portfolio / 'Cocoon-Cabin-Portfolio-EN.pdf').read_bytes().startswith(b'%PDF-'), 'Missing or invalid PDF')
    print('PASS:', len(files), 'tracked files;', links, 'local documentation links; 9 SVG pages; 243 editable text objects; 3 cover links; embedded 4K backgrounds; matching Figma ZIP.')

if __name__ == '__main__':
    main()
