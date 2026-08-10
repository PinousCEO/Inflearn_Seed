namespace IdleBattle.Audio
{
    /// <summary>
    /// 게임에서 쓰는 효과음 목록입니다.
    ///
    /// 이름은 그대로 리소스 경로가 됩니다. <c>Assets/Resources/Audio/{이름}.wav</c>를 넣어 두면
    /// 코드로 합성한 기본 소리 대신 그 음원이 재생됩니다(예: <c>Assets/Resources/Audio/EnemyHit.wav</c>).
    /// </summary>
    public enum SfxId
    {
        None = 0,

        // ── UI 공통 ──────────────────────────────────────────────
        UiClick,
        UiSelect,
        UiTabSwitch,
        UiPanelOpen,
        UiPanelClose,
        UiToast,
        UiPopupOpen,
        UiConfirm,
        UiCancel,
        UiDenied,

        // ── 게임 흐름 ────────────────────────────────────────────
        SceneFade,
        LoadingComplete,
        NetworkLost,
        NetworkRestored,

        // ── 타이틀 · 캐릭터 선택 ─────────────────────────────────
        TitleTap,
        LoginSuccess,
        LoginFailed,
        CharacterFocus,
        StatTick,
        AdventureStart,

        // ── 전투 · 플레이어 ──────────────────────────────────────
        // 스킬 소리는 SkillData의 animationIndex(0~4)로 고릅니다.
        // 그 번호가 곧 재생되는 애니메이션(Character 애니메이터의 Skill1~Skill5)이자
        // Character/Skill0의 VFX(1-1~1-4) 번호이기 때문입니다.
        // 4번만 예외로, Skill0 대신 01_Prefabs/Effects/Row 프리팹을 그 자리에 띄웁니다.
        SkillCleave,        // 0 · Savage Cleave
        SkillGroundSmash,   // 1 · Ground Smash
        SkillLeapCrush,     // 2 · Leap Crush
        SkillEarthshatter,  // 3 · Earthshatter
        SkillBattleRoar,    // 4 · Battle Roar
        WeaponDraw,
        WeaponSheathe,
        PlayerHurt,
        PlayerDeath,
        PlayerRevive,
        LevelUp,
        PotionHeal,
        PotionMana,
        Footstep,

        // ── 전투 · 몬스터 ────────────────────────────────────────
        EnemySpawn,
        BossSpawn,
        EnemyHit,
        EnemyCritical,
        EnemyDeath,
        EnemyAttack,
        EnemyStun,
        EnemySlow,

        // ── 전설 장비 특수효과 ───────────────────────────────────
        LegendaryTornado,
        LegendaryQuake,
        LegendaryExplosion,
        LegendarySky,

        // ── 보상 · 진행 ──────────────────────────────────────────
        CoinDrop,
        CoinPickup,
        LootCommon,
        LootRare,
        LootEpic,
        LootLegendary,
        LootPickup,
        WaveStart,
        StageAdvance,

        // ── 장비 · 성장 ──────────────────────────────────────────
        Equip,
        Unequip,
        PassiveInvest,
        PassiveRefund,

        // ── 우편 · 랭킹 · 오프라인 보상 ──────────────────────────
        MailClaim,
        MailClaimAll,
        RewardClaim
    }
}
