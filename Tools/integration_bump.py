"""Version of an optional library on its integration branch: what is installed, what is the latest, and how to update.
Used by .github/workflows/integration-watch.yml (and by hand).

  python Tools/integration_bump.py current <lib>     installed version on this checkout
  python Tools/integration_bump.py latest <lib>      latest release
  python Tools/integration_bump.py apply <lib> <v>   install version v (manifest; R3 also its NuGet DLL)

lib: unitask | r3 | uieffect"""
import io, json, os, re, sys, urllib.request, zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
MANIFEST = os.path.join(ROOT, 'Packages', 'manifest.json')
R3_PLUGINS = os.path.join(ROOT, 'Assets', 'Plugins', 'R3')

LIBS = {
    'unitask': {'package': 'com.cysharp.unitask', 'repo': 'Cysharp/UniTask',
                'url': 'https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#{v}'},
    'r3': {'package': 'com.cysharp.r3', 'repo': 'Cysharp/R3',
           'url': 'https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#{v}'},
    'uieffect': {'package': 'com.coffee.ui-effect', 'openupm': True},
}


def get(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'uianim-integration-watch'})
    token = os.environ.get('GITHUB_TOKEN')
    if token and 'api.github.com' in url: req.add_header('Authorization', 'Bearer ' + token)
    with urllib.request.urlopen(req, timeout=60) as r: return r.read()


def manifest():
    with open(MANIFEST, encoding='utf-8') as f: return json.load(f)


def current(lib):
    value = manifest()['dependencies'].get(LIBS[lib]['package'])
    if value is None: return ''
    return value.rsplit('#', 1)[1] if '#' in value else value


def latest(lib):
    info = LIBS[lib]
    if info.get('openupm'):
        data = json.loads(get('https://package.openupm.com/' + info['package']))
        return data['dist-tags']['latest']
    return json.loads(get(f"https://api.github.com/repos/{info['repo']}/releases/latest"))['tag_name']


def apply(lib, version):
    info = LIBS[lib]
    m = manifest()
    m['dependencies'][info['package']] = info['url'].format(v=version) if 'url' in info else version
    if info.get('openupm'):
        regs = m.setdefault('scopedRegistries', [])
        scope = info['package'].rsplit('.', 1)[0]   # com.coffee
        reg = next((r for r in regs if r['url'] == 'https://package.openupm.com'), None)
        if reg is None: regs.append({'name': 'OpenUPM', 'url': 'https://package.openupm.com', 'scopes': [scope]})
        elif scope not in reg['scopes']: reg['scopes'].append(scope)
    with open(MANIFEST, 'w', encoding='utf-8') as f: json.dump(m, f, indent=2); f.write('\n')
    if lib == 'r3': r3_dll(version.lstrip('v'))


def r3_dll(version):
    """R3's core is a NuGet package: its netstandard2.1 DLL goes in Assets/Plugins/R3 (the .meta files stay)."""
    data = get(f'https://api.nuget.org/v3-flatcontainer/r3/{version}/r3.{version}.nupkg')
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        dll = z.read('lib/netstandard2.1/R3.dll')
    os.makedirs(R3_PLUGINS, exist_ok=True)
    with open(os.path.join(R3_PLUGINS, 'R3.dll'), 'wb') as f: f.write(dll)


if __name__ == '__main__':
    cmd, lib = sys.argv[1], sys.argv[2]
    if lib not in LIBS: sys.exit('unknown lib: ' + lib)
    if cmd == 'current': print(current(lib))
    elif cmd == 'latest': print(latest(lib))
    elif cmd == 'apply': apply(lib, sys.argv[3])
    else: sys.exit('unknown command: ' + cmd)
