import urllib.request

try:
    req = urllib.request.Request('http://127.0.0.1:8000/logs/analyze', method='POST', data=b'', headers={'Content-Type': 'application/json'})
    urllib.request.urlopen(req)
except Exception as e:
    print(e.read().decode())
