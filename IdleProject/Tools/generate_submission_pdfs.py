from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, ListFlowable, ListItem,
)


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Docs" / "Submission"
OUT.mkdir(parents=True, exist_ok=True)

FONT_REGULAR = Path("C:/Windows/Fonts/malgun.ttf")
FONT_BOLD = Path("C:/Windows/Fonts/malgunbd.ttf")
if not FONT_REGULAR.exists():
    FONT_REGULAR = ROOT / "Assets" / "04_Fonts" / "NotoSansKR-Black.ttf"
if not FONT_BOLD.exists():
    FONT_BOLD = FONT_REGULAR

pdfmetrics.registerFont(TTFont("Korean", str(FONT_REGULAR)))
pdfmetrics.registerFont(TTFont("KoreanBold", str(FONT_BOLD)))

NAVY = colors.HexColor("#17233B")
BLUE = colors.HexColor("#2E5BFF")
GOLD = colors.HexColor("#D5A63D")
PALE = colors.HexColor("#F2F5FA")
INK = colors.HexColor("#202633")
MUTED = colors.HexColor("#687083")
WARN = colors.HexColor("#FFF0D5")


def styles():
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle("Title", parent=base["Title"], fontName="KoreanBold", fontSize=25,
                                leading=34, textColor=NAVY, alignment=TA_CENTER, spaceAfter=8 * mm),
        "subtitle": ParagraphStyle("Subtitle", parent=base["Normal"], fontName="Korean", fontSize=11,
                                   leading=18, textColor=MUTED, alignment=TA_CENTER, spaceAfter=7 * mm),
        "h1": ParagraphStyle("H1", parent=base["Heading1"], fontName="KoreanBold", fontSize=16,
                             leading=22, textColor=NAVY, spaceBefore=6 * mm, spaceAfter=3 * mm),
        "h2": ParagraphStyle("H2", parent=base["Heading2"], fontName="KoreanBold", fontSize=11.5,
                             leading=17, textColor=BLUE, spaceBefore=4 * mm, spaceAfter=2 * mm),
        "body": ParagraphStyle("Body", parent=base["BodyText"], fontName="Korean", fontSize=9.2,
                               leading=15, textColor=INK, spaceAfter=2.2 * mm, wordWrap="CJK"),
        "small": ParagraphStyle("Small", parent=base["BodyText"], fontName="Korean", fontSize=7.7,
                                leading=11.5, textColor=MUTED, wordWrap="CJK"),
        "quote": ParagraphStyle("Quote", parent=base["BodyText"], fontName="Korean", fontSize=8.4,
                                leading=14, textColor=NAVY, backColor=PALE, borderColor=colors.HexColor("#CAD5E8"),
                                borderWidth=.5, borderPadding=8, spaceBefore=2 * mm, spaceAfter=3 * mm,
                                wordWrap="CJK"),
        "warning": ParagraphStyle("Warning", parent=base["BodyText"], fontName="KoreanBold", fontSize=8.4,
                                  leading=14, textColor=colors.HexColor("#7A4A00"), backColor=WARN,
                                  borderColor=GOLD, borderWidth=.7, borderPadding=8, spaceAfter=3 * mm,
                                  wordWrap="CJK"),
    }


S = styles()


def header_footer(canvas, doc, label):
    canvas.saveState()
    w, h = A4
    canvas.setStrokeColor(colors.HexColor("#D8DEEA"))
    canvas.line(18 * mm, h - 14 * mm, w - 18 * mm, h - 14 * mm)
    canvas.setFont("Korean", 7.5)
    canvas.setFillColor(MUTED)
    canvas.drawString(18 * mm, h - 10.5 * mm, label)
    canvas.drawRightString(w - 18 * mm, 10 * mm, f"{doc.page}")
    canvas.restoreState()


