# 사운드 시스템

배경음악(BGM)은 `Resources/Sounds`의 음원을 씬에 맞춰 자동으로 틀고,
효과음(SFX)은 **코드로 합성**합니다. 나중에 실제 음원을 넣으면 코드를 고치지 않고 그대로 교체됩니다.

톤은 **밝고 가벼운 캐주얼**입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `AudioRoot.cs` | 사운드 컴포넌트가 올라앉을 `SoundRoot`를 마련합니다. |
| `BgmManager.cs` | 씬에 맞춰 배경음악을 틀고 크로스페이드로 넘깁니다. |
| `SfxId.cs` | 효과음 목록(61종). 이름이 곧 리소스 경로입니다. |
| `SfxSynth.cs` | 발진기 · 필터 · 엔벨로프 · 잔향 같은 최소한의 DSP 도구입니다. |
| `SfxLibrary.cs` | 효과음별 합성 레시피와 재생 설정(음량 · 피치 흔들림 · 최소 간격)입니다. |
| `AudioManager.cs` | 효과음 재생기. 채널 24개를 돌려쓰며 2D · 3D 재생을 처리합니다. |
| `UiSfxBinder.cs` | 씬의 모든 `Button`에 공통 클릭음을 자동으로 붙입니다. |

## SoundRoot

**소리는 전부 씬의 `SoundRoot` 하나로만 납니다. 오브젝트를 더 만들지 않습니다.**

```
SoundRoot          ← Title 씬에 있는 그것 (컴포넌트 3개가 여기 붙습니다)
 ├ BGM_Source      ← 배경음악 전용, loop
 └ SFX_Source      ← 효과음 전용, PlayOneShot으로 겹쳐 재생
```

- 씬에 `SoundRoot`가 있으면 **그것을 그대로 씁니다.** 두 소스도 씬에 있는 것을 그대로 씁니다.
- **인스펙터에서 정해 둔 음량을 기본값으로 물려받습니다.** (BGM 0.5 / SFX 1.0)
- `SoundRoot`는 `DontDestroyOnLoad`로 씬을 넘어 살아남습니다.
  Select · Main에는 `SoundRoot`가 없지만 Title에서 넘어온 것이 계속 일합니다.
- 어느 씬에도 없으면(Main부터 바로 실행하는 경우 등) 그때만 같은 이름으로 만듭니다.
- 다른 씬이 자기 `SoundRoot`를 또 들고 오면, 쓰이지 않으므로 그쪽을 치웁니다.

효과음이 겹쳐 나는 것은 `AudioSource.PlayOneShot`이 처리합니다. 채널을 여럿 둘 필요가 없습니다.
소스가 2D 하나뿐이라 3D 위치로는 거리감이 생기지 않으므로,
`PlayAt`은 카메라와의 거리만큼 **음량을 줄여서** 멀리서 난 소리가 작게 들리게 합니다.

**저장된 음량 설정(PlayerPrefs)이 있으면 인스펙터 값보다 그쪽이 우선입니다.**
인스펙터 값은 "처음 켰을 때의 기본값" 역할입니다.

## 배경음악

씬 이름을 보고 알아서 곡을 고릅니다.

| 씬 | 곡 |
| --- | --- |
| Title, Select | `Resources/Sounds/Title_BGM` |
| Main | `Resources/Sounds/Main_BGM` |

Title → Select는 같은 곡이라 **끊기지 않고 이어집니다**. Main으로 넘어갈 때만 0.8초 크로스페이드로 바뀝니다.

```csharp
BgmManager.Volume = 0.5f;          // 0~1, PlayerPrefs에 저장됨
BgmManager.Muted  = true;
BgmManager.Play("Main_BGM");       // 직접 지정하고 싶을 때
BgmManager.Stop();
```

곡을 더 추가하려면 `Resources/Sounds`에 넣고 `BgmManager.ApplySceneTrack`의 씬 매칭만 고치면 됩니다.

