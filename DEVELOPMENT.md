# 다른 컴퓨터에서 개발하기

Windows 10/11, Git, Windows PowerShell 5.1 및 .NET Framework 4.x의 C# 컴파일러가 필요합니다. Python 3은 독립 검증과 API 호출 예제에 사용하며 앱 실행 자체에는 필요하지 않습니다. 외부 NuGet 패키지는 없습니다.

```powershell
git clone https://github.com/sjidok750-creator/UMD.git
cd UMD
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\PMStudio.exe
```

`PMStudio.exe`와 `PMStudio.Core.dll`은 같은 폴더에 두세요. 저장소에는 소스를 보관하며 실행 파일은 빌드 시 생성합니다. GitHub Actions가 성공하면 해당 실행의 Artifacts에서도 실행 파일 묶음을 받을 수 있습니다.

## 수정과 동기화

이 폴더를 편집기 또는 Codex의 프로젝트로 열어 작업합니다. 기존 폴더에서는 새 작업 전 `git pull --ff-only`로 최신 변경을 받습니다. 충돌이 있으면 기존 변경을 보존하고 해결한 뒤 진행합니다.

```powershell
# 수정 후 전체 재검증 (Python 3 필요)
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify-all.ps1
git status
git add <수정한파일>
git commit -m "변경 내용"
git push
```

푸시에는 본인 GitHub 계정의 저장소 쓰기 권한과 Git 인증이 필요합니다. 토큰은 코드나 프로젝트 파일에 넣지 마세요. 개인 작업 입력은 Git에서 제외되는 `local-projects/` 폴더에 보관할 수 있습니다.

## 소스 구성

| 영역 | 주요 파일 |
|---|---|
| 데이터/철근 규격/치수 입력 | Model.cs, RebarCatalog.cs, SectionRecipe.cs |
| 단면 적분/평형/방향별 내력/계수 | Geometry.cs, SectionSolver.cs, Engine.cs, DirectionalSolver.cs, Reduction.cs |
| Windows UI/그림 입력 | App.cs, GuidedSectionEditor.cs, SectionForms.cs, Theme.cs |
| 그래프/보고서 | Plots.cs, Dashboard.cs, ReportSheet.cs, Reports.cs |
| 로컬 HTTP API | ApiServer.cs, ApiMain.cs, API.md, api-example.py |
| 검증 | *Tests.cs, verify.ps1, independent_check.py, additional_independent_check.py |

`build.ps1`이 계산 DLL → UI → API 순으로 빌드합니다. 새 소스 파일을 추가하면 이 스크립트의 해당 컴파일 목록에도 추가해야 합니다. 현재 Visual Studio 솔루션이 아닌 스크립트 빌드 프로젝트입니다.

## 검증과 남은 한계

`verify-all.ps1`은 빌드, C# 검사, 별도 Python 수치 비교를 실행합니다. 생성 자료는 Git에서 제외되는 `artifacts/`에 저장합니다. Python 실행 파일을 지정하려면 `-Python 'C:\경로\python.exe'`를 전달하세요. `verify.ps1`만 실행하면 C# 검사만 실행하며 기본 출력은 임시 폴더입니다.

Windows 자동 검증은 공식 [checkout](https://github.com/actions/checkout), [setup-python](https://github.com/actions/setup-python), [upload-artifact](https://github.com/actions/upload-artifact)의 v7 사용 예를 따릅니다. 로컬 전체 검증과 GitHub 실행 결과는 구분하며, GitHub Actions의 실제 통과 여부는 업로드 후 Actions 화면에서 확인합니다.

기존 루트의 `verification.txt` 등은 0.8 배포 시점의 기록이며, 현재 소스의 새 실행 결과는 `artifacts/verification.log`와 비교 CSV로 확인합니다. 검사 통과는 설계기준 인증이나 모든 입력의 정확성을 보증하지 않습니다.

편람 2008 예제의 균형편심 차이 약 5.87%는 미확정 상태입니다. 비대칭 벽체의 공개 원문 순수휨 값과의 잔여 차이도 있습니다. `MANUAL-2008-VALIDATION.md`, `VALIDATION-0.5.md`, `VALIDATION-0.7.md`의 가정·출처·차이를 유지하고, 수치를 맞추기 위해 근거 없이 재료값이나 공식을 변경하지 마세요. 현재 모드는 일반 RC 단면 모델 및 명시된 참고 계수 모드이며 완전한 KDS 부재 설계 프로그램은 아닙니다.