def document(path, label):
    doc = BaseDocTemplate(str(path), pagesize=A4, rightMargin=18 * mm, leftMargin=18 * mm,
                          topMargin=20 * mm, bottomMargin=17 * mm,
                          title=label, author="Tiny Legends 1인 개발팀")
    frame = Frame(doc.leftMargin, doc.bottomMargin, doc.width, doc.height, id="normal")
    doc.addPageTemplates(PageTemplate(id="main", frames=frame,
                                      onPage=lambda c, d: header_footer(c, d, label)))
    return doc


def p(text, style="body"):
    return Paragraph(text, S[style])


def bullets(items):
    return ListFlowable([ListItem(p(item), leftIndent=4 * mm) for item in items],
                        bulletType="bullet", start="circle", leftIndent=6 * mm,
                        bulletFontName="Korean", bulletFontSize=7)


def table(rows, widths=None, header=True, small=False):
    data = [[p(str(cell), "small" if small else "body") for cell in row] for row in rows]
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0, hAlign="LEFT")
    commands = [
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), .35, colors.HexColor("#CCD3DF")),
        ("LEFTPADDING", (0, 0), (-1, -1), 5), ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 5), ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]
    if header:
        commands += [("BACKGROUND", (0, 0), (-1, 0), NAVY),
                     ("TEXTCOLOR", (0, 0), (-1, 0), colors.white)]
        for cell in data[0]:
            cell.style = ParagraphStyle("TableHead", parent=cell.style, fontName="KoreanBold",
                                        textColor=colors.white)
    t.setStyle(TableStyle(commands))
    return t