> 두 파일은 2분짜리 22MB WAV라 *Decompress On Load*로 두면 메모리에 40MB 넘게 올라갑니다.
> **Streaming + Vorbis(품질 0.7)** 로 임포트 설정을 바꿔 두었습니다.
> 원래대로 돌리려면 인스펙터에서 Load Type을 바꾸면 됩니다.

## 효과음

```csharp
using IdleBattle.Audio;

AudioManager.Play(SfxId.LevelUp);                     // 화면 전체에 같은 크기로
AudioManager.PlayAt(SfxId.EnemyHit, enemy.position);  // 월드 좌표에서(거리에 따라 작아짐)

AudioManager.MasterVolume = 0.8f;   // 0~1, PlayerPrefs에 저장됨
AudioManager.SfxVolume  = 0.7f;
AudioManager.Muted      = true;
```

### 길이 상한

합성 효과음은 **모두 1초를 넘지 않습니다**(`SfxLibrary.MaxSeconds = 0.95초`).
길면 다음 소리와 겹쳐 화면이 답답해지기 때문에, 레시피가 더 길어도 `SfxSynth.Limit`이 끝을 부드럽게 잘라 냅니다.

### 음색 규칙 — 밝은 동화풍, 심플하고 신비롭게

악기는 **오르골 · 유리종 · 마림바 · 숨결** 네 가지만 씁니다.

| 재료 | 쓰는 곳 |
| --- | --- |
| `MusicBox` | 기본 음색. 보상 · 레벨업 · 팡파르 |
| `Glass` | 반짝이는 쪽. 마법 · 전설 등급 · 동전 |
| `Marimba` | 톡 치는 쪽. 클릭 · 타격 |
| `Round` | 낮은 울림. 착지 · 피격의 몸통 |

핵심은 두 가지입니다.

1. **배음마다 사그라지는 속도를 다르게 줍니다** (`SfxSynth.Bell`).
   진짜 종은 높은 배음이 먼저 죽고 기본음만 남아서, "팅" 하고 밝게 시작해 맑게 풀립니다.
   모든 배음에 같은 엔벨로프를 걸면 이 변화가 없어 **전자음처럼 납작하게** 들립니다.
2. **거의 모든 소리에 울림을 겁니다** (`SfxSynth.Reverb`).
   병렬 콤 4개 + 직렬 올패스 2개. 신비로운 인상은 대부분 여기서 나옵니다.

그 밖에:
- 음정은 **도-레-미-솔-라(장음계 5음)** 만. 무엇이 겹쳐도 불협이 생기지 않습니다.
- 살짝 어긋난 사본을 겹쳐(디튠) 소리에 폭을 줍니다.

### 전투에서 반복되는 소리는 음정을 빼야 합니다

**가장 중요한 규칙입니다.** 쉬지 않고 반복되는 소리에 음정이 있으면,
효과음이 아니라 **박자에 맞춰 울리는 딸랑거림**으로 들립니다.

| 소리 | 빈도 | 반드시 |
| --- | --- | --- |
| `Footstep` | 0.33초마다 (계속 걸어다님) | 잡음만 |
| `EnemyHit` | 초당 최대 10회 이상 | 잡음 + 순식간에 훑고 내려가는 저음 |
| `WeaponDraw` · `WeaponSheathe` | 공격 한 번에 두 번 | 잡음만 |
| `EnemyAttack` | 마리마다 1.1초 | 잡음만 |
| `PlayerHurt` | 맞을 때마다 | 잡음 + 짧은 저음 |

저음을 넣더라도 **한 음에 머물게 두면 안 됩니다.** 끝까지 아래로 훑고 지나가면서
머물기 전에 사그라지도록 짧게 끊어야 음정이 남지 않습니다.

종·오르골 같은 음정 있는 소리는 **가끔 나는 사건**에만 씁니다
(보상 · 레벨업 · 판넬 열기 · 스테이지 이동).

### 공격 스킬만 예외 — 타격감

캐릭터는 스킬을 쓸 때 무기를 뽑아 휘두릅니다(`CharacterEquipmentPresenter.DrawWeapon`).
그래서 공격 5종만은 종소리가 아니라 **베기와 내려찍기**로 만듭니다.

