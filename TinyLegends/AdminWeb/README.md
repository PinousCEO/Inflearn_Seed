# IdleProject 우편 관리자

게임의 우편함(`USERS/{uid}` 문서의 `POST` 필드)에 운영자가 직접 우편을 넣는 페이지입니다.

```
AdminWeb/index.html    ← 이 파일 하나가 전부입니다
```

## 여는 방법

**`index.html`을 브라우저로 더블클릭.** 끝입니다.

서버도, Firebase SDK도, 인터넷 라이브러리도 쓰지 않습니다. Google REST API에 바로 `fetch` 합니다.

| 하는 일 | 엔드포인트 |
|---|---|
| 로그인 | `identitytoolkit.googleapis.com/v1/accounts:signInWithPassword` |
| 토큰 갱신 | `securetoken.googleapis.com/v1/token` |
| 유저 목록 | `GET firestore.googleapis.com/v1/…/documents/USERS` |
| 우편 발송 | `POST …/documents:commit` (`appendMissingElements`) |

두 API 모두 `file://` 출처(`Origin: null`)를 CORS로 허용하는 것을 확인했습니다.
Firebase 설정(apiKey · projectId)은 `Assets/google-services.json`에서 뽑아 페이지 안에 박아 뒀습니다.

## 처음 한 번만 — 로그인 계정과 관리자 권한

### ① 이메일 로그인 켜기

현재 이 프로젝트는 이메일/비밀번호 로그인이 꺼져 있습니다(`PASSWORD_LOGIN_DISABLED`).

1. Firebase 콘솔 → Authentication → Sign-in method → **이메일/비밀번호** 사용 설정
2. Users 탭 → 사용자 추가로 관리자 계정 하나 생성

### ② 관리자 권한 주기

지금 보안 규칙으로는 각자 자기 문서만 쓸 수 있어서 남의 우편함에 못 넣습니다.
페이지의 **"처음 한 번 해야 하는 설정"** 을 펼쳐 규칙을 복사한 뒤,

1. Firebase 콘솔 → Firestore Database → 규칙에 붙여 넣고 게시
2. 페이지에서 로그인 → 화면에 뜨는 **내 UID** 복사
3. 콘솔 → Firestore → `ADMINS` 컬렉션에 그 UID를 **문서 ID로 하는 빈 문서** 추가

이 문서가 있는 계정만 전체 유저를 읽고 우편을 넣을 수 있습니다.

## 쓰는 순서

1. **로그인** — 위에서 만든 관리자 계정
2. **유저 불러오기** — `USERS` 컬렉션을 300개씩 페이지 넘겨 전부 읽습니다
3. **대상 고르기**
   - 검색(이름·UID) / 직업 / 레벨 범위 / 우편 보유 여부로 거르기
   - 표에 보이는 순서 기준 **구간 선택**(예: 1~50번), 체크박스 **Shift+클릭**으로도 구간 선택
   - 발송 대상은 **전체 유저** 또는 **선택한 유저** 중에 고릅니다
4. **우편 작성** — 미리보기에 Firestore로 들어갈 값이 그대로 보입니다
5. **우편 보내기** — 한 번에 100명씩 묶어 커밋합니다. 묶음이 실패하면 한 명씩 다시 시도해 누가 실패했는지 기록에 남깁니다

유저 행을 클릭하면 그 사람의 현재 우편함을 열어 볼 수 있고, 잘못 보낸 우편은 거기서 지웁니다.

## 우편 형식

```js
{
  id: "admin-20260809-1130-a1b2", // 게임이 수령할 때 이 값으로 우편을 지목합니다
  title: "운영자 우편",
  body: "점검에 협조해 주셔서 감사합니다.",
  rewardType: "gold",             // none | gold | exp | item
  rewardItemId: "equipment-016",  // rewardType이 item일 때만
  rewardAmount: 50000,            // Firestore integer
  claimed: false,
  sentAt: <timestamp>,
  expiresAt: <timestamp>          // 없으면 만료되지 않습니다
}
```