def build_game_pdf():
    out = OUT / "03_게임_소개_및_설명_Tiny_Legends.pdf"
    story = [Spacer(1, 18 * mm), p("Tiny Legends", "title"),
             p("끝없이 강해지는 영웅과 함께 지옥의 전장을 돌파하는 모바일 방치형 액션 RPG", "subtitle"),
             table([
                 ["항목", "내용"],
                 ["장르", "모바일 방치형 액션 RPG / 자동 전투 / 성장"],
                 ["플랫폼", "Android (세로 화면)"],
                 ["개발 환경", "Unity 6000.3.13f1 · Universal Render Pipeline 17.3.0"],
                 ["지원 언어", "한국어 · 일본어 · 영어"],
                 ["개발 형태", "1인 개발"],
             ], [35 * mm, 120 * mm]),
             Spacer(1, 5 * mm), p("한 줄 소개", "h1"),
             p("영웅이 스스로 전투하고 성장하는 과정을 지켜보며 장비와 스킬 조합으로 더 높은 스테이지에 도전하는 방치형 RPG입니다."),
             PageBreak(), p("1. 게임 개요", "h1"),
             p("플레이어는 영웅과 이름을 선택한 뒤 전투에 진입합니다. 영웅은 가까운 몬스터를 자동으로 추적하고 기본 공격과 스킬을 사용합니다. 몬스터 처치로 경험치·골드·장비를 얻고, 장비와 스킬을 정비해 점점 강해지는 적과 보스를 상대합니다."),
             p("핵심 경험", "h2"),
             bullets([
                 "직접 조작 부담 없이 진행되는 자동 이동·자동 전투",
                 "일반 전투 라운드와 보스전이 이어지는 단계형 스테이지",
                 "등급별 장비 획득·착용과 전투력 성장",
                 "레벨에 따라 해금되는 스킬과 구간별 선택",
                 "접속하지 않은 시간도 성장으로 환산하는 오프라인 보상",
                 "게스트 또는 Google 계정 로그인과 Firebase 기반 저장",
             ]),
             p("2. 게임 방법", "h1"),
             p("목표", "h2"),
             p("몬스터 웨이브와 보스를 처치해 다음 스테이지로 계속 전진하는 것이 목표입니다. 장비·레벨·스킬을 성장시켜 더 강한 적을 상대하고 자신의 최고 진행 기록과 전투력을 높입니다."),
             p("조작", "h2"),
             table([
                 ["화면/기능", "조작 방법"],
                 ["전투", "이동·타깃 선택·기본 공격·스킬 사용은 자동으로 진행됩니다."],
                 ["Equipment", "획득 장비를 확인하고 아이템을 눌러 착용 여부를 결정합니다."],
                 ["Skill", "스킬 정보를 확인하고 성장 구간별 스킬을 선택·강화합니다."],
                 ["Dungeon", "던전 종류와 보상·입장 횟수를 확인합니다."],
                 ["Shop", "무료 상품과 성장용 상품 목록을 확인합니다."],
                 ["하단 메뉴", "아이콘을 터치해 전투·장비·스킬·던전·상점 화면을 전환합니다."],
             ], [35 * mm, 120 * mm]),
             p("종료 조건", "h2"),
             p("고정된 최종 엔딩이 없는 지속 진행형 게임입니다. 체력이 0이 되면 일시적으로 쓰러진 뒤 부활하여 전투를 계속합니다. 플레이 종료는 모바일 기기에서 앱을 닫거나 백그라운드로 전환하는 방식이며, 진행 데이터는 계정에 저장됩니다."),
             PageBreak(), p("3. 설치 및 실행 방법", "h1"),
             p("Android APK 설치", "h2"),
             bullets([
                 "아래 플레이 링크에서 APK 파일을 Android 기기로 내려받습니다.",
                 "기기 설정에서 해당 브라우저 또는 파일 관리자의 ‘알 수 없는 앱 설치’를 한시적으로 허용합니다.",
                 "APK를 실행해 설치한 뒤 Tiny Legends 아이콘을 누릅니다.",
                 "인터넷 연결 상태에서 게스트 또는 Google 로그인을 선택합니다.",
                 "처음 플레이하는 계정은 영웅과 이름을 선택한 뒤 ‘모험 시작’을 누릅니다.",
             ]),
             p("요구 환경", "h2"),
             table([
                 ["항목", "요구 사항"],
                 ["운영체제", "Android 7.1(API 25) 이상"],
                 ["화면", "세로 모드 권장 / 다양한 비율의 Safe Area 대응"],
                 ["네트워크", "로그인·클라우드 저장을 위한 인터넷 연결 필요"],
                 ["권한", "별도 카메라·마이크·위치 권한 불필요"],
             ], [38 * mm, 117 * mm]),
             p("제출 링크", "h1"),
             p("제출 전 아래 세 항목을 실제 공개 링크로 교체해야 합니다. 링크는 심사 종료 시점까지 접근 가능해야 합니다.", "warning"),
             table([
                 ["구분", "링크"],
                 ["플레이 / APK", "[제출 전 입력] https://____________________________"],
                 ["GitHub 저장소", "https://github.com/PinousCEO/Inflearn_Seed"],
                 ["플레이 영상", "[제출 전 입력] https://youtube.com/________________"],
             ], [38 * mm, 117 * mm]),
             p("4. 플레이 흐름", "h1"),
             p("앱 실행 → 로그인 → 저장 캐릭터 확인 → 신규 계정은 영웅 선택 → Main 전투 → 몬스터/보스 처치 → 경험치·골드·장비 획득 → Equipment/Skill에서 성장 → 더 높은 스테이지 반복"),
             p("데이터 안내", "h2"),
             p("Firebase Authentication과 Cloud Firestore를 사용합니다. 게스트 계정은 앱 데이터 삭제·로그아웃 시 동일 계정 복구가 어려울 수 있으므로 심사용 계정은 설치 상태를 유지하거나 Google 로그인을 사용하는 것을 권장합니다."),
             p("제출 전 체크", "h1"),
             bullets([
                 "APK 링크를 외부 계정에서도 다운로드할 수 있는지 확인",
                 "YouTube 영상이 비공개가 아닌 공개 또는 일부 공개인지 확인",
                 "GitHub 저장소 권한과 Git LFS 파일 다운로드 가능 여부 확인",
                 "실제 Android 기기에서 로그인부터 Main 진입까지 최종 확인",
             ])]
    document(out, "게임 소개 및 설명 · Tiny Legends").build(story)
    return out


