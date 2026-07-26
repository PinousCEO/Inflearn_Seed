using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipmentPresenter : MonoBehaviour
    {
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
            if (equippedWeapon != null)
                for(int i = 0; i < equippedWeapon.Length; i++)
                    equippedWeapon[i].SetActive(isDrawn);

            if (stowedWeapon != null)
                stowedWeapon.SetActive(!isDrawn);
        }
    }
}
