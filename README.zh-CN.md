# Slot String

[English](README.md) | **简体中文**

一个 Unity 2021.3+ Package Manager 包，提供把字符串里的数字占位符解析为运行时值的小型工具。

```csharp
var line = new SlotString("${0}: ${1} pts", host);
line.ToString();   // "Alice: 42 pts"
```


## 安装

在 Unity 中打开 Package Manager，选择 **Add package from disk...**，选中本包的 `package.json` 文件。

## 用法

所有公开类型都在 `SlotStrings` 命名空间下：

```csharp
using SlotStrings;
```

### 快速开始

`SlotString` 把固定文本和从 host 取出的值交错拼接成一个字符串。占位符语法是 `${N}`，`N` 是非负整数索引。

```csharp
using SlotStrings;

// 1. 实现 ISlotStringHost：你的占位符数据源。
public sealed class ScoreBoardHost : ISlotStringHost
{
    private string _playerName = "Alice";
    private int _score;
    private int _token;

    public string PlayerName
    {
        get => _playerName;
        set { _playerName = value; _token++; }
    }

    public int Score
    {
        get => _score;
        set { _score = value; _token++; }
    }

    // 数字索引映射到你想暴露的任意数据。
    public string Access(int index) => index switch
    {
        0 => _playerName,
        1 => _score.ToString(),
        _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
    };

    // 返回值是不透明的——见下文「缓存与状态 token 失效」。
    public int GetStateToken() => _token;
}

// 2. 构造一个 SlotString，按需渲染。
var host = new ScoreBoardHost();
var line = new SlotString("${0}: ${1} pts", host);

string rendered = line.ToString();   // "Alice: 0 pts"
rendered        = line.ToString();   // "Alice: 0 pts"  — 命中缓存，没有调用 Access

host.Score      = 42;                // setter 自增了 state token
rendered        = line.ToString();   // "Alice: 42 pts" — token 变了，重新计算
```

### 从外部数据源加载模板

raw 字符串可以来自任何地方——最常见的是 Unity `ScriptableObject`，也可以是本地化表、JSON、服务器配置，或者字符串字面量。库只看到一个 `string`。

```csharp
[CreateAssetMenu]
public class LogTemplates : ScriptableObject
{
    public string OnHit = "${0} dealt ${1} damage to ${2}";
}

var line = new SlotString(templates.OnHit, host);
```

raw 在构造时被读取一次，之后不再持有——之后是否变化与库无关。想换 raw，重新构造一个 `SlotString` 即可。

### 占位符语法

- `${0}`、`${1}`、`${42}` —— 任意非负整数索引。
- 格式错误的 token（`${}`、`${abc}`、未闭合的 `${0`、溢出的索引）会原样保留为字面量。解析器从不抛异常。
- **没有转义语法。** 每个格式合法的 `${N}` 都会被当成占位符解析；没有办法在模板里直接写出一个字面量的 `${0}`。如果输出里就是要包含 `${0}` 这种文本，把它放在 host 那一侧，从一个别的占位符索引取出来即可 —— `Format` 把 host 返回的字符串原样 append，不会二次解析：

  ```csharp
  // 想要输出："Use ${0} to insert your name"
  var line = new SlotString("Use ${0} to insert your name", host);
  // host.Access(0) 返回字面量字符串 "${0}"
  ```

### 缓存与状态 token 失效

只要 `GetStateToken()` 返回的值与上次调用相同，`ToString()` 就复用缓存的输出。token 一旦不同，下次 `ToString()` 就会重新计算并刷新缓存。

契约是 **基于等值的**，不要求单调递增——任何「数据变化时也跟着变化」的 int 都满足要求：

```csharp
// 计数器风格 —— 每次数据变更时自增。
public int GetStateToken() => _token;

// hash 风格 —— 从当前状态派生，无需手动维护。
public int GetStateToken() => System.HashCode.Combine(_playerName, _score);
```

如果数据变了但 token 没跟着变，`ToString()` 会一直返回过期文本——这是唯一需要避免的失败模式。

`ToStringForce()` 跳过缓存检查，每次都重新计算，并把新值回写缓存。适用于不能信任 token 协议的场景（调试、editor inspector）。

### 共享一个已解析的模板

`SlotStringTemplate` 不可变——解析一次，可以在多个 `SlotString` 实例之间复用：

```csharp
var template = new SlotStringTemplate("${0} dealt ${1} damage to ${2}");

foreach (var entryHost in combatLogHosts)
    new SlotString(template, entryHost);
```

每个 `SlotString` 有自己的缓存；只有 template 是共享的。

### Host 契约：`Access` 不能返回 null

如果 `Access(index)` 返回 `null`，渲染会抛 `InvalidOperationException`，message 里带出问题索引。需要优雅降级时，在你自己的 `Access` 里处理：

```csharp
public string Access(int index) => index switch
{
    0 => _playerName ?? string.Empty,
    _ => string.Empty,   // 未映射的索引 → 返回空串而不是抛
};
```
