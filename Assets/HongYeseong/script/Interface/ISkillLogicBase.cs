/// <summary>
/// 모든 스킬 로직 클래스가 구현해야 하는 인터페이스.
/// CoolTime.UseSkill() 이 스킬 오브젝트를 풀에서 꺼낸 뒤
/// SetOwner 를 호출해 playerStat 을 주입하고 스킬을 활성화합니다.
/// </summary>
public interface ISkillLogicBase
{
    /// <summary>
    /// 스킬 소유자(시전자) 의 CharaStat 을 주입한다.
    /// PrefabInfo 스탯 스케일링 + OnEnable 에서 처리해야 할 버프/효과를 여기서 실행한다.
    /// </summary>
    void SetOwner(CharaStat ownerStat);
}
