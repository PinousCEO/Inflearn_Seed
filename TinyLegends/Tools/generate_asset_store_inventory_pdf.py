from pathlib import Path
import re
from collections import defaultdict

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether
)

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
OUTPUT = ROOT / "Docs" / "TinyLegends_Unity_AssetStore_Assets.pdf"

PUBLISHERS = {
    "134212": "amusedART",
    "201717": "Michsky",
    "203303": "Raygeas",
    "206800": "Lana Studio",
    "231405": "Kyeoms",
    "239285": "Lana Studio",
    "245262": "Kyeoms",
    "323684": "SineVFX",
    "359602": "Korzanowski",
}

CATEGORIES = {
    "134212": "3D 캐릭터",
    "201717": "UI / GUI 툴킷",
    "203303": "3D 환경",
    "206800": "VFX",
    "231405": "VFX",
    "239285": "VFX",
    "245262": "VFX",
    "323684": "VFX",
    "359602": "3D 소품",
}

DESCRIPTIONS = {
    "134212": "미라 몬스터 모델, 텍스처, 머티리얼, 애니메이터와 테스트 씬.",
    "201717": "메뉴, 버튼, 진행 표시줄 등 현대적인 게임 UI 구성요소와 편집 도구.",
    "203303": "스타일라이즈드 판타지 마을의 건물, 자연물, 지형, 머티리얼과 데모 환경.",
    "206800": "토네이도·날씨·자연 현상을 표현하는 파티클 기반 환경 VFX.",
    "231405": "하이퍼 캐주얼 게임용 범용 파티클 효과 모음 Vol.1.",
    "239285": "캐주얼 RPG의 전투·마법·상태 표현용 파티클 효과 모음.",
    "245262": "하이퍼 캐주얼 게임용 범용 파티클 효과 모음 Vol.2.",
    "323684": "탑다운 시점 전투용 공격, 폭발, 방벽, 실드 등의 효과 모음.",
    "359602": "로우 폴리 나무 방패 모델 4종과 프리팹, 머티리얼, 텍스처.",
}

LINKS = {
    "134212": "https://assetstore.unity.com/packages/3d/characters/free-mummy-monster-134212",
    "201717": "https://assetstore.unity.com/packages/tools/gui/modern-ui-pack-201717",
    "203303": "https://assetstore.unity.com/packages/3d/environments/fantasy/suntail-stylized-fantasy-village-203303",
    "206800": "https://assetstore.unity.com/packages/vfx/particles/environment/environment-weather-nature-vfx-pack-206800",
    "231405": "https://assetstore.unity.com/packages/vfx/particles/hyper-casual-fx-pack-vol-1-231405",
    "239285": "https://assetstore.unity.com/packages/vfx/particles/casual-rpg-vfx-239285",
    "245262": "https://assetstore.unity.com/packages/vfx/particles/hyper-casual-fx-pack-vol-2-245262",
    "323684": "https://assetstore.unity.com/packages/vfx/particles/spells/top-down-effects-2-0-323684",
    "359602": "https://assetstore.unity.com/packages/3d/props/weapons/low-poly-3d-wooden-shield-pack-359602",
}

USAGES = {
    "134212": "전투에 등장하는 미라 적 캐릭터 모델로 사용 (Enemy.prefab).",
    "201717": "메인 화면과 캐릭터 선택 화면의 버튼·패널 등 UI 구성에 사용 (Main, Select 씬).",
    "203303": "캐릭터 선택 화면의 판타지 마을 배경과 환경 오브젝트로 사용 (Select 씬).",
    "206800": "전설 장비 효과인 모래·눈 토네이도 공격에 사용 (Tornado_sand, Tornado_snow).",
    "231405": "바바리안 스킬과 전투 타격·지진·직선 공격 이펙트에 사용 (Skill 1-1/1-3/1-4, Hit, EarthQuake, Row).",
    "239285": "아이템 등급별 드롭·획득, 스턴·감속 오라·몬스터 영역, 스킬 1-2 및 목표 방향 화살표에 사용.",
    "245262": "전투 타격 이펙트 Hit의 일부 파티클과 머티리얼에 사용.",
    "323684": "에셋은 임포트되어 있으나 현재 게임 씬·프리팹에서 직접 사용되지 않음.",
    "359602": "플레이어 캐릭터가 장착한 나무 방패 모델로 사용 (Character.prefab).",
}