def build_ai_pdf():
    out = OUT / "04_AI_활용_기술_문서_Tiny_Legends.pdf"
    story = [Spacer(1, 16 * mm), p("AI 활용 기술 문서", "title"),
             p("Tiny Legends · Unity 기반 방치형 RPG의 AI 협업 구조, 프롬프트 및 검증 방식", "subtitle"),
             table([
                 ["항목", "내용"],
                 ["프로젝트", "Inflearn_Seed / IdleProject (Tiny Legends)"],
                 ["엔진", "Unity 6000.3.13f1 · URP 17.3.0"],
                 ["AI 도구", "Claude Code, OpenAI Codex, Unity AI Assistant / Unity MCP"],
                 ["작성 기준일", "2026-08-10"],
                 ["개발 형태", "1인 개발 · AI 보조 활용"],
             ], [38 * mm, 117 * mm]),
             PageBreak(), p("1. AI 활용 개요", "h1"),
             p("본 프로젝트에서 AI는 단순 코드 자동완성이 아니라 저장소를 조사하고, 코드를 수정하고, Unity 에디터에서 결과를 직접 검증하는 개발 에이전트로 사용되었습니다. 개발자는 기획·에셋 선정·플레이 감각·최종 의사결정을 담당하고 AI 결과를 반복 검수했습니다."),
             table([
                 ["도구", "주요 활용"],
                 ["Claude Code (Anthropic)", "초기·주요 시스템 구현, 리팩터링, 디버깅, 데이터/에디터 도구 생성"],
                 ["OpenAI Codex", "후속 기능 수정, 모바일 호환성 감사, Unity MCP 기반 런타임 검증, 제출 문서 생성"],
                 ["Unity AI Assistant / MCP", "AI가 Unity 내부 C# 실행, 씬·에셋 조회/수정, 콘솔 로그 및 렌더 상태 검증"],
             ], [48 * mm, 107 * mm]),
             p("사람과 AI의 역할 구분", "h2"),
             table([
                 ["개발자(사람)", "AI"],
                 ["게임 기획, 재미 판단, 에셋 선정, 실제 플레이 테스트, 결과 승인·반려",
                  "C# 구현, 에디터 자동화, 데이터 생성, 오류 추적, 검증 코드 실행, 문서 초안"],
             ], [77.5 * mm, 77.5 * mm]),
             p("2. 기술 구조", "h1"),
             p("자연어 지시 → 저장소/기존 구현 조사 → 코드 또는 에디터 자동화 작성 → Unity MCP로 컴파일·씬 값·콘솔·렌더 결과 확인 → 실패 시 원인 수정 → 개발자 보고 및 플레이 검수의 순환 구조입니다."),
             p("Unity MCP 검증 범위", "h2"),
             bullets([
                 "에디터 내부 C# 컴파일 및 실행",
                 "씬 오브젝트·컴포넌트·직렬화 참조 검사",
                 "런타임 오브젝트 생성과 UI 렌더 상태 확인",
                 "Unity Console 오류·경고 및 스택 추적 회수",
                 "Android 빌드 전 쉐이더·Resources·씬 구성 검사",
             ]),
             p("재현 가능한 에디터 자동화", "h2"),
             p("대규모 UI와 데이터는 YAML을 직접 편집하기보다 ShopPanelBuilder, EquipmentPanelBuilder, LocalizationBinder, CharacterAnimatorSkillSetup 등의 Editor 도구를 생성해 다시 실행 가능한 형태로 관리했습니다."),
             PageBreak(), p("3. AI 활용 구현 사례", "h1"),
             table([
                 ["영역", "AI 활용 내용"],
                 ["전투", "자동 타깃·이동·공격·스킬 연계, 웨이브/보스, 피격·드롭 연출 구현 및 오류 수정"],
                 ["데이터", "ScriptableObject 기반 캐릭터·스킬·아이템·스테이지 규칙 생성 및 검증"],
                 ["UI", "Equipment·Skill·Dungeon·Shop 계층 생성, Safe Area와 다양한 화면비 대응"],
                 ["저장/로그인", "Firebase 익명·Google 인증, Firestore 저장 흐름과 실패 분기 보강"],
                 ["로컬라이제이션", "한국어·일본어·영어 3개 언어를 단일 CSV로 관리하고 씬 키를 자동 배선"],
                 ["사운드", "SfxSynth에서 파형·잡음·필터·포락선을 조합해 효과음 61종을 코드로 절차적 합성"],
                 ["모바일 검증", "중복 AudioListener, Missing Script, Safe Area, shader stripping, Resources 경로를 자동 감사"],
             ], [36 * mm, 119 * mm], small=True),
             p("대표적인 검증 사례", "h2"),
             bullets([
                 "데미지 텍스트가 생성되지만 보이지 않는 문제를 런타임에서 직접 강제 호출해 Canvas 정렬 문제로 특정",
                 "저장 조회 예외가 신규 계정으로 오인되어 Select로 이동하는 분기를 성공/없음/실패의 3상태로 분리",
                 "빌드에서만 Resources.Load가 실패할 프리팹 경로를 실제 Resources 구조로 이동하고 빌드 전 검증기 추가",
                 "모바일 씬의 중복 AudioListener와 끊어진 Missing Script 슬롯을 전수 검사해 제거",
             ]),
             p("4. 주요 프롬프트 및 지시 사항", "h1"),
             p("상시 규칙", "h2"),
             bullets([
                 "기존 구현과 계층을 먼저 조사하고 같은 규칙을 따를 것",
                 "주석은 동작 설명보다 해당 구현을 선택한 이유를 남길 것",
                 "UI 레이아웃과 기능 동작을 구분하고 불필요한 런타임 스크립트를 늘리지 않을 것",
                 "모바일 설명 문구는 짧게 유지하고 번역은 단일 CSV에서 관리할 것",
                 "밸런스 변경은 근거 수치를 제시한 뒤 적용할 것",
                 "작업 후 Unity 컴파일·콘솔·실제 런타임 상태를 직접 확인할 것",
             ]),
             p("프롬프트 예시 1 · 기존 규칙 확장", "h2"),
             p("“Skill5 애니메이션을 기존처럼 연결하고, Effects/Row 프리팹을 실행해 화면의 모든 몬스터에게 5차례 피해를 주게 해줘.”", "quote"),
             p("핵심은 ‘기존처럼’이라는 제약입니다. AI가 기존 스킬의 Animator 파라미터, Animation Event, 데이터 구조를 먼저 조사한 뒤 동일한 패턴으로 확장하도록 했습니다."),
             p("프롬프트 예시 2 · 증상 기반 디버깅", "h2"),
             p("“쿨타임이 남아 있는데 T-Pose가 나오는 원인을 찾아서 절대 나오지 않게 해줘.”", "quote"),
             p("원인을 미리 단정하지 않고 증상과 기대 결과를 제공해 AI가 코드·Animator·에셋 참조를 함께 조사하도록 했습니다."),
             p("프롬프트 예시 3 · 모바일 호환성", "h2"),
             p("“게임 자체의 변경점이 없는 범위에서 모바일 실행 시 문제가 될 부분을 직접 파악하고 수정해.”", "quote"),
             p("게임 규칙과 연출은 유지하고 Android 설정, 씬 중복, Safe Area, Resources, 쉐이더, 입력과 비동기 저장을 감사하도록 범위를 정의했습니다."),
             PageBreak(), p("5. 외부 에셋 및 오픈소스", "h1"),
             p("엔진·SDK·오픈소스", "h2"),
             table([
                 ["이름", "용도", "라이선스/출처"],
                 ["Unity / URP / TMP / Input System", "엔진·렌더링·UI·입력", "Unity Terms 및 공식 패키지 라이선스"],
                 ["Firebase Unity SDK 13.14.0", "Auth·Firestore", "Apache License 2.0 · github.com/firebase/firebase-unity-sdk"],
                 ["Google Sign-In for Unity", "Google 인증", "Apache License 2.0"],
                 ["External Dependency Manager", "Android 의존성", "Apache License 2.0"],
                 ["Noto Sans KR / JP", "한국어·일본어 폰트", "SIL Open Font License 1.1 · github.com/notofonts/noto-cjk"],
                 ["Mixamo animations", "플레이어 애니메이션", "Adobe ID 기반 개인·상업·비영리 프로젝트 로열티 프리 사용 안내"],
             ], [42 * mm, 41 * mm, 72 * mm], small=True),
             p("Unity Asset Store 에셋", "h2"),
             table([
                 ["에셋", "제작자", "사용처"],
                 ["Suntail Village", "Raygeas", "전투 배경 환경"],
                 ["Mummy Monster", "amusedART", "몬스터 모델·애니메이션"],
                 ["Modern UI Pack v5.5.28", "Michsky", "UI 컴포넌트·애니메이션"],
                 ["Casual RPG / Environment VFX", "Lana Studio", "스킬·환경 이펙트"],
                 ["Hyper Casual FX Vol.1·2", "Kyeoms (VFX_Klaus)", "타격·획득 연출"],
                 ["Top Down Effects", "SineVFX", "스킬 범위 연출"],
                 ["Wooden Shield Pack", "Korzanowski", "방패 모델"],
             ], [61 * mm, 43 * mm, 51 * mm], small=True),
             p("위 유료·무료 에셋은 Unity Asset Store를 통해 취득했으며 Unity Asset Store EULA를 따릅니다. 에셋 원본을 독립적으로 재배포하지 않고 게임 빌드의 구성 요소로만 사용합니다."),
             p("사운드", "h2"),
             table([
                 ["항목", "출처 및 상태"],
                 ["효과음 61종", "외부 음원 없이 프로젝트 코드 SfxSynth가 런타임 생성"],
                 ["Title_BGM.wav / Main_BGM.wav", "프로젝트에는 파일만 있고 원작자·취득 경로·라이선스 기록이 없음 — 제출 전 반드시 확인"],
             ], [48 * mm, 107 * mm]),
             p("저작권 제출 전 필수 확인", "h1"),
             p("BGM 2곡의 원작자, 곡명, 취득 URL 또는 구매 내역, 허용 라이선스를 확인해 이 문서의 사운드 표를 교체해야 합니다. 확인할 수 없다면 해당 BGM을 사용 권한이 명확한 음원으로 교체해야 합니다.", "warning"),
             p("공식 참고 링크", "h2"),
             bullets([
                 "Unity Asset Store Terms: https://unity.com/legal/as-terms",
                 "Firebase Unity SDK: https://github.com/firebase/firebase-unity-sdk",
                 "Noto CJK: https://github.com/notofonts/noto-cjk",
                 "Adobe Mixamo FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html",
                 "프로젝트 저장소: https://github.com/PinousCEO/Inflearn_Seed",
             ]),
             p("6. 한계와 책임", "h1"),
             p("AI가 생성한 코드는 개발자가 검토하고 Unity 에디터 및 실제 플레이로 확인했습니다. AI 결과는 오류 가능성이 있으므로 최종 제출·배포 책임은 개발자에게 있습니다. 외부 저작물의 라이선스는 취득 당시 계정과 구매 내역을 기준으로 최종 재확인해야 합니다."),
             p("1인 개발 프로젝트이므로 별도의 팀원 롤 기술서는 제출하지 않습니다.", "quote")]
    document(out, "AI 활용 기술 문서 · Tiny Legends").build(story)
    return out


if __name__ == "__main__":
    for generated in (build_game_pdf(), build_ai_pdf()):
        print(generated)
