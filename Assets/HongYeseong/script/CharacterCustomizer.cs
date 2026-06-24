using UnityEngine;

/// <summary>
/// 캐릭터 아머/바디 파츠 커스터마이징 (스텁 - missing script 복구용)
/// guid: c7332e7a20bce8a4ca19d193327fae39
/// </summary>
public class CharacterCustomizer : MonoBehaviour
{
    public Transform armorPartsRoot;
    public Transform bodyPartsRoot;
    public Transform facePartsRoot;
    public bool helmetVisible = false;
    public bool syncHairColor = true;
    public string armorLocks = "000000000000";
    public string faceLocks  = "000000000000";
}
