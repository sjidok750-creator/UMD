# P–M 도표의 형태와 검산 근거

사용자 참고 그림의 숫자·규정·색상 명칭을 원문으로 확인한 것은 아니다. 따라서 그 그림의 검정/파랑선을 특정 코드로 단정하지 않는다. 아래는 일반적인 두 곡선 도표의 의미와 다른 공개 예제로 확인한 관계이다.

1. 한쪽 그림은 한 휨 방향을 표시한다. 반대 방향은 따로 계산해야 한다. 비대칭 형상·배근을 양 방향 대칭으로 복사하지 않는다. 원점 아래의 인장 P 구간도 있을 수 있다.
2. 통상 공칭선과 계수 적용선을 겹쳐 표시한다. 평가에는 적용 기준의 계수·축력 제한을 반영한 경계가 필요하다. 공칭선은 저감 전 내력과 저감량 설명에 유용하다. 두 선이 있다는 이유만으로 판정이 완성되는 것은 아니다.
3. 원점에서 나가는 선은 M/P 일정인 편심선이다. 요구점·균형점 등 연결한 점에 따라 뜻이 다르므로 이름을 붙인다. 수평선은 같은 P에서 요구와 내력을 비교하고, 수직선은 M 좌표를 읽는 보조선이다. 편심선 교점의 비례 하중배율과 고정 P의 M 내력/요구 비율을 혼동하지 않는다.

프로그램은 검정 Pn–Mn, 선택한 참고 모드의 파랑 φPn–φMn, 빨강 하중 편심선·균형 편심선·축력 비교선을 별도 데이터로 그린다. 계산점만 직선 연결하며 미해결 구간을 연결하지 않는다. 한쪽 그래프의 M은 실제 조합 ψ 방향 성분의 양의 크기이고, 음 방향 조합은 ψ+180°의 다른 내력을 계산한다.

## 외부 검산 예제

원문: StructurePoint (2022), Interaction Diagram – Tied Reinforced Concrete Column Design Strength (ACI 318-19), 표 1 PDF 29쪽(문서 본문 p.26).
https://structurepoint.org/publication/pdf/Interaction-Diagram-Tied-Reinforced-Concrete-Column-Design-Strength-ACI-318-19.pdf

원문: StructurePoint (2022), Interaction Diagram – Tied Reinforced Concrete Column with High-Strength Reinforcing Bars (ACI 318-19), 표 1 PDF 29쪽(본문 p.26).
https://structurepoint.org/publication/pdf/Interaction-Diagram-Tied-Reinforced-Concrete-Column-with-High-Strength-Reinforcing-Bars-ACI-318-19.pdf

공통 입력: 16×16 in, fc′=5000 psi, Es=29,000,000 psi, 상·하부 각 4개 #9(각 1 in²), 철근 중심 이격 2.5 in. Grade 60/80의 fy=60,000/80,000 psi. 단위를 mm·MPa로 정확 환산했다. CenterLumped 공제 모델로 원문과 맞췄다. 부재 세장비는 제외한 단면 검산이다.

검산점: 축력상한과 순압축·순인장, fs=0/0.5fy/fy, εt=εy+0.003, 순수휨. 원문 spColumn 표의 반올림값과 비교했다. 허용오차는 축력 0.051 kip, 모멘트 0.011 kip·ft로 인쇄된 정밀도에 맞췄다. 원문 계산 도중의 반올림값을 재사용하지 않았다. 실제 비교값은 verification.txt의 REFERENCE 항목에 남겼다.

참고 계수 구현의 기술 근거: spColumn Technical Manual, solution methods, φ와 곡선 그림 및 비정상 분기 설명. 이 매뉴얼은 폭이 좁아지는 단면에서 φ 변화로 계수 적용 축력이 비단조가 될 수 있으며, 비대칭 양축 절단에서는 단순 P–M 투영만으로 판단하면 안 된다고 설명한다. 본 개발판은 복수 교점을 미확정으로 남긴다. 상용 프로그램의 처리 알고리즘을 동일 구현했다고 주장하지 않는다.
https://structurepoint.org/pdfs/manuals/spcolumn-manual.pdf

공개 예제는 선의 관계를 검증하기 위한 별도 입력이다. 이 예제의 ACI 계수를 일반 프로젝트/KDS 기준으로 자동 전환하지 않는다. 실제 구조물의 기준별 전체 검토와 상용 실행 결과 대조는 별도 과제이다.

## 내부 독립 확인

- 직사각형 폐형식과 비대칭 배근의 반대 방향을 비교.
- 각 곡선점의 축력 및 모멘트 방향 평형, 균형 철근 항복, 순수휨 평형을 확인.
- 적응 세분화 종료와 별도 1/4 위치 표본의 실제 재계산을 비교. 표본 구간의 오차 검사는 전 구간 오차 보증이 아니다.
- 표의 Mn/φMn이 각각 그려진 Cn/Cr과 일치하는지 확인.
- 오목부가 있는 가상 교각 예제로 같은 조합 세 값과 방향 비교를 확인.
- ExactDisk와 CenterLumped 모델 차이는 로그에 수치로 기록. Grade 60 순수휨에서 차이가 있으며 이를 숨겨 원문 일치를 주장하지 않는다.


## 0.5 추가 검증
원형 8-#10 나선철근 예제와 16×20 in 직사각형 이축 예제를 추가했다. 공개 계산표와 독립 Python 적분/연립해 대조 및 철근 호칭 면적 수정은 VALIDATION-0.5.md에 상세 기록했다. 편람 평균깊이 진단을 실제 최외단 항복점으로 바꾸어 사용하지 않는다.
