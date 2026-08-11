# Tiny Legends Addressables 정리

## 구조

모든 게임 런타임 콘텐츠는 `Assets/AddressableContent`로 이동했고, 기존 Resources 상대 경로를 주소로 유지했다. 따라서 호출부는 문자열 변경 없이 `AddressableContent`를 통해 로드한다.

| 그룹 | 개수 | 라벨 | 내용 |
| --- | ---: | --- | --- |
| Content-Audio | 2 | `content.bgm` | Title/Main BGM |
| Content-Characters | 2 | `content.characters` | 플레이어, 적 프리팹 |
| Content-Data | 7 | `content.items`, `content.stage`, `content.skills` | ItemCatalog, StageData, SkillData 5개 |
| Content-Effects | 16 | `content.effects` | 전투, 드롭, 장비 효과 프리팹 |
| Content-Localization | 1 | `content.localization` | Localization CSV |
| Content-UI | 4 | `content.ui` | 데미지, 아이템 설명, 팝업 프리팹 |

총 32개 엔트리이며 모두 Local Build/Load Path와 `Pack Together`를 사용한다.

## 주소 규칙

- 확장자를 제거한 프로젝트 상대 경로를 사용한다.
- 예: `Data/ItemCatalog`, `Data/StageData`, `01_Prefabs/Effects/Hit`, `UI/One_Line_Popup`
- BGM은 기존 주소인 `Sounds/Title_BGM`, `Sounds/Main_BGM`을 유지한다.
- 복수 로드는 주소가 아니라 라벨을 사용한다. 현재 `SkillData` 목록이 `content.skills`를 사용한다.

## 런타임 로딩

`AddressableContent.Load<T>(address)`와 `LoadAll<T>(label)`이 핸들을 캐시한다. 같은 자산을 여러 시스템에서 요청해도 중복 로드하지 않으며 플레이 세션 종료 시 핸들을 해제한다.

현재 동기 API를 유지하기 위해 `WaitForCompletion()`을 사용한다. 원격 배포 또는 WebGL로 전환할 때는 초기 부트스트랩에서 비동기 프리로드하는 구조로 변경해야 한다.

## Resources에 남긴 항목

- `BillingMode.json`: Unity Purchasing 설정
- `XboxCloudSettings.asset`: 플랫폼 설정
- `Runtime/RuntimeLit.mat`: 런타임 생성 머티리얼의 URP/Lit 셰이더가 빌드에서 제거되지 않게 하는 보존 자산

`SfxLibrary`의 Resources 조회는 선택적 외부 SFX 오버라이드용이다. 파일이 없으면 코드가 효과음을 합성하므로 Addressables 이전 대상이 아니다.

## 유지보수

새 동적 콘텐츠는 적절한 `Content-*` 그룹에 넣고 위 주소/라벨 규칙을 따른다. 씬과 프리팹에 직렬화된 직접 참조 자산은 Addressable 엔트리로 중복 등록하지 않는다. 콘텐츠 빌드는 Unity의 `Window > Asset Management > Addressables > Groups`에서 `Build > New Build > Default Build Script`로 생성한다.
