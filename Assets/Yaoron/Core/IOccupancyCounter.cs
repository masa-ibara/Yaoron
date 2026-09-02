namespace Yaoron.Core
{
    /// <summary>
    /// ルームの在室人数を知っている実体 (実装は Avatar 層の YaAvatarManager)。
    /// Net 層が Avatar 層を直接参照して循環しないよう、YaServices 越しにこの型で受け渡す。
    /// </summary>
    public interface IOccupancyCounter
    {
        /// <summary>自分を含む、現在ルームに存在するアバター数。</summary>
        int Occupants { get; }
    }
}
