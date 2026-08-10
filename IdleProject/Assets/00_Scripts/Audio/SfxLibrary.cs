using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle.Audio
{
    /// <summary>재생할 때 쓰는 음량과 최소 간격입니다.</summary>
    internal readonly struct SfxProfile
    {
        public readonly float Volume;
        /// <summary>이 시간 안에 같은 소리가 다시 요청되면 흘려보냅니다.</summary>
        public readonly float MinInterval;

        public SfxProfile(float volume, float minInterval)
        {
            Volume = volume;
            MinInterval = minInterval;
        }
    }

    /// <summary>
    /// 효과음 클립을 마련해 주는 곳입니다.
    ///
    /// 1) <c>Resources/Sounds/{SfxId}</c> 에 실제 음원이 있으면 그것을 씁니다. (BGM과 같은 폴더입니다)
    /// 2) 없으면 <see cref="SfxSynth"/>로 그 자리에서 합성합니다.
    ///
    /// ── 음색 규칙 ──────────────────────────────────────────────
    /// 밝은 동화풍에 어울리는 **오르골 · 마림바 · 하프 · 바람** 네 가지만 씁니다.
    ///
    /// - 소리의 재료는 거의 전부 <see cref="SfxSynth.Bell"/>입니다.
    ///   배음마다 사그라지는 속도가 달라 "팅" 하고 시작해 맑게 풀립니다.
    /// - 거의 모든 소리에 <see cref="SfxSynth.Reverb"/>로 울림을 겁니다. 신비로운 인상은 여기서 나옵니다.
    /// - 잡음(치직·쉭)과 왜곡은 쓰지 않습니다. 바람 소리조차 고역을 깎아 숨결처럼 만듭니다.
    /// - 타격도 딸깍이는 앞머리 없이 부드러운 나무 소리로 냅니다.
    /// - 음정은 도-레-미-솔-라(장음계 5음)만 씁니다. 무엇이 겹쳐도 불협이 생기지 않습니다.
    ///
    /// 모든 합성 효과음은 <see cref="MaxSeconds"/>를 넘지 않습니다.
    /// </summary>
    public static class SfxLibrary
    {
        /// <summary>BGM과 효과음을 한곳에서 관리하도록 같은 폴더를 씁니다.</summary>
        private const string ResourceRoot = "Sounds/";

        /// <summary>합성 효과음의 길이 상한입니다. 어떤 소리도 1초를 넘지 않습니다.</summary>
        public const float MaxSeconds = .95f;

        // 장음계 5음(C 메이저 펜타토닉).
        private const float C4 = 261.6f;
        private const float E4 = 329.6f;
        private const float G4 = 392f;
        private const float A4 = 440f;
        private const float C5 = 523.3f;
        private const float D5 = 587.3f;
        private const float E5 = 659.3f;
        private const float G5 = 784f;
        private const float A5 = 880f;
        private const float C6 = 1046.5f;
        private const float D6 = 1174.7f;
        private const float E6 = 1318.5f;
        private const float G6 = 1568f;
        private const float A6 = 1760f;
        private const float C7 = 2093f;

        // ── 음색표 ────────────────────────────────────────────────
        // 높은 배음일수록 빨리 죽습니다. 그 차이가 악기의 성격을 만듭니다.

        /// <summary>오르골 · 첼레스타. 맑고 길게 남습니다. 이 게임의 기본 음색입니다.</summary>
        private static readonly SfxSynth.Partial[] MusicBox =
        {
            new(1f, 1f, .34f), new(2f, .42f, .17f), new(3f, .2f, .09f),
            new(4.2f, .1f, .05f), new(5.4f, .05f, .03f)
        };

        /// <summary>유리 · 종. 오르골보다 반짝이고 배음이 살짝 어긋나 신비롭습니다.</summary>
        private static readonly SfxSynth.Partial[] Glass =
        {
            new(1f, 1f, .26f), new(2.02f, .5f, .15f), new(3.01f, .28f, .08f),
            new(4.9f, .14f, .045f), new(7.1f, .06f, .025f)
        };

        /// <summary>마림바 · 칼림바. 나무를 두드린 둥근 소리로, 짧게 끊깁니다.</summary>
        private static readonly SfxSynth.Partial[] Marimba =
        {
            new(1f, 1f, .13f), new(3f, .3f, .045f), new(6f, .1f, .02f)
        };

        /// <summary>낮은 울림. 착지·타격의 몸통으로 씁니다. 배음이 거의 없어 둔탁하지 않고 부드럽습니다.</summary>
        private static readonly SfxSynth.Partial[] Round =
        {
            new(1f, 1f, .1f), new(2f, .16f, .04f)
        };

        private static readonly Dictionary<SfxId, AudioClip> cache = new();

        /// <summary>효과음 클립을 얻습니다. 처음 부를 때 만들어지고, 그 뒤로는 캐시에서 돌려줍니다.</summary>
        public static AudioClip Get(SfxId id)
        {
            if (cache.TryGetValue(id, out var cached) && cached != null) return cached;

            var clip = Resources.Load<AudioClip>(ResourceRoot + id);
            if (clip == null)
                clip = SfxSynth.ToClip("SFX_" + id, SfxSynth.Limit(Build(id), MaxSeconds));
            cache[id] = clip;
            return clip;
        }

        public static void Clear() => cache.Clear();

        internal static SfxProfile GetProfile(SfxId id)
        {
            switch (id)
            {
                // ── 전투 내내 반복되는 소리 ───────────────────────────
                // 여기서 간격을 넉넉히 두지 않으면, 하나하나는 작아도 겹겹이 쌓여
                // 계속 딸랑거리는 잡음처럼 들립니다. 공격 소리를 가리지 않는 것이 우선입니다.
                case SfxId.EnemyHit: return new SfxProfile(.34f, .05f);
                // 치명타는 드물게 나므로 평타보다 조금 크게, 대신 겹치지 않도록 간격을 둡니다.
                case SfxId.EnemyCritical: return new SfxProfile(.46f, .09f);
                case SfxId.EnemyAttack: return new SfxProfile(.18f, .4f);
                case SfxId.EnemyDeath: return new SfxProfile(.42f, .14f);
                case SfxId.EnemySpawn: return new SfxProfile(.30f, .3f);
                case SfxId.Footstep: return new SfxProfile(.12f, .015f);
                // 동전·아이템은 몬스터를 잡을 때마다 나옵니다. 작게, 드물게 둡니다.
                case SfxId.CoinDrop: return new SfxProfile(.22f, .35f);
                case SfxId.CoinPickup: return new SfxProfile(.22f, .4f);
                case SfxId.LootPickup: return new SfxProfile(.24f, .4f);
                case SfxId.LootCommon: return new SfxProfile(.30f, .3f);
                // 화면 전체에 같은 크기로 나는 소리라 유난히 도드라집니다.
                // 실제 재생 여부는 PlayerDataManager가 누적 피해로 한 번 더 거릅니다.
                case SfxId.PlayerHurt: return new SfxProfile(.26f, 1.2f);
                // 8초마다 자동으로 들어갑니다.
                case SfxId.PotionHeal:
                case SfxId.PotionMana: return new SfxProfile(.30f, .5f);
                // 스킬을 쓸 때마다 뽑고 넣습니다. 공격 소리를 가리지 않게 아주 작게 둡니다.
                case SfxId.WeaponDraw: return new SfxProfile(.22f, .15f);
                case SfxId.WeaponSheathe: return new SfxProfile(.16f, .25f);
                case SfxId.StatTick: return new SfxProfile(.12f, .02f);

                // 한 번씩만 나는 큰 소리들.
                case SfxId.BossSpawn: return new SfxProfile(.74f, .5f);
                case SfxId.StageAdvance: return new SfxProfile(.72f, .5f);
                case SfxId.LevelUp: return new SfxProfile(.72f, .3f);
                case SfxId.PlayerDeath: return new SfxProfile(.64f, .5f);
                case SfxId.TitleTap: return new SfxProfile(.76f, .4f);
                case SfxId.AdventureStart: return new SfxProfile(.76f, .4f);
                case SfxId.LootLegendary: return new SfxProfile(.74f, .2f);
                case SfxId.RewardClaim: return new SfxProfile(.70f, .3f);
                case SfxId.LegendaryExplosion: return new SfxProfile(.56f, .2f);
                case SfxId.LegendaryQuake: return new SfxProfile(.50f, .3f);
                case SfxId.LegendarySky: return new SfxProfile(.56f, .3f);
                case SfxId.LegendaryTornado: return new SfxProfile(.46f, .3f);

                // 공격은 타격감이 있어야 하므로 다른 소리보다 앞에 나오게 둡니다.
                // 피해량이 큰 스킬일수록 조금 더 크게 냅니다(40 · 50 · 80 · 90).
                // 4번은 화면 전체를 다섯 번 치는 마무리 기술이라 가장 크게 냅니다.
                case SfxId.SkillBattleRoar: return new SfxProfile(.74f, .08f);
                case SfxId.SkillEarthshatter: return new SfxProfile(.70f, .08f);
                case SfxId.SkillLeapCrush: return new SfxProfile(.68f, .08f);
                case SfxId.SkillGroundSmash: return new SfxProfile(.62f, .08f);
                case SfxId.SkillCleave: return new SfxProfile(.58f, .08f);

                // UI는 눌린 느낌만 주면 되므로 작게 둡니다.
                case SfxId.UiClick: return new SfxProfile(.28f, .05f);
                case SfxId.UiSelect: return new SfxProfile(.30f, .05f);
                case SfxId.UiTabSwitch: return new SfxProfile(.34f, .08f);
                case SfxId.UiToast: return new SfxProfile(.34f, .12f);

                default: return new SfxProfile(.48f, .06f);
            }
        }

        // ==================================================================
        // 합성 레시피
        // ==================================================================

        private static float[] Build(SfxId id)
        {
            switch (id)
            {
                // ── UI 공통 ──────────────────────────────────────────
                case SfxId.UiClick: return Tap(.18f, C6, Marimba, .18f);
                case SfxId.UiSelect: return Tap(.3f, E6, MusicBox, .26f);
                case SfxId.UiTabSwitch: return Run(.35f, new[] { G5, C6, E6 }, .045f, Glass, .3f, .55f);
                case SfxId.UiPanelOpen: return Panel(true);
                case SfxId.UiPanelClose: return Panel(false);
                case SfxId.UiToast: return Run(.45f, new[] { A5, E6 }, .07f, MusicBox, .34f, 0f);
                case SfxId.UiPopupOpen: return Chord(.55f, new[] { C5, G5, C6 }, MusicBox, .38f, .22f);
                case SfxId.UiConfirm: return Run(.45f, new[] { G5, D6 }, .085f, MusicBox, .32f, 0f);
                case SfxId.UiCancel: return Run(.4f, new[] { D6, G5 }, .075f, MusicBox, .3f, 0f);
                case SfxId.UiDenied: return Denied();

                // ── 게임 흐름 ────────────────────────────────────────
                case SfxId.SceneFade: return SceneFade();
                case SfxId.LoadingComplete: return Run(.9f, new[] { C6, E6, G6, C7 }, .075f, MusicBox, .42f, .3f);
                case SfxId.NetworkLost: return Run(.6f, new[] { E5, C5 }, .13f, MusicBox, .34f, 0f);
                case SfxId.NetworkRestored: return Run(.6f, new[] { C5, G5 }, .13f, MusicBox, .34f, 0f);

                // ── 타이틀 · 캐릭터 선택 ─────────────────────────────
                case SfxId.TitleTap: return TitleTap();
                case SfxId.LoginSuccess: return Run(.8f, new[] { C6, E6, G6 }, .09f, MusicBox, .4f, .25f);
                case SfxId.LoginFailed: return Run(.55f, new[] { G5, E5 }, .11f, MusicBox, .32f, 0f);
                case SfxId.CharacterFocus: return CharacterFocus();
                case SfxId.StatTick: return Tap(.06f, C7, Marimba, 0f, .5f);
                case SfxId.AdventureStart: return AdventureStart();

                // ── 전투 · 플레이어 ──────────────────────────────────
                case SfxId.SkillCleave: return SkillCleave();
                case SfxId.SkillGroundSmash: return SkillGroundSmash();
                case SfxId.SkillLeapCrush: return SkillLeapCrush();
                case SfxId.SkillEarthshatter: return SkillEarthshatter();
                case SfxId.SkillBattleRoar: return SkillBattleRoar();
                case SfxId.WeaponDraw: return WeaponSlide(true);
                case SfxId.WeaponSheathe: return WeaponSlide(false);
                case SfxId.PlayerHurt: return PlayerHurt();
                case SfxId.PlayerDeath: return PlayerDeath();
                case SfxId.PlayerRevive: return PlayerRevive();
                case SfxId.LevelUp: return LevelUp();
                case SfxId.PotionHeal: return Potion(E5, C6);
                case SfxId.PotionMana: return Potion(A5, E6);
                case SfxId.Footstep: return Footstep();

                // ── 전투 · 몬스터 ────────────────────────────────────
                case SfxId.EnemySpawn: return Run(.4f, new[] { G4, D5 }, .06f, Glass, .3f, .4f);
                case SfxId.BossSpawn: return BossSpawn();
                case SfxId.EnemyHit: return EnemyHit();
                case SfxId.EnemyCritical: return EnemyCritical();
                case SfxId.EnemyDeath: return EnemyDeath();
                case SfxId.EnemyAttack: return EnemyAttack();
                case SfxId.EnemyStun: return EnemyStun();
                case SfxId.EnemySlow: return Run(.55f, new[] { A6, E6, A5 }, .075f, Glass, .34f, .5f);

                // ── 전설 장비 특수효과 ───────────────────────────────
                case SfxId.LegendaryTornado: return LegendaryTornado();
                case SfxId.LegendaryQuake: return LegendaryQuake();
                case SfxId.LegendaryExplosion: return LegendaryExplosion();
                case SfxId.LegendarySky: return LegendarySky();

                // ── 보상 · 진행 ──────────────────────────────────────
                // 동전과 줍기는 전투 내내 반복됩니다.
                // 유리종은 여운이 길어 딸랑거림이 쌓이므로, 짧게 끊기는 나무 소리로 냅니다.
                case SfxId.CoinDrop: return Run(.24f, new[] { C7, G6 }, .04f, Marimba, .16f, 0f);
                case SfxId.CoinPickup: return Run(.22f, new[] { G6, C7 }, .045f, Marimba, .14f, 0f);
                case SfxId.LootCommon: return Tap(.22f, G5, Marimba, .16f);
                case SfxId.LootRare: return Chord(.55f, new[] { E6 }, MusicBox, .4f, .18f);
                case SfxId.LootEpic: return Run(.75f, new[] { E6, G6, C7 }, .07f, MusicBox, .42f, .25f);
                case SfxId.LootLegendary: return LootLegendary();
                case SfxId.LootPickup: return Run(.22f, new[] { C6, G6 }, .04f, Marimba, .14f, 0f);
                case SfxId.WaveStart: return WaveStart();
                case SfxId.StageAdvance: return StageAdvance();

                // ── 장비 · 성장 ──────────────────────────────────────
                case SfxId.Equip: return Equip();
                case SfxId.Unequip: return Run(.3f, new[] { C6, G5 }, .05f, Marimba, .26f, 0f);
                case SfxId.PassiveInvest: return Run(.5f, new[] { G5, D6, G6 }, .055f, Glass, .38f, .4f);
                case SfxId.PassiveRefund: return Run(.45f, new[] { G6, D6, G5 }, .055f, Glass, .34f, .35f);

                // ── 우편 · 보상 ──────────────────────────────────────
                case SfxId.MailClaim: return Run(.6f, new[] { C6, G6 }, .09f, MusicBox, .38f, .2f);
                case SfxId.MailClaimAll: return Run(.85f, new[] { C6, E6, G6, C7 }, .07f, MusicBox, .42f, .28f);
                case SfxId.RewardClaim: return RewardClaim();

                default: return Tap(.18f, C6, Marimba, .18f);
            }
        }

        // ------------------------------------------------------------------
        // 기본 재료
        // ------------------------------------------------------------------

        /// <summary>한 음을 톡 치고 울림을 남깁니다.</summary>
        private static float[] Tap(
            float seconds, float hz, SfxSynth.Partial[] tone, float reverbMix, float peak = .72f)
        {
            var mix = SfxSynth.Bell(seconds, hz, tone, .005f);
            if (reverbMix > 0f) SfxSynth.Reverb(mix, reverbMix, .8f, .45f);
            return SfxSynth.Normalize(mix, peak);
        }

        /// <summary>음을 차례로 이어 칩니다. 오르골이 한 소절 도는 느낌입니다.</summary>
        private static float[] Run(
            float seconds, float[] notes, float step, SfxSynth.Partial[] tone,
            float reverbMix, float airMix, float peak = .76f)
        {
            var mix = SfxSynth.Buffer(seconds);

            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * step;
                var remaining = seconds - start;
                if (remaining <= .02f) break;
                // 살짝 어긋난 사본을 겹쳐 소리에 폭을 줍니다.
                SfxSynth.Mix(mix, SfxSynth.Bell(remaining, notes[i], tone, .005f, 5f), .55f, start);
            }

            if (airMix > 0f) SfxSynth.Mix(mix, Breath(seconds, notes[0] * 3f, notes[notes.Length - 1] * 4f), airMix * .3f);
            if (reverbMix > 0f) SfxSynth.Reverb(mix, reverbMix, 1f, .42f);
            return SfxSynth.Normalize(mix, peak);
        }

        /// <summary>여러 음을 한 번에 울립니다.</summary>
        private static float[] Chord(
            float seconds, float[] notes, SfxSynth.Partial[] tone,
            float reverbMix, float spread, float peak = .78f)
        {
            var mix = SfxSynth.Buffer(seconds);

            for (var i = 0; i < notes.Length; i++)
            {
                // 완전히 동시에 치면 딱딱하므로 아주 조금씩 어긋나게 둡니다.
                var start = i * spread * .06f;
                SfxSynth.Mix(mix, SfxSynth.Bell(seconds - start, notes[i], tone, .006f, 4f), .5f, start);
            }

            if (reverbMix > 0f) SfxSynth.Reverb(mix, reverbMix, 1.1f, .4f);
            return SfxSynth.Normalize(mix, peak);
        }

        /// <summary>숨결 같은 바람입니다. 고역을 깎아 쉭쉭거리지 않게 합니다.</summary>
        private static float[] Breath(float seconds, float fromHz, float toHz, uint seed = 4242u)
        {
            var layer = SfxSynth.Noise(seconds, .9f, new SfxSynth.Rng(seed));
            SfxSynth.BandPass(layer, fromHz, toHz, 1.6f);
            // 거친 결을 한 번 더 깎아 냅니다. 이게 없으면 치직거립니다.
            SfxSynth.LowPass(layer, 6000f, 3000f);
            return SfxSynth.Shape(layer, seconds * .3f, seconds * .5f);
        }

        /// <summary>낮은 울림입니다. 착지와 타격의 몸통으로 씁니다.</summary>
        private static float[] Thump(float seconds, float hz, float decay, float amplitude = .9f)
        {
            var layer = SfxSynth.Tone(seconds, Wave.Sine, hz * 1.6f, hz, amplitude, .4f);
            return SfxSynth.Pluck(layer, .006f, decay);
        }

        // ------------------------------------------------------------------
        // 공격 재료 — 베기와 내려찍기
        // ------------------------------------------------------------------

        /// <summary>
        /// 무기가 공기를 가르는 소리입니다.
        /// 잡음의 통과 대역을 빠르게 훑고, 칼끝이 가장 빨라지는 후반부에 힘을 실어 "쉬익" 하고 지나갑니다.
        /// </summary>
        private static float[] Swing(float seconds, float fromHz, float toHz, uint seed, float peak = .68f)
        {
            var layer = SfxSynth.Noise(seconds, .9f, new SfxSynth.Rng(seed));
            SfxSynth.BandPass(layer, fromHz, toHz, 3.2f);
            return SfxSynth.Swell(layer, peak, 2.6f, 2.2f);
        }

        /// <summary>베어 낼 때 살짝 스치는 날카로운 앞머리입니다. 아주 짧아야 거칠게 들리지 않습니다.</summary>
        private static float[] Cut(float seconds, uint seed, float amplitude = .9f)
        {
            var layer = SfxSynth.Noise(seconds, amplitude, new SfxSynth.Rng(seed));
            SfxSynth.HighPass(layer, 2600f, 5200f);
            return SfxSynth.Pluck(layer, .0006f, seconds * .16f);
        }

        /// <summary>
        /// 돌과 흙이 튀어 흩어지는 소리입니다.
        /// 짧은 조각을 불규칙한 간격으로 흩뿌려 파편이 사방으로 튀는 느낌을 만듭니다.
        /// </summary>
        private static float[] Debris(float seconds, int count, uint seed, float lowHz = 900f, float highHz = 3800f)
        {
            var mix = SfxSynth.Buffer(seconds);
            var rng = new SfxSynth.Rng(seed);

            for (var i = 0; i < count; i++)
            {
                var start = rng.Range(0f, seconds * .55f);
                var length = Mathf.Min(.06f, seconds - start);
                if (length <= .004f) continue;

                var piece = SfxSynth.Noise(length, .9f, rng);
                SfxSynth.BandPass(piece, rng.Range(lowHz, highHz), rng.Range(lowHz * .5f, highHz * .6f), 2.8f);
                SfxSynth.Pluck(piece, .0004f, rng.Range(.005f, .016f));
                SfxSynth.Mix(mix, piece, rng.Range(.3f, .9f), start);
            }

            return mix;
        }

        /// <summary>땅이 넓게 갈라지며 사방으로 퍼지는 저역 진동입니다.</summary>
        private static float[] Rumble(float seconds, float startHz, float endHz, float shakeHz, uint seed)
        {
            var mix = SfxSynth.Buffer(seconds);

            var low = SfxSynth.Tone(seconds, Wave.Sine, startHz, endHz, .9f);
            SfxSynth.Tremolo(low, shakeHz, .4f);
            SfxSynth.Shape(low, seconds * .06f, seconds * .55f);
            SfxSynth.Mix(mix, low, .9f);

            var ground = SfxSynth.Noise(seconds, .9f, new SfxSynth.Rng(seed));
            SfxSynth.LowPass(ground, 700f, 180f);
            SfxSynth.Tremolo(ground, shakeHz * 1.7f, .5f);
            SfxSynth.Shape(ground, seconds * .05f, seconds * .5f);
            SfxSynth.Mix(mix, ground, .5f);

            return mix;
        }

        /// <summary>
        /// 땅을 내려찍는 소리입니다.
        /// 낮은 음이 뚝 떨어지며 몸통을 만들고, 저역만 남긴 잡음이 흙먼지를 만듭니다.
        /// 찌그러뜨리지 않아 무겁되 거칠지는 않습니다.
        /// </summary>
        private static float[] Slam(float seconds, float startHz, float endHz, uint seed, float amplitude = 1f)
        {
            var mix = SfxSynth.Buffer(seconds);

            var body = SfxSynth.Tone(seconds, Wave.Sine, startHz, endHz, .95f * amplitude, .3f);
            SfxSynth.Pluck(body, .0015f, seconds * .22f);
            SfxSynth.Mix(mix, body, 1f);

            var dirt = SfxSynth.Noise(seconds * .6f, .9f, new SfxSynth.Rng(seed));
            SfxSynth.LowPass(dirt, 2400f, 320f);
            SfxSynth.Pluck(dirt, .001f, seconds * .1f);
            SfxSynth.Mix(mix, dirt, .5f * amplitude);

            return mix;
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------

        private static float[] Panel(bool opening)
        {
            var notes = opening ? new[] { C5, G5, C6 } : new[] { C6, G5, C5 };
            var mix = SfxSynth.Buffer(opening ? .5f : .4f);
            var length = opening ? .5f : .4f;

            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .05f;
                SfxSynth.Mix(mix, SfxSynth.Bell(length - start, notes[i], MusicBox, .006f, 5f), .5f, start);
            }

            SfxSynth.Mix(mix, Breath(length, opening ? 1200f : 3600f, opening ? 3600f : 1200f, 3313u), .16f);
            SfxSynth.Reverb(mix, .36f, 1f, .42f);
            return SfxSynth.Normalize(mix, .74f);
        }

        /// <summary>거부. 날카롭게 튕겨 내지 않고, 낮은 두 음으로 부드럽게 "안 돼"라고 합니다.</summary>
        private static float[] Denied()
        {
            var mix = SfxSynth.Buffer(.4f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.24f, G4, Round, .012f), .7f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.3f, E4, Round, .012f), .7f, .1f);
            SfxSynth.LowPass(mix, 2200f, 1100f);
            SfxSynth.Reverb(mix, .24f, .8f, .5f);
            return SfxSynth.Normalize(mix, .62f);
        }

        // ------------------------------------------------------------------
        // 흐름 · 타이틀
        // ------------------------------------------------------------------

        private static float[] SceneFade()
        {
            var mix = SfxSynth.Buffer(.6f);
            SfxSynth.Mix(mix, Breath(.5f, 700f, 3200f, 7717u), .55f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.5f, C5, MusicBox, .02f, 6f), .3f);
            SfxSynth.Reverb(mix, .4f, 1.2f, .45f);
            return SfxSynth.Normalize(mix, .56f);
        }

        /// <summary>타이틀. 오래된 오르골 뚜껑이 열리는 순간처럼 화음이 넓게 퍼집니다.</summary>
        private static float[] TitleTap()
        {
            var mix = SfxSynth.Buffer(.95f);

            // 낮은 뿌리음 위로 5음을 부채처럼 펼칩니다.
            SfxSynth.Mix(mix, SfxSynth.Bell(.95f, C4, MusicBox, .01f, 4f), .34f);
            var notes = new[] { G5, C6, E6, G6, C7 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = .03f + i * .05f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, notes[i], Glass, .006f, 6f), .34f, start);
            }

            SfxSynth.Mix(mix, Breath(.8f, 2000f, 5000f, 2929u), .1f);
            SfxSynth.Reverb(mix, .5f, 1.4f, .38f);
            return SfxSynth.Normalize(mix, .8f);
        }

        private static float[] CharacterFocus()
        {
            var mix = SfxSynth.Buffer(.7f);
            var notes = new[] { C6, E6, A6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .06f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.7f - start, notes[i], Glass, .008f, 7f), .45f, start);
            }
            SfxSynth.Mix(mix, Breath(.6f, 1600f, 4800f, 6161u), .18f);
            SfxSynth.Reverb(mix, .44f, 1.2f, .4f);
            return SfxSynth.Normalize(mix, .7f);
        }

        private static float[] AdventureStart()
        {
            var mix = SfxSynth.Buffer(.95f);

            SfxSynth.Mix(mix, Thump(.3f, C4 * .5f, .09f), .4f);
            var notes = new[] { C5, E5, G5, C6, E6, G6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .055f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, notes[i], MusicBox, .006f, 5f), .38f, start);
            }

            SfxSynth.Mix(mix, Breath(.85f, 1400f, 4600f, 4747u), .14f);
            SfxSynth.Reverb(mix, .46f, 1.3f, .4f);
            return SfxSynth.Normalize(mix, .8f);
        }

        // ------------------------------------------------------------------
        // 스킬 — 실제 SkillData(Resources/Data/Skills)에 맞춰 만듭니다.
        //
        // 파일 이름은 lightning · ice · meteor · beam · bomb 이지만 내용은 전혀 다릅니다.
        // 모두 바바리안의 근접 · 범위기(SkillType.Area, 대상 다수)입니다.
        // 소리는 animationIndex로 고릅니다.
        //
        //   0  Savage Cleave  피해 40 · 쿨 1.1초 · 시전 0.30  "빠른 무기 타격"
        //   1  Ground Smash   피해 50 · 쿨 2초 · 시전 0.35  "돌과 먼지를 튀기는 강타"
        //   2  Leap Crush     피해 80 · 쿨 2.5초 · 시전 0.50  "돌이 날리는 내리찍는 착지"
        //   3  Earthshatter   피해 90 · 쿨 3초 · 시전 0.20  "넓고 거친 땅 충격"
        //   4  Battle Roar    피해 40×5 · 쿨 8초 · 시전 0.60 "화면 전체를 다섯 번 치는 충격파"
        //
        // 피해량이 클수록 무겁고 길게, 시전 시간이 길수록 준비 동작을 길게 잡았습니다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 0 · Savage Cleave — 빠른 무기 타격.
        /// 넷 중 가장 가볍고 빠릅니다. 준비 동작 없이 단숨에 베고 끝냅니다.
        /// </summary>
        private static float[] SkillCleave()
        {
            var mix = SfxSynth.Buffer(.45f);

            // 짧고 높은 휘두름. 시전 0.3초에 맞춰 준비 동작을 짧게 둡니다.
            SfxSynth.Mix(mix, Swing(.15f, 1800f, 6000f, 1481u, .74f), .85f);
            SfxSynth.Mix(mix, Cut(.08f, 1482u), .6f, .11f);
            // 베어 낸 뒤의 가벼운 충격. 땅이 아니라 몸통을 친 무게입니다.
            SfxSynth.Mix(mix, Slam(.24f, 300f, 70f, 1483u, .55f), .6f, .11f);

            SfxSynth.Reverb(mix, .26f, .9f, .42f);
            return SfxSynth.Normalize(mix, .8f);
        }

        /// <summary>
        /// 1 · Ground Smash — 돌과 먼지를 튀기는 강타.
        /// 무기로 땅을 내리쳐 파편이 튑니다.
        /// </summary>
        private static float[] SkillGroundSmash()
        {
            var mix = SfxSynth.Buffer(.62f);

            SfxSynth.Mix(mix, Swing(.22f, 900f, 3400f, 2642u, .8f), .8f);

            SfxSynth.Mix(mix, Slam(.36f, 230f, 46f, 2643u), .9f, .2f);
            SfxSynth.Mix(mix, Cut(.1f, 2644u), .45f, .2f);
            // 튀어 오르는 돌과 흙.
            SfxSynth.Mix(mix, Debris(.36f, 9, 2645u), .5f, .22f);

            SfxSynth.Reverb(mix, .32f, 1.1f, .45f);
            return SfxSynth.Normalize(mix, .84f);
        }

        /// <summary>
        /// 2 · Leap Crush — 도약 후 내리찍는 착지.
        /// 뛰어오르는 바람 → 잠깐의 공중 → 무겁게 짓누르는 착지 순서로 만듭니다.
        /// </summary>
        private static float[] SkillLeapCrush()
        {
            var mix = SfxSynth.Buffer(.82f);

            // 도약. 위로 뜨는 느낌이라 주파수가 올라갑니다.
            SfxSynth.Mix(mix, Swing(.24f, 400f, 2200f, 3753u, .5f), .5f);

            // 착지. 시전 0.5초에 맞춰 늦게 떨어집니다.
            SfxSynth.Mix(mix, Slam(.5f, 280f, 34f, 3754u), 1f, .32f);
            SfxSynth.Mix(mix, Cut(.12f, 3755u), .4f, .32f);
            SfxSynth.Mix(mix, Debris(.44f, 14, 3756u, 700f, 3200f), .55f, .34f);
            SfxSynth.Mix(mix, Rumble(.4f, 62f, 40f, 11f, 3757u), .45f, .34f);

            SfxSynth.Reverb(mix, .4f, 1.3f, .45f);
            return SfxSynth.Normalize(mix, .88f);
        }

        /// <summary>
        /// 3 · Earthshatter — 넓고 거친 땅 충격. 피해 90으로 가장 강합니다.
        /// 시전 0.2초로 가장 빨라 준비 동작이 거의 없고, 대신 여파가 넓게 오래 퍼집니다.
        /// </summary>
        private static float[] SkillEarthshatter()
        {
            var mix = SfxSynth.Buffer(.92f);

            SfxSynth.Mix(mix, Swing(.13f, 1000f, 3000f, 4864u, .85f), .6f);

            // 가장 크고 낮은 충격.
            SfxSynth.Mix(mix, Slam(.58f, 320f, 28f, 4865u), 1f, .12f);
            SfxSynth.Mix(mix, Cut(.14f, 4866u), .45f, .12f);

            // 갈라진 땅이 사방으로 퍼져 나갑니다. 이것이 "넓다"는 인상을 만듭니다.
            SfxSynth.Mix(mix, Rumble(.66f, 74f, 32f, 8f, 4867u), .7f, .14f);
            SfxSynth.Mix(mix, Debris(.55f, 18, 4868u, 600f, 3000f), .5f, .15f);

            SfxSynth.Reverb(mix, .46f, 1.5f, .45f);
            return SfxSynth.Normalize(mix, .9f);
        }

        /// <summary>
        /// 4 · Battle Roar — 화면 안의 모든 몬스터를 다섯 번 치는 마무리 기술입니다.
        ///
        /// 눈에 보이는 피해가 다섯 번 들어가므로 소리도 **다섯 번 두드립니다.**
        /// 한 번만 크게 울리면 화면과 소리의 횟수가 어긋나 얻어맞는 느낌이 사라집니다.
        /// 뒤로 갈수록 조금씩 높고 작게 해서, 퍼져 나가며 잦아드는 충격파로 들리게 합니다.
        /// </summary>
        private static float[] SkillBattleRoar()
        {
            var mix = SfxSynth.Buffer(.95f);

            // 내지르는 준비 동작. 시전 0.6초에 맞춰 낮은 데서 차오릅니다.
            SfxSynth.Mix(mix, Swing(.26f, 260f, 1600f, 5971u, .7f), .6f);
            SfxSynth.Mix(mix, Rumble(.9f, 58f, 30f, 9f, 5972u), .5f);

            // 다섯 번의 타격. BattleGameController의 타격 간격과 같은 리듬입니다.
            for (var i = 0; i < 5; i++)
            {
                var start = .1f + i * .16f;
                var fade = 1f - i * .13f;
                var seed = (uint)(5973 + i * 3);
                SfxSynth.Mix(mix, Slam(.34f, 300f + i * 40f, 30f, seed, .92f), fade, start);
                SfxSynth.Mix(mix, Cut(.09f, seed + 1), .34f * fade, start);
                SfxSynth.Mix(mix, Debris(.28f, 8, seed + 2, 700f, 3400f), .34f * fade, start + .02f);
            }

            SfxSynth.Reverb(mix, .5f, 1.6f, .45f);
            return SfxSynth.Normalize(mix, .92f);
        }

        // ------------------------------------------------------------------
        // 플레이어
        // ------------------------------------------------------------------

        /// <summary>
        /// 무기를 칼집에서 빼고 넣는 마찰음입니다.
        ///
        /// 스킬을 쓸 때마다 뽑고 넣으므로 공격 한 번에 두 번 납니다.
        /// 음정이 있으면(종·유리) 공격할 때마다 딸랑거리므로, 잡음만으로 만들고 아주 작게 둡니다.
        /// </summary>
        private static float[] WeaponSlide(bool drawing)
        {
            var length = drawing ? .22f : .16f;
            var mix = SfxSynth.Buffer(length);

            var slide = SfxSynth.Noise(length, .9f, new SfxSynth.Rng(drawing ? 6006u : 6007u));
            SfxSynth.BandPass(slide, drawing ? 1800f : 3400f, drawing ? 5000f : 1300f, 2.4f);
            SfxSynth.Swell(slide, drawing ? .6f : .32f, 2.2f, 2f);
            SfxSynth.Mix(mix, slide, .9f);

            return SfxSynth.Normalize(mix, drawing ? .42f : .3f);
        }

        /// <summary>
        /// 흙을 밟는 소리입니다.
        ///
        /// 0.33초마다 쉬지 않고 나기 때문에 **음정이 있으면 안 됩니다.**
        /// 같은 음이 반복되면 발소리가 아니라 박자에 맞춰 울리는 노래처럼 들립니다.
        /// 낮은 성분도 순식간에 훑고 지나가게 해서 음정이 남지 않도록 합니다.
        /// </summary>
        private static float[] Footstep()
        {
            var mix = SfxSynth.Buffer(.1f);

            var ground = SfxSynth.Noise(.08f, .9f, new SfxSynth.Rng(2718u));
            SfxSynth.LowPass(ground, 1600f, 350f);
            SfxSynth.Pluck(ground, .002f, .024f);
            SfxSynth.Mix(mix, ground, .95f);

            var weight = SfxSynth.Tone(.06f, Wave.Sine, 190f, 44f, .8f, .35f);
            SfxSynth.Pluck(weight, .002f, .009f);
            SfxSynth.Mix(mix, weight, .3f);

            return SfxSynth.Normalize(mix, .42f);
        }

        /// <summary>
        /// 피격.
        ///
        /// 지금은 어디서도 재생하지 않습니다.
        /// 몬스터가 때릴 때 <see cref="SfxId.EnemyAttack"/>와 한꺼번에 울려 "퉁퉁"거렸고,
        /// 체력 변화는 HP 바로 충분히 보이기 때문입니다.
        /// 다시 쓰려면 <c>PlayerDataManager.TakeDamage</c>에서 불러 주면 됩니다.
        /// </summary>
        private static float[] PlayerHurt()
        {
            var mix = SfxSynth.Buffer(.26f);

            var body = SfxSynth.Tone(.18f, Wave.Sine, 380f, 42f, .95f, .32f);
            SfxSynth.Pluck(body, .0015f, .03f);
            SfxSynth.Mix(mix, body, .8f);

            var impact = SfxSynth.Noise(.16f, .9f, new SfxSynth.Rng(7208u));
            SfxSynth.LowPass(impact, 2000f, 380f);
            SfxSynth.Pluck(impact, .001f, .04f);
            SfxSynth.Mix(mix, impact, .85f);

            return SfxSynth.Normalize(mix, .66f);
        }

        /// <summary>쓰러짐. 오르골 태엽이 풀리듯 음이 하나씩 떨어집니다.</summary>
        private static float[] PlayerDeath()
        {
            var mix = SfxSynth.Buffer(.85f);
            var notes = new[] { G5, E5, C5, G4, E4 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .09f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.85f - start, notes[i], MusicBox, .008f, 5f), .45f, start);
            }
            SfxSynth.Reverb(mix, .44f, 1.3f, .45f);
            return SfxSynth.Normalize(mix, .7f);
        }

        private static float[] PlayerRevive()
        {
            var mix = SfxSynth.Buffer(.85f);
            var notes = new[] { C5, G5, C6, E6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .075f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.85f - start, notes[i], MusicBox, .01f, 6f), .42f, start);
            }
            SfxSynth.Mix(mix, Breath(.75f, 1200f, 4400f, 9451u), .18f);
            SfxSynth.Reverb(mix, .46f, 1.3f, .4f);
            return SfxSynth.Normalize(mix, .72f);
        }

        /// <summary>레벨업. 오르골이 한 소절을 밝게 올라갑니다.</summary>
        private static float[] LevelUp()
        {
            var mix = SfxSynth.Buffer(.95f);
            var notes = new[] { C6, D6, E6, G6, C7 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .065f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, notes[i], MusicBox, .006f, 5f), .4f, start);
            }
            SfxSynth.Mix(mix, SfxSynth.Bell(.9f, C5, MusicBox, .012f, 4f), .22f);
            SfxSynth.Mix(mix, Breath(.85f, 2400f, 6000f, 1607u), .12f);
            SfxSynth.Reverb(mix, .48f, 1.3f, .38f);
            return SfxSynth.Normalize(mix, .8f);
        }

        /// <summary>물약. 물방울이 떨어져 번지는 소리입니다.</summary>
        private static float[] Potion(float startHz, float endHz)
        {
            var mix = SfxSynth.Buffer(.5f);

            var drop = SfxSynth.Tone(.16f, Wave.Sine, startHz, endHz, .9f, .4f);
            SfxSynth.Pluck(drop, .006f, .045f);
            SfxSynth.Mix(mix, drop, .7f);

            // 물약은 8초마다 자동으로 들어갑니다. 여운이 길면 계속 딸랑거리므로 짧게 끊습니다.
            SfxSynth.Mix(mix, SfxSynth.Bell(.2f, endHz, Marimba, .006f, 5f), .32f, .04f);
            SfxSynth.Reverb(mix, .2f, .9f, .5f);
            return SfxSynth.Normalize(mix, .52f);
        }

        // ------------------------------------------------------------------
        // 몬스터
        // ------------------------------------------------------------------

        private static float[] BossSpawn()
        {
            var mix = SfxSynth.Buffer(.95f);
            SfxSynth.Mix(mix, Thump(.4f, 65f, .12f), .7f);
            // 낮은 화음을 넓은 울림으로 깔아 위압감 대신 신비로움을 줍니다.
            foreach (var (hz, start) in new[] { (C4, 0f), (G4, .07f), (C5, .14f), (E5, .21f) })
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, hz, MusicBox, .01f, 5f), .38f, start);
            SfxSynth.Mix(mix, Breath(.85f, 900f, 3600f, 1927u), .14f);
            SfxSynth.Reverb(mix, .5f, 1.5f, .42f);
            return SfxSynth.Normalize(mix, .8f);
        }

        /// <summary>
        /// 타격.
        ///
        /// 몬스터는 체력이 많아 죽기 전까지 수십 번 맞고, 전설 장비는 한 번에 여러 번 때립니다.
        /// 그래서 초당 열 번 넘게 날 수 있으므로 **음정이 있으면 안 됩니다.**
        /// 음이 있으면 연타할 때 실로폰 연주처럼 들립니다.
        /// 저음을 순식간에 훑어 내려 "퍽" 하는 충격만 남깁니다.
        /// </summary>
        private static float[] EnemyHit()
        {
            var mix = SfxSynth.Buffer(.16f);

            // 끝에서 한 음에 머물면 그 음정이 들립니다. 계속 아래로 훑고 지나가게 두고,
            // 머물기 전에 사그라지도록 짧게 끊습니다.
            var body = SfxSynth.Tone(.13f, Wave.Sine, 460f, 46f, .95f, .3f);
            SfxSynth.Pluck(body, .001f, .019f);
            SfxSynth.Mix(mix, body, .85f);

            var impact = SfxSynth.Noise(.11f, .9f, new SfxSynth.Rng(5040u));
            SfxSynth.BandPass(impact, 2800f, 600f, 1.2f);
            SfxSynth.Pluck(impact, .0006f, .026f);
            SfxSynth.Mix(mix, impact, .85f);

            return SfxSynth.Normalize(mix, .72f);
        }

        /// <summary>
        /// 치명타.
        ///
        /// 평타와 같은 "퍽"을 쓰되 조금 더 높고 밝게 훑고, 그 위에 반짝이는 두 음을 얹습니다.
        /// 평타처럼 연달아 나지 않으므로(확률로만 터집니다) 음정을 써도 실로폰처럼 들리지 않습니다.
        /// </summary>
        private static float[] EnemyCritical()
        {
            var mix = SfxSynth.Buffer(.34f);

            var body = SfxSynth.Tone(.15f, Wave.Sine, 720f, 58f, .95f, .3f);
            SfxSynth.Pluck(body, .001f, .022f);
            SfxSynth.Mix(mix, body, .85f);

            var impact = SfxSynth.Noise(.12f, .9f, new SfxSynth.Rng(9137u));
            SfxSynth.BandPass(impact, 3800f, 900f, 1.3f);
            SfxSynth.Pluck(impact, .0005f, .028f);
            SfxSynth.Mix(mix, impact, .8f);

            // 위로 튀는 두 음이 "제대로 꽂혔다"는 인상을 만듭니다.
            SfxSynth.Mix(mix, SfxSynth.Bell(.24f, C6, Glass, .004f, 7f), .4f, .01f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.2f, G6, Glass, .004f, 8f), .28f, .05f);
            SfxSynth.Reverb(mix, .24f, .9f, .45f);
            return SfxSynth.Normalize(mix, .8f);
        }

        private static float[] EnemyDeath()
        {
            // 몬스터는 쉴 새 없이 죽습니다. 종이 울리면 여운이 쌓이므로 나무 소리로 짧게 냅니다.
            var mix = SfxSynth.Buffer(.35f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.22f, G5, Marimba, .005f), .5f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.26f, C5, Round, .006f), .55f, .03f);
            SfxSynth.Mix(mix, Breath(.24f, 2200f, 900f, 6183u), .22f);
            SfxSynth.Reverb(mix, .2f, .8f, .5f);
            return SfxSynth.Normalize(mix, .7f);
        }

        /// <summary>
        /// 몬스터 공격.
        ///
        /// 여덟 마리가 각자 1.1초마다 휘두르므로 초당 일곱 번까지 납니다.
        /// 저음을 넣으면 그 "퉁"이 겹겹이 쌓여 북 치는 소리가 되므로, 바람 소리만 남깁니다.
        /// </summary>
        private static float[] EnemyAttack()
        {
            var mix = SfxSynth.Buffer(.2f);
            SfxSynth.Mix(mix, Swing(.17f, 1100f, 3200f, 7296u, .6f), .9f);
            return SfxSynth.Normalize(mix, .46f);
        }

        /// <summary>스턴. 음정이 흔들려 어질어질한 느낌을 줍니다.</summary>
        private static float[] EnemyStun()
        {
            var mix = SfxSynth.Buffer(.5f);
            var wobble = SfxSynth.Tone(.42f, Wave.Sine, A5, G5, .9f, 1f, null, 11f, .07f);
            SfxSynth.Pluck(wobble, .01f, .13f);
            SfxSynth.Mix(mix, wobble, .6f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.36f, E6, Glass, .008f, 9f), .3f, .03f);
            SfxSynth.Reverb(mix, .32f, 1f, .45f);
            return SfxSynth.Normalize(mix, .6f);
        }

        // ------------------------------------------------------------------
        // 전설 장비
        // ------------------------------------------------------------------

        private static float[] LegendaryTornado()
        {
            var mix = SfxSynth.Buffer(.85f);
            var wind = Breath(.78f, 800f, 3000f, 2048u);
            SfxSynth.Tremolo(wind, 7f, .4f);
            SfxSynth.Mix(mix, wind, .8f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.7f, G5, Glass, .02f, 8f), .3f, .06f);
            SfxSynth.Reverb(mix, .42f, 1.3f, .42f);
            return SfxSynth.Normalize(mix, .7f);
        }

        private static float[] LegendaryQuake()
        {
            var mix = SfxSynth.Buffer(.8f);
            var low = SfxSynth.Tone(.7f, Wave.Sine, 62f, 48f, .9f);
            SfxSynth.Tremolo(low, 13f, .45f);
            SfxSynth.Shape(low, .05f, .35f);
            SfxSynth.Mix(mix, low, .9f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.55f, C5, MusicBox, .012f, 5f), .3f);
            SfxSynth.Reverb(mix, .38f, 1.3f, .5f);
            return SfxSynth.Normalize(mix, .74f);
        }

        private static float[] LegendaryExplosion()
        {
            var mix = SfxSynth.Buffer(.75f);
            SfxSynth.Mix(mix, Thump(.5f, 76f, .14f), .95f);
            foreach (var (hz, start) in new[] { (C6, 0f), (G6, .04f), (C7, .08f) })
                SfxSynth.Mix(mix, SfxSynth.Bell(.6f - start, hz, Glass, .004f, 8f), .3f, start);
            SfxSynth.Mix(mix, Breath(.55f, 1800f, 5000f, 4096u), .14f);
            SfxSynth.Reverb(mix, .44f, 1.3f, .42f);
            return SfxSynth.Normalize(mix, .8f);
        }

        private static float[] LegendarySky()
        {
            var mix = SfxSynth.Buffer(.95f);
            var notes = new[] { C7, A6, G6, E6, C6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .05f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.7f - start, notes[i], Glass, .005f, 8f), .34f, start);
            }
            SfxSynth.Mix(mix, Thump(.36f, 88f, .1f), .8f, .48f);
            SfxSynth.Reverb(mix, .46f, 1.4f, .4f);
            return SfxSynth.Normalize(mix, .78f);
        }

        // ------------------------------------------------------------------
        // 보상 · 진행 · 장비
        // ------------------------------------------------------------------

        private static float[] LootLegendary()
        {
            var mix = SfxSynth.Buffer(.95f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.95f, C5, MusicBox, .012f, 4f), .3f);
            var notes = new[] { E6, G6, C7, E6 * 2f };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = i * .055f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, notes[i], Glass, .005f, 7f), .34f, start);
            }
            SfxSynth.Mix(mix, Breath(.85f, 2200f, 6500f, 9216u), .12f);
            SfxSynth.Reverb(mix, .5f, 1.4f, .38f);
            return SfxSynth.Normalize(mix, .8f);
        }

        private static float[] WaveStart()
        {
            var mix = SfxSynth.Buffer(.6f);
            SfxSynth.Mix(mix, Thump(.24f, 110f, .06f), .7f);
            SfxSynth.Mix(mix, Thump(.24f, 110f, .06f), .55f, .13f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.4f, G5, MusicBox, .008f, 5f), .45f, .26f);
            SfxSynth.Reverb(mix, .34f, 1f, .45f);
            return SfxSynth.Normalize(mix, .72f);
        }

        private static float[] StageAdvance()
        {
            var mix = SfxSynth.Buffer(.95f);
            SfxSynth.Mix(mix, Thump(.3f, 98f, .08f), .6f);
            var notes = new[] { G5, C6, E6, G6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var start = .04f + i * .07f;
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, notes[i], MusicBox, .007f, 5f), .4f, start);
            }
            SfxSynth.Mix(mix, Breath(.8f, 1800f, 5200f, 3033u), .12f);
            SfxSynth.Reverb(mix, .46f, 1.3f, .4f);
            return SfxSynth.Normalize(mix, .8f);
        }

        private static float[] Equip()
        {
            var mix = SfxSynth.Buffer(.4f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.16f, C5, Marimba, .005f), .6f);
            SfxSynth.Mix(mix, SfxSynth.Bell(.34f, C7, Glass, .005f, 7f), .45f, .03f);
            SfxSynth.Reverb(mix, .3f, .9f, .42f);
            return SfxSynth.Normalize(mix, .68f);
        }

        /// <summary>오프라인 보상. 오르골 음이 흩뿌려지듯 쏟아집니다.</summary>
        private static float[] RewardClaim()
        {
            var mix = SfxSynth.Buffer(.95f);
            var rng = new SfxSynth.Rng(1919u);
            var scale = new[] { C6, D6, E6, G6, A6, C7 };

            for (var i = 0; i < 9; i++)
            {
                var hz = scale[Mathf.Clamp((int)(rng.Unit() * scale.Length), 0, scale.Length - 1)];
                var start = rng.Range(0f, .48f);
                SfxSynth.Mix(mix, SfxSynth.Bell(.95f - start, hz, Glass, .005f, 7f), rng.Range(.16f, .3f), start);
            }

            SfxSynth.Mix(mix, SfxSynth.Bell(.9f, C5, MusicBox, .012f, 4f), .3f);
            SfxSynth.Reverb(mix, .48f, 1.4f, .4f);
            return SfxSynth.Normalize(mix, .78f);
        }
    }
}
