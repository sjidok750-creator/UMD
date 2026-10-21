"""Start PMStudio.Api.exe first. Python standard library only."""
import json
import sys
import urllib.error
import urllib.request

BASE = "http://127.0.0.1:18741"

def main():
    if len(sys.argv) > 1:
        with open(sys.argv[1], encoding="utf-8-sig") as handle:
            project = json.load(handle)
    else:
        with urllib.request.urlopen(BASE + "/v1/examples/rectangle", timeout=20) as response:
            project = json.load(response)
    body = json.dumps(project, ensure_ascii=False, allow_nan=False).encode("utf-8")
    request = urllib.request.Request(BASE + "/v1/analyze", data=body,
                                     headers={"Content-Type": "application/json; charset=utf-8"})
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        print(error.read().decode("utf-8"), file=sys.stderr)
        return 1
    print("Engine:", result["Engine"])
    print("Nominal P range (kN):", result["PMin"], result["PMax"])
    for demand in result["Demands"]:
        print(demand["Name"], demand["Status"], "fixed-P moment utilization:", demand["Utilization"])
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