`id`는 비워 두지 마세요. 게임은 수령한 우편을 id로 지목해 지웁니다.

## 아이템 목록 다시 만들기

보상 아이템 자동완성 목록은 Unity의 `ItemCatalog` / `CharacterCatalog`에서 뽑아
`index.html` 안 `/* CATALOG:BEGIN */ … /* CATALOG:END */` 블록에 넣어 둔 값입니다.
아이템을 추가했다면 **프로젝트 루트에서** 아래를 돌리면 그 블록만 갈아 끼웁니다.

```bash
python - <<'EOF'
import re, json, glob
TYPES=['Equipment','Consumable','Material','Currency','Quest','Other']
RAR=['Common','Uncommon','Rare','Epic','Legendary']
def dec(s):
    s=s.strip()
    try: return json.loads(s) if s.startswith('"') else s
    except Exception: return s.strip('"')
items, chars = [], []
for p in glob.glob('Assets/**/*.asset', recursive=True):
    t=open(p,encoding='utf-8',errors='replace').read()
    m=re.search(r'^  itemId: (\S+)\s*$', t, re.M)
    if not m: continue
    n=re.search(r'^  displayName: (.*)$', t, re.M)
    ty=re.search(r'^  itemType: (\d+)$', t, re.M)
    r=re.search(r'^  rarity: (\d+)$', t, re.M)
    items.append({'id':m.group(1),'name':dec(n.group(1)) if n else '',
                  'type':TYPES[int(ty.group(1))] if ty else 'Other',
                  'rarity':RAR[int(r.group(1))] if r else 'Common'})
for p in glob.glob('Assets/00_Data/Characters/character-*.asset'):
    t=open(p,encoding='utf-8',errors='replace').read()
    m=re.search(r'^  characterId: (\S+)\s*$', t, re.M)
    n=re.search(r'^  displayName: (.*)$', t, re.M)
    if m: chars.append({'id':m.group(1),'name':dec(n.group(1)) if n else ''})
items.sort(key=lambda x:x['id']); chars.sort(key=lambda c:c['id'])
block  = ['/* CATALOG:BEGIN */','var CATALOG = {','  characters: [']
block += ['    %s,' % json.dumps(c, ensure_ascii=False) for c in chars]
block += ['  ],','  items: ['] + ['    %s,' % json.dumps(i, ensure_ascii=False) for i in items]
block += ['  ]','};','/* CATALOG:END */']
html = open('AdminWeb/index.html', encoding='utf-8').read()
new, n = re.subn(r'/\* CATALOG:BEGIN \*/.*?/\* CATALOG:END \*/', '\n'.join(block), html, flags=re.S)
assert n == 1
open('AdminWeb/index.html','w',encoding='utf-8').write(new)
print('items', len(items), '| characters', len(chars))
EOF
```

## 알아 둘 점

- 랭킹은 `RANKING/{uid}` 컬렉션에 따로 쌓입니다(`name` · `level` · `power` · `formulaVersion` · `updatedAt`).
  게임이 전투력을 계산해 올리며, 규칙은 `Assets/00_Scripts/Core/CombatPower.cs` 한 곳에 있습니다.
  이 컬렉션 규칙도 위 "규칙 복사"에 들어 있으니 함께 게시하세요.
- 발송은 `appendMissingElements`(= arrayUnion)라 기존 우편을 건드리지 않고 한 통만 덧붙입니다.
- 게임 쪽 수령은 트랜잭션으로 처리해서, 유저가 접속 중일 때 보낸 우편도 사라지지 않습니다.
- 젬(다이아) 보상은 게임에 재화 자체가 없어서 넣을 수 없습니다. `gold` / `exp` / `item`만 됩니다.
- 로그인 토큰은 탭을 닫으면 사라집니다(`sessionStorage`). 발송 기록과 저장한 양식은 이 브라우저에만 남습니다.
- Google 계정 로그인은 팝업 방식이라 `file://`에서 쓸 수 없어 뺐습니다. 이메일/비밀번호만 씁니다.
