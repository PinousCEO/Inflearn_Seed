using IdleBattle.Audio;
using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipmentPresenter : MonoBehaviour
    {
        // Awake의 첫 수납과, 이미 같은 상태인데 다시 부르는 경우에는 소리를 내지 않습니다.
        private bool hasAppliedState;
        private bool isDrawn;

        [Header("Weapon Objects")]
        [Tooltip("전투 중 캐릭터가 손에 들고 있는 장비 오브젝트")]
        [SerializeField] private GameObject[] equippedWeapon;

        [Tooltip("비전투 중 캐릭터에 수납된 장비 오브젝트")]
        [SerializeField] private GameObject stowedWeapon;

        private void Awake()
        {
            SheatheWeapon();
        }

        public void DrawWeapon()
        {
            SetWeaponDrawn(true);
        }

        public void SheatheWeapon()
        {
            SetWeaponDrawn(false);
        }

        public void SetWeaponDrawn(bool isDrawn)
        {
            // 비어 있는 칸이 하나라도 있으면 스킬 시작 시점에 예외가 나 전투가 끊기므로 건너뜁니다.
            if (equippedWeapon != null)
                for(int i = 0; i < equippedWeapon.Length; i++)
                    if (equippedWeapon[i] != null)
                        equippedWeapon[i].SetActive(isDrawn);

            if (stowedWeapon != null)
                stowedWeapon.SetActive(!isDrawn);

            if (hasAppliedState && isDrawn != this.isDrawn)
                AudioManager.PlayAt(isDrawn ? SfxId.WeaponDraw : SfxId.WeaponSheathe, transform.position);

            this.isDrawn = isDrawn;
            hasAppliedState = true;
        }
    }
}
