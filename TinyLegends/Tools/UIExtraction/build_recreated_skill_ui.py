#!/usr/bin/env python3
"""Build text-free, Unity-ready Skill UI parts. Skill artwork is excluded."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/Recreated/Skill"
S = 4
NAVY = (19, 34, 44, 255)
NAVY_DARK = (12, 25, 34, 255)
INK = (224, 217, 203, 255)
GOLD = (169, 126, 58, 255)
CORAL = (207, 78, 58, 255)
MUTED = (79, 81, 80, 255)


def canvas(size):
    return Image.new("RGBA", (size[0]*S, size[1]*S), (0, 0, 0, 0))


def rr(draw, box, radius, **kwargs):
    draw.rounded_rectangle(tuple(v*S for v in box), radius=radius*S,
                           width=kwargs.pop("width", 1)*S, **kwargs)


def save(image, relative):
    target = OUT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize((image.width//S, image.height//S), Image.Resampling.LANCZOS).save(target, optimize=True)


def sliced_panel(name, face=NAVY, outline=(128, 111, 82, 210), coral=False):
    im=canvas((128,128)); d=ImageDraw.Draw(im)
    rr(d,(2,2,125,125),9,fill=outline)
    rr(d,(4,4,123,123),7,fill=(33,43,48,255))
    rr(d,(6,6,121,121),6,fill=face)
    d.line((16*S,7*S,112*S,7*S),fill=(255,245,220,38),width=S)
    if coral:
        glow=Image.new("RGBA",im.size,(0,0,0,0)); gd=ImageDraw.Draw(glow)
        rr(gd,(3,3,124,124),9,outline=(238,89,60,190),width=4)
        im=Image.alpha_composite(glow.filter(ImageFilter.GaussianBlur(3*S)),im)
        d=ImageDraw.Draw(im); rr(d,(3,3,124,124),9,outline=(239,93,62,255),width=3)
    save(im,f"Frames/{name}.png")


def capsule(name, locked=False):
    im=canvas((256,64)); d=ImageDraw.Draw(im)
    line=MUTED if locked else GOLD
    rr(d,(2,2,253,61),18,fill=(58,58,56,220) if locked else (128,94,40,220))
    rr(d,(4,4,251,59),16,fill=NAVY_DARK)
    d.polygon([(0,32*S),(12*S,20*S),(12*S,44*S)],fill=line)
    d.polygon([(256*S,32*S),(244*S,20*S),(244*S,44*S)],fill=line)
    save(im,f"Frames/{name}.png")


def level_badge():
    im=canvas((112,42)); d=ImageDraw.Draw(im)
    rr(d,(1,1,110,40),8,fill=(94,76,46,235))
    rr(d,(3,3,108,38),6,fill=(13,22,28,245))
    save(im,"Frames/Level_Badge.png")


def action_button(name, state):
    im=canvas((256,80)); d=ImageDraw.Draw(im)
    if state == "disabled": outer=(99,96,91,220); inner=(55,55,54,255); face=(68,68,67,255)
    elif state == "pressed": outer=(222,112,87,255); inner=(112,45,37,255); face=(169,61,46,255)
    else: outer=(238,126,98,255); inner=(130,51,40,255); face=CORAL
    rr(d,(1,1,254,78),10,fill=outer); rr(d,(4,4,251,75),8,fill=inner); rr(d,(8,8,247,71),6,fill=face)
    d.line((18*S,10*S,238*S,10*S),fill=(255,238,218,90),width=2*S)
    save(im,f"Frames/Button_LevelUp_{state.title()}.png")


def reset_button():
    im=canvas((176,64)); d=ImageDraw.Draw(im)
    rr(d,(2,2,173,61),12,fill=(125,115,96,220)); rr(d,(4,4,171,59),10,fill=NAVY_DARK)
    save(im,"Frames/Button_Reset.png")


def selected_check():
    im=canvas((64,64)); d=ImageDraw.Draw(im)
    d.ellipse((6*S,6*S,58*S,58*S),fill=(244,126,96,255))
    d.ellipse((9*S,9*S,55*S,55*S),fill=CORAL)
    d.line((19*S,32*S,28*S,41*S,46*S,21*S),fill=INK,width=5*S,joint="curve")
    save(im,"Icons/Icon_SelectedCheck.png")


def lock_icon():
    im=canvas((64,64)); d=ImageDraw.Draw(im)
    d.arc((17*S,7*S,47*S,39*S),180,360,fill=INK,width=6*S)
    rr(d,(13,27,51,57),7,fill=INK)
    d.ellipse((29*S,36*S,35*S,42*S),fill=(71,75,74,255)); d.rectangle((30*S,40*S,34*S,49*S),fill=(71,75,74,255))
    save(im,"Icons/Icon_Lock.png")


def reset_icon():
    im=canvas((64,64)); d=ImageDraw.Draw(im)
    d.arc((13*S,13*S,51*S,51*S),35,320,fill=INK,width=5*S)
    d.polygon([(42*S,9*S),(55*S,14*S),(48*S,25*S)],fill=INK)
    save(im,"Icons/Icon_Reset.png")


def ornament(name, kind):
    if kind == "diamond":
        im=canvas((64,64)); d=ImageDraw.Draw(im)
        d.polygon([(32*S,8*S),(42*S,32*S),(32*S,56*S),(22*S,32*S)],fill=GOLD)
        d.polygon([(32*S,18*S),(37*S,32*S),(32*S,46*S),(27*S,32*S)],fill=NAVY_DARK)
    elif kind == "divider":
        im=canvas((512,32)); d=ImageDraw.Draw(im)
        for x in range(12,244):
            a=int(160*(x-12)/232); d.line((x*S,15*S,x*S,17*S),fill=(*GOLD[:3],a),width=S)
        for x in range(268,500):
            a=int(160*(500-x)/232); d.line((x*S,15*S,x*S,17*S),fill=(*GOLD[:3],a),width=S)
        d.polygon([(256*S,7*S),(265*S,16*S),(256*S,25*S),(247*S,16*S)],fill=GOLD)
        d.polygon([(256*S,12*S),(260*S,16*S),(256*S,20*S),(252*S,16*S)],fill=NAVY_DARK)
    else:
        im=canvas((48,48)); d=ImageDraw.Draw(im)
        d.polygon([(24*S,5*S),(28*S,19*S),(43*S,24*S),(28*S,29*S),(24*S,43*S),(20*S,29*S),(5*S,24*S),(20*S,19*S)],fill=INK)
        d.polygon([(24*S,15*S),(26*S,22*S),(33*S,24*S),(26*S,26*S),(24*S,33*S),(22*S,26*S),(15*S,24*S),(22*S,22*S)],fill=NAVY_DARK)
    save(im,f"Decorations/{name}.png")


def main():
    sliced_panel("Panel_Tier",NAVY)
    sliced_panel("Panel_SkillInfo",NAVY_DARK)
    sliced_panel("SkillSlot_Normal",(18,29,36,255))
    sliced_panel("SkillSlot_Selected",(24,32,38,255),coral=True)
    sliced_panel("SkillSlot_Locked",(24,30,34,230),outline=(74,75,72,210))
    capsule("LevelRequirement_Unlocked")
    capsule("LevelRequirement_Locked",True)
    level_badge(); reset_button()
    for state in ("normal","pressed","disabled"): action_button("LevelUp",state)
    selected_check(); lock_icon(); reset_icon()
    ornament("Tier_HeaderRule","divider")
    ornament("EmptyState_Diamond","sparkle")
    ornament("Panel_CornerDiamond","diamond")
    manifest={
      "source_reference":["Skill_FiveTiers_Selected.png","Skill_FiveTiers_Empty.png"],
      "excluded":"Skill artwork and all text",
      "nine_slice_borders":{
        "Frames/Panel_Tier.png":[16,16,16,16],
        "Frames/Panel_SkillInfo.png":[16,16,16,16],
        "Frames/SkillSlot_Normal.png":[16,16,16,16],
        "Frames/SkillSlot_Selected.png":[16,16,16,16],
        "Frames/SkillSlot_Locked.png":[16,16,16,16],
        "Frames/LevelRequirement_Unlocked.png":[24,24,24,24],
        "Frames/LevelRequirement_Locked.png":[24,24,24,24],
        "Frames/Level_Badge.png":[10,10,10,10],
        "Frames/Button_LevelUp_Normal.png":[18,18,18,18],
        "Frames/Button_LevelUp_Pressed.png":[18,18,18,18],
        "Frames/Button_LevelUp_Disabled.png":[18,18,18,18],
        "Frames/Button_Reset.png":[16,16,16,16]
      }}
    (OUT/"manifest_ui.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    print(f"Built Skill UI (no skill art) under {OUT}")


if __name__ == "__main__": main()