def scan():
    records = defaultdict(lambda: {"names": set(), "versions": set(), "uploads": set(), "paths": []})
    patterns = {
        "id": re.compile(r"^\s*productId:\s*(\d+)\s*$", re.M),
        "name": re.compile(r"^\s*packageName:\s*(.+?)\s*$", re.M),
        "version": re.compile(r"^\s*packageVersion:\s*(.+?)\s*$", re.M),
        "upload": re.compile(r"^\s*uploadId:\s*(\d+)\s*$", re.M),
    }
    for meta in ASSETS.rglob("*.meta"):
        try:
            text = meta.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        match = patterns["id"].search(text)
        if not match:
            continue
        pid = match.group(1)
        rec = records[pid]
        for key, target in (("name", "names"), ("version", "versions"), ("upload", "uploads")):
            found = patterns[key].search(text)
            if found:
                rec[target].add(found.group(1).strip())
        rec["paths"].append(meta.relative_to(ROOT).as_posix()[:-5])
    return records

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Malgun", 8)
    canvas.setFillColor(colors.HexColor("#64748B"))
    canvas.drawString(18 * mm, 10 * mm, "TinyLegends · Unity Asset Store 에셋 목록")
    canvas.drawRightString(192 * mm, 10 * mm, f"{doc.page}")
    canvas.restoreState()

def main():
    records = scan()
    expected = set(PUBLISHERS)
    if set(records) != expected:
        missing = sorted(expected - set(records))
        extra = sorted(set(records) - expected)
        raise RuntimeError(f"Asset inventory changed. missing={missing}, extra={extra}")

    pdfmetrics.registerFont(TTFont("Malgun", r"C:\Windows\Fonts\malgun.ttf"))
    pdfmetrics.registerFont(TTFont("MalgunBold", r"C:\Windows\Fonts\malgunbd.ttf"))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(str(OUTPUT), pagesize=A4, rightMargin=24*mm, leftMargin=24*mm,
                            topMargin=24*mm, bottomMargin=20*mm, title="Unity Asset Store 에셋 목록")
    styles = getSampleStyleSheet()
    title = ParagraphStyle("TitleKo", parent=styles["Title"], fontName="MalgunBold", fontSize=24,
                           leading=32, textColor=colors.HexColor("#172554"), alignment=TA_CENTER)
    body = ParagraphStyle("BodyKo", parent=styles["BodyText"], fontName="Malgun", fontSize=8.2,
                          leading=12, textColor=colors.HexColor("#1F2937"))
    head = ParagraphStyle("HeadKo", parent=body, fontName="MalgunBold", textColor=colors.white,
                          alignment=TA_CENTER)
    link = ParagraphStyle("LinkKo", parent=body, fontSize=6.8, leading=9, textColor=colors.HexColor("#2563EB"))
    story = [Paragraph("Unity Asset Store 에셋 목록", title), Spacer(1, 9*mm)]
    rows = [[Paragraph("에셋명", head), Paragraph("링크 주소", head), Paragraph("프로젝트 활용 용도", head)]]
    for idx, pid in enumerate(sorted(records, key=int), 1):
        r = records[pid]
        name = sorted(r["names"])[0]
        url = LINKS[pid]
        rows.append([
            Paragraph(f"{idx}. {name}", body),
            Paragraph(f'<link href="{url}">{url}</link>', link),
            Paragraph(USAGES[pid], body),
        ])
    table = Table(rows, colWidths=[45*mm, 58*mm, 53*mm], repeatRows=1)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0,0), (-1,0), colors.HexColor("#1E3A8A")),
        ("FONTNAME", (0,0), (-1,-1), "Malgun"),
        ("VALIGN", (0,0), (-1,-1), "MIDDLE"),
        ("GRID", (0,0), (-1,-1), 0.35, colors.HexColor("#CBD5E1")),
        ("ROWBACKGROUNDS", (0,1), (-1,-1), [colors.white, colors.HexColor("#EFF6FF")]),
        ("TOPPADDING", (0,0), (-1,-1), 7), ("BOTTOMPADDING", (0,0), (-1,-1), 7),
    ]))
    story.append(table)
    doc.build(story)
    print(OUTPUT)

if __name__ == "__main__":
    main()
