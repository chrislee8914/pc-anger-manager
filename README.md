# PC분노관리 (PC Anger Manager)

작업 표시줄 트레이에서 작은 동물이 달리는 Windows 앱입니다.
PC가 바쁠수록(CPU·GPU·메모리 사용률이 높을수록) 더 빨리 달립니다.

## 다운로드

[Releases](../../releases/latest) 페이지에서 둘 중 하나를 받으세요.

| 파일 | 설명 |
|---|---|
| `PcAngerManager-portable.exe` (약 50MB) | 설치 없이 아무 Windows 10/11 (64비트) PC에서 바로 실행. 메모리를 약 130MB 사용 |
| `PcAngerManager.exe` (약 0.3MB) | 메모리 약 60MB로 더 가벼움. 대신 [.NET 10 데스크톱 런타임](https://dotnet.microsoft.com/download/dotnet/10.0)을 먼저 설치해야 함 |

받은 exe를 실행하면 트레이(작업 표시줄 오른쪽 아래)에 캐릭터가 나타납니다.
처음 실행할 때 Windows가 "알 수 없는 게시자" 경고를 띄울 수 있습니다 → "추가 정보" → "실행".

## 기능

- **달리는 캐릭터**: 토끼 / 고양이 / 말. 사용률에 따라 4단계로 속도가 바뀜
  - 0~25%: 5fps, 25~50%: 10fps, 50~75%: 20fps, 75% 이상: 최대 FPS 설정값
- **기분 단계**: 사용률에 따라 모습이 바뀜
  - 60% 미만: 파란 **천사 토끼** / 60~90%: 노란 **토끼** (러너가 토끼일 때)
  - 90% 이상: 빨간 **악마 토끼** (어떤 러너든)
- **왼쪽 클릭**: PC 상태 (CPU, GPU, 메모리, 온도, 저장 장치, 네트워크). 열려 있으면 5초마다 갱신
- **오른쪽 클릭**: 러너 선택, 커스텀 러너, 설정, 정보, 작업 관리자, 종료
- **마우스 올리기**: 속도 기준 값과 온도
- **커스텀 러너**: 직접 그린 PNG(최대 32x32px) 여러 장으로 나만의 캐릭터 만들기. 끌어다 놓아 추가, 끌어서 순서 변경
- **화면 배율 대응**: 모니터 배율(100%, 150% 등)에 맞춰 아이콘을 선명하게
- **설정**: 테마(아이콘 색), 달리기 속도 기준(CPU/GPU/메모리), 최대 FPS(10/20/30/40), 온도 단위, 윈도우 시작 시 자동 실행
- **가벼움**: 평소 CPU 단일 코어의 1% 미만, 메모리 약 60MB

설정과 커스텀 러너는 `%APPDATA%\PcAngerManager\`에 저장됩니다.

## 직접 빌드하기

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)가 필요합니다.

```powershell
dotnet run                                  # 바로 실행
dotnet run -- --preview                     # 캐릭터 확대 미리보기 창과 함께 실행 (개발용)
dotnet publish -c Release -o dist           # 작은 exe (.NET 런타임 필요)
dotnet publish -c Release -o dist-portable -p:SelfContained=true -p:EnableCompressionInSingleFile=true   # 설치 없이 실행되는 exe
```

### 캐릭터 그림 고치기

[Sprites.cs](Sprites.cs)에 16x16 글자 그림으로 들어 있습니다. `#` = 칠한 칸, `.` = 빈칸.
`dotnet watch run -- --preview`로 실행해 두면 고치고 저장하는 즉시 미리보기 창과 트레이에 반영됩니다.
