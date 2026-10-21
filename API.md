# PM Studio + Local API 0.5

## 시작

`PMStudio.Api.exe`를 실행하거나 Windows 앱의 API 버튼을 누릅니다. 같은 포트로 두 개를 동시에 실행하면 포트 충돌 오류가 납니다. 기본 포트 18741, loopback IPv4만 사용합니다.

Python 표준 라이브러리 예제:

```text
python api-example.py
python api-example.py examples/rectangle.pmproj.json
```

PowerShell 호출:

```powershell
$base = 'http://127.0.0.1:18741'
$project = Invoke-RestMethod "$base/v1/examples/rectangle"
$json = $project | ConvertTo-Json -Depth 30 -Compress
$result = Invoke-RestMethod "$base/v1/analyze" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json))
$result.Demands | Format-Table Name, Status, Utilization
```

## 요청

`POST /v1/analyze`, `Content-Type: application/json`, UTF-8 본문. 완전한 요청 예제는 `examples/rectangle.pmproj.json`입니다. `GET /v1/examples/rectangle`도 같은 스키마를 반환합니다.

주요 필드:

| 필드 | 의미 |
|---|---|
| Format / Version | `PMStudio.Project` / `1.0` |
| Model | `RC-RectangularBlock-Nominal-v1` |
| Units | `mm-MPa-kN-kNm` |
| Material | Fc, Fy, Es, EpsCu, Alpha, Beta |
| Contours | Hole 불리언과 Points 배열. 점은 X,Y |
| Bars | X,Y,Area. 원형 철근 1개씩 |
| Loads | Name,P,Mx,My. 동일 조합의 동시값 |
| AngleCount | 24~144, 4의 배수. 하중 검토는 이 값과 2배 값 비교 |
| DepthCount | 24~180, 3D 표시용 깊이 분할 |
| SliceP | 결과 Slice에 넣을 축력 |

한 요청 최대 2 MB, 최대 40조합·300철근·800꼭짓점(윤곽별 최대 512개), 외곽 1개·개구부 8개입니다. 추가 계산량 제한도 있어 모든 최대값을 동시에 사용할 수는 없습니다. 동시에 처리할 수 있는 연결은 2개이며 초과 연결은 닫습니다.

## 응답

- `DisplayRings`: 3D 표시면용 축력별 절단점 배열. 표시 분할을 바꿔도 원래 계산 결과는 바뀌지 않습니다.
- `Limits`: YieldStrain, BalancedMx/BalancedMy, PureBendingMx/PureBendingMy. 기준별 φ/압축·인장지배 판정은 미적용입니다.
- `Curves`: 방향별 c 샘플과 P,Mx,My. **Depth=-1, BlockDepth=-1은 균일압축의 무한 c**를 뜻합니다. Depth=0은 균일인장 끝점입니다.
- `PMxPaths`, `PMyPaths`: 반대축 모멘트가 0인 축별 경로. 비대칭 단면에서 균일압축/인장 끝점의 반대축 모멘트가 0이 아니면 그 끝점은 경로에 포함되지 않을 수 있습니다.
- `Slice`: SliceP에서의 2×AngleCount 방향 절단점. 축력 범위 밖이면 빈 배열입니다.
- `Demands`: `InsideNominal`, `OutsideNominal`, `Unresolved`. 규정상 OK/NG가 아닙니다.
- `Utilization`: 고정 P 모멘트 거리비. 정의할 수 없는 경우 null.
- `AngularChange`: 조합별 N/2N 사용률 상대 변화의 최대값. 다른 오차의 추정값이 아닙니다.
- `PMin`, `PMax`, `GrossArea`, `SteelArea`, `Cx`, `Cy`, `Notes`: 모델과 단위/기준점 안내.

점별 철근 응력은 DLL의 `Engine.Evaluate(project, angleDeg, depthMm, true)`로 조회합니다. 이 저수준 메서드 전에는 `Engine.Validate(project)`를 호출해야 합니다. HTTP의 별도 점 조회 엔드포인트는 아직 없습니다.

오류: 400 입력·JSON 오류, 403 Host/Origin 제한, 404 엔드포인트 없음, 413 본문 크기 초과, 415 Content-Type 오류, 422 평형 미수렴, 500 내부 오류. 오류 본문은 `{ "error": "설명" }`입니다.

HTTP/1.1, Content-Length가 필요하며 chunked 요청은 지원하지 않습니다. Host는 `127.0.0.1:포트` 또는 `localhost:포트`여야 합니다. 브라우저 Origin 요청은 거절합니다. 로컬 Python/C#/PowerShell/CLI 연결용입니다.

## 현재 예제 기준값

600×800 mm, D25 12개(면적 π×25²/4를 각 철근에 사용), Fc=30 MPa, Fy=400 MPa, Es=200000 MPa, α=β=0.85, εcu=0.003:

- Ag = 480000 mm²
- As = 5890.58622548086 mm²
- Pmax = 14445.9870914426 kN
- Pmin = −2356.19449019234 kN
- ULS-01 고정 P 모멘트 사용률 ≈ 0.527888539

위 값은 이 프로그램의 지정 공칭 모델에 대한 회귀 기준이며 특정 설계기준의 허용내력이 아닙니다. `api-example.py`는 기존 서버에 실제 계산을 요청하고 결과를 화면에 출력합니다.

## 0.3 보고서 데이터
Project.ReductionProfile 기본 Nominal. 검산 전용 Reference-ACI31819-Tied 선택 가능. Project.BarDisplacement 기본 ExactDisk, 공개 예제용 CenterLumped 지원.
Analysis.ReportDiagrams는 Pmax/절대 Mxmax/절대 Mymax 조합의 같은 P·Mx·My, MomentAngle(도), Paths, Balanced/PureBending/AtDemand를 반환합니다. ReducedPaths/ReducedAtDemand는 명시적으로 선택한 참고 계수 결과입니다. ReducedIntersectionCount>1은 미확정이며, 모멘트 비율을 설계 적합 인증으로 해석하지 않습니다.

## 0.5 원형/편람 검산
CircularRadius=0: 기존 다각형. 양수: 개구부 없는 등분 원형 윤곽을 확인하여 원호 압축영역 적분. 좌표로 중심을 정의합니다. ReductionProfile에 Reference-KR2008-Tied 추가(예제의 Fc24/Fy300/Es200000/εcu0.003/αβ0.85 조건). φc=.70, φf=.85, 축력상한 .8φcP0와 낮은 축력의 보간을 해당 예제 검산에 사용합니다. 편람과의 평형편심 차이 때문에 Reports.Summary의 참고 판정은 편람 대조 미확정입니다.


0.5: ReductionProfile=Reference-ACI31819-Spiral은 공개 원형 예제의 참고 검산입니다. φc=0.75, φt=0.90, 축력상한0.85×φc×P0입니다. 요청의 Rebar.Area는 mm²의 직접 면적이며, 국내 호칭으로 추정해 재계산하지 않습니다.
