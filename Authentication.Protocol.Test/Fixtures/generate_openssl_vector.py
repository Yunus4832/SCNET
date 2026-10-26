"""Regenerate the public interoperability fixture; requires OpenSSL, no Python packages.
Private keys exist only in a TemporaryDirectory and are never included in the fixture.
"""
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile


def b64(data):
    return base64.urlsafe_b64encode(data).decode().rstrip('=')


def address(digest):
    alphabet = 'qpzry9x8gf2tvdw0s3jn54khce6mua7l'
    bits = ''.join(f'{value:08b}' for value in digest)
    bits += '0' * (-len(bits) % 5)
    symbols = [int(bits[index:index + 5], 2) for index in range(0, len(bits), 5)]
    prefix = 'scnet'
    checksum = 1
    values = [ord(char) >> 5 for char in prefix] + [0] + [ord(char) & 31 for char in prefix] + symbols + [0] * 6
    for value in values:
        top = checksum >> 25
        checksum = ((checksum & 0x1ffffff) << 5) ^ value
        for index, generator in enumerate([0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3]):
            if (top >> index) & 1:
                checksum ^= generator
    checksum ^= 0x2bc830a3
    return prefix + '1' + ''.join(alphabet[value] for value in symbols + [(checksum >> (5 * (5 - index))) & 31 for index in range(6)])


def run(*args):
    return subprocess.check_output(['openssl', *map(str, args)], stderr=subprocess.DEVNULL)


with tempfile.TemporaryDirectory(prefix='scnet-public-vector-') as temporary:
    root = Path(temporary)
    keys = {}
    for name in ['authority', 'subject', 'server']:
        path = root / (name + '.pem')
        run('genpkey', '-algorithm', 'EC', '-pkeyopt', 'ec_paramgen_curve:P-256', '-out', path)
        spki = run('pkey', '-in', path, '-pubout', '-outform', 'DER')
        assert spki[:27].hex() == '3059301306072a8648ce3d020106082a8648ce3d03010703420004'
        point = spki[-64:]
        jwk = {'crv': 'P-256', 'kty': 'EC', 'x': b64(point[:32]), 'y': b64(point[32:])}
        thumbprint = hashlib.sha256(json.dumps(jwk, separators=(',', ':'), sort_keys=True).encode()).digest()
        keys[name] = {'publicKey': b64(spki), 'identity': address(thumbprint), 'thumbprintHex': thumbprint.hex()}
    now = 1792840000
    nonce = b64(bytes(range(32)))
    binding = b64(bytes(range(32, 64)))
    header = {'alg': 'ES256', 'typ': 'scnet-admission+jwt', 'kid': keys['authority']['identity']}
    claims = {'iss': keys['authority']['identity'], 'aud': keys['server']['identity'], 'sub': keys['subject']['identity'], 'iat': now, 'nbf': now, 'exp': now + 120, 'jti': b64(bytes(range(64, 96))), 'purpose': 'join', 'nonce': nonce, 'cb': binding}
    signing_input = '.'.join(b64(json.dumps(item, separators=(',', ':')).encode()) for item in [header, claims])
    (root / 'input').write_bytes(signing_input.encode())
    signature = run('dgst', '-sha256', '-sign', root / 'authority.pem', root / 'input')
    assert signature[0] == 0x30 and signature[2] == 2
    length = signature[3]
    r = int.from_bytes(signature[4:4 + length], 'big')
    offset = 4 + length
    assert signature[offset] == 2
    s = int.from_bytes(signature[offset + 2:], 'big')
    token = signing_input + '.' + b64(r.to_bytes(32, 'big') + s.to_bytes(32, 'big'))
    vector = {'generator': run('version').decode().strip(), 'now': now, 'keys': keys, 'nonce': nonce, 'channelBinding': binding, 'expiresAt': now + 120, 'approval': token}
    Path(__file__).with_name('openssl-approval.json').write_text(json.dumps(vector, indent=2) + '\n')