| 재료 | 하는 일 |
| --- | --- |
| `Swing` | 공기를 가르는 소리. 칼끝이 가장 빨라지는 후반부에 힘이 실립니다(`SfxSynth.Swell`) |
| `Cut` | 베어 낼 때 스치는 짧고 날카로운 앞머리 |
| `Slam` | 땅을 내려찍는 몸통. 낮은 음이 뚝 떨어지고 흙먼지가 함께 터집니다 |

**소리는 `SkillData.animationIndex`(0~4)로 고릅니다.** 스킬 목록의 순서가 아닙니다.
그 번호가 곧 재생되는 애니메이션(애니메이터의 `Skill1`~`Skill5`)이자
`Character/Skill0`의 이펙트 번호(1-1~1-4)라서,
스킬 구성이 바뀌어도 화면에 보이는 동작과 소리가 항상 함께 갑니다.

4번(`SkillBattleRoar`)만은 예외입니다. `Character/Skill0`에 VFX가 없어
`01_Prefabs/Effects/Row` 프리팹을 발밑에 띄우고, 모든 몬스터를 다섯 번 칩니다.
그래서 소리도 한 번이 아니라 **다섯 번 두드립니다.**

> `Resources/Data/Skills`의 파일 이름(lightning · ice · meteor · beam · bomb)은
> 내용과 전혀 다릅니다. 실제로는 전부 바바리안 근접 · 범위기입니다. **이름 말고 데이터를 보세요.**

| anim | 스킬 | 데이터 | 소리 |
| --- | --- | --- | --- |
| 0 | Savage Cleave | 피해 40 · 시전 0.30 | 단숨에 베는 한 방 (450ms, 저역 9%) |
| 1 | Ground Smash | 피해 50 · 시전 0.35 | 땅 강타 + 돌 파편 (620ms, 저역 48%) |
| 2 | Leap Crush | 피해 80 · 시전 0.50 | 도약 → 착지 강타 + 돌·진동 (820ms, 저역 64%) |
| 3 | Earthshatter | 피해 90 · 시전 0.20 | 넓게 갈라지는 땅 (920ms, 저역 70%) |

피해량이 클수록 길고 무겁게(저역 9% → 70%), 시전 시간이 길수록 준비 동작을 길게 잡았습니다.
왜곡은 걸지 않아 무겁되 거칠지는 않습니다.

### 음원 넣기

음원은 **BGM과 같은 `Assets/Resources/Sounds/` 폴더**에 넣습니다.
파일 이름을 **`SfxId`와 똑같이** 맞추면 그대로 재생됩니다.

```
Assets/Resources/Sounds/Title_BGM.wav      ← 배경음악
Assets/Resources/Sounds/Main_BGM.wav
Assets/Resources/Sounds/EnemyHit.wav       ← 효과음
Assets/Resources/Sounds/LootLegendary.wav
Assets/Resources/Sounds/TitleTap.wav
```

파일이 없는 효과음만 코드로 합성해 대신 냅니다.
넣은 것부터 하나씩 실제 음원으로 바뀌므로, 61개를 한 번에 채울 필요는 없습니다.

### 소리가 몰리지 않게 하는 장치

타격음처럼 초당 수십 번 터지는 소리는 그대로 두면 뭉개집니다.
`SfxLibrary.GetProfile`에 효과음별 **최소 간격**을 두어, 그 안에 다시 들어온 요청은 흘려보냅니다.
같은 소리가 기계적으로 들리지 않도록 재생마다 **피치를 조금씩 흔듭니다**.

### 새 효과음 추가하기

1. `SfxId`에 이름을 추가합니다.
2. `SfxLibrary.Build`의 `switch`에 레시피를 한 줄 추가합니다. `Pop` · `Ding` · `Tick` · `Sparkle` · `Swish` · `Fanfare`를 조합하면 톤이 유지됩니다.
3. 필요하면 `GetProfile`에 음량 · 최소 간격을 지정합니다(없으면 기본값을 씁니다).
