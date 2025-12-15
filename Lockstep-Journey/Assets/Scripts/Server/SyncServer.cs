using System.Collections.Generic;

/// <summary>
/// 帧同步服务器：负责收集、打包和发送权威输入
/// </summary>
public class SyncServer
{
    // 存储每帧的输入，Key: 帧号 (int), Value: {玩家ID (string): 输入数据 (PlayerInput)}
    private Dictionary<int, Dictionary<int, PlayerInput>> authoritativeInputs =
        new Dictionary<int, Dictionary<int, PlayerInput>>();

    // 假设服务器始终领先于客户端，用于模拟网络延迟后的输入返回

    /// <summary>
    /// 客户端发送输入到服务器
    /// </summary>
    public void ReceiveInput(int frame, int playerID, PlayerInput input)
    {
        if (!authoritativeInputs.ContainsKey(frame))
        {
            authoritativeInputs[frame] = new Dictionary<int, PlayerInput>();
        }
        // 存储该帧该玩家的输入
        authoritativeInputs[frame][playerID] = input;
    }

    /// <summary>
    /// 客户端请求某个特定帧的权威输入包 (模拟网络延迟后到达)
    /// </summary>
    /// <param name="frame">客户端期待收到的权威帧号</param>
    /// <returns>该帧的所有权威输入，如果尚未收到则返回 null</returns>
    public Dictionary<int, PlayerInput> GetAuthoritativeInputs(int frame)
    {
        if (authoritativeInputs.ContainsKey(frame))
        {
            return authoritativeInputs[frame];
        }
        return null;
    }
}