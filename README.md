# Slot String

**English** | [简体中文](README.zh-CN.md)

A Unity 2021.3+ Package Manager package that provides a small runtime utility for resolving numeric slot placeholders in strings.

```csharp
var line = new SlotString("${0}: ${1} pts", host);
line.ToString();   // "Alice: 42 pts"
```


## Installation

In Unity, open Package Manager, choose **Add package from disk...**, and select this package's `package.json` file.

## Usage

All public types live in the `SlotStrings` namespace:

```csharp
using SlotStrings;
```

### Quick start

A `SlotString` interleaves fixed text with values pulled from a host. Placeholders use `${N}` syntax (any non-negative integer slot number — slots can be sparse, non-contiguous, anything).

```csharp
using SlotStrings;

// 1. Implement ISlotStringHost: your data source for placeholder values.
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

    // Numeric slots map to whatever data you like.
    public string Access(int slot) => slot switch
    {
        0 => _playerName,
        1 => _score.ToString(),
        _ => throw new System.ArgumentOutOfRangeException(nameof(slot)),
    };

    // Returned value is opaque — see "Caching and state-token invalidation" below.
    public int GetStateToken() => _token;
}

// 2. Construct a SlotString and render it.
var host = new ScoreBoardHost();
var line = new SlotString("${0}: ${1} pts", host);

string rendered = line.ToString();   // "Alice: 0 pts"
rendered        = line.ToString();   // "Alice: 0 pts"  — cache hit; Access not called

host.Score      = 42;                // setter bumps the state token
rendered        = line.ToString();   // "Alice: 42 pts" — token changed; recomputes
```

### Loading templates from external sources

The raw string can come from anywhere — most commonly a Unity `ScriptableObject`, but also localization tables, JSON, server config, or a literal. The library only sees a `string`.

```csharp
[CreateAssetMenu]
public class LogTemplates : ScriptableObject
{
    public string OnHit = "${0} dealt ${1} damage to ${2}";
}

var line = new SlotString(templates.OnHit, host);
```

The raw is read once during construction and not retained — whether it later changes is irrelevant. To use a different raw, construct a new `SlotString`.

### Placeholder syntax

- `${0}`, `${1}`, `${42}` — any non-negative integer slot. Slots are just int identifiers; they need not be 0-based, contiguous, or sequential.
- Malformed tokens (`${}`, `${abc}`, unclosed `${0`, overflowing slot number) are kept verbatim. The parser never throws.
- **No escape syntax.** Every well-formed `${N}` is interpreted as a placeholder; there is no way to write a literal `${0}` in the template itself. If you need one in the output, expose it as a host value and reference it from a different slot — `Format` appends host values verbatim and does not re-parse them:

  ```csharp
  // Want output: "Use ${0} to insert your name"
  var line = new SlotString("Use ${0} to insert your name", host);
  // host.Access(0) returns the literal string "${0}"
  ```

### Caching and state-token invalidation

`ToString()` caches output while `GetStateToken()` returns the same value as on the previous call. When the token differs, the next `ToString()` recomputes and refreshes the cache.

The contract is **equality-based**, not monotonic — any int that changes when data changes works:

```csharp
// Counter — bump on every mutation.
public int GetStateToken() => _token;

// Hash — derived from current state, no manual bookkeeping.
public int GetStateToken() => System.HashCode.Combine(_playerName, _score);
```

If data mutates without the token changing, `ToString()` returns stale text — the only failure mode to avoid.

`ToStringForce()` skips the cache check and always recomputes, also refreshing the cache. Use it when the token protocol can't be trusted (debugging, editor inspector).

### Sharing a parsed template

`SlotStringTemplate` is immutable — parse once, reuse across many `SlotString` instances:

```csharp
var template = new SlotStringTemplate("${0} dealt ${1} damage to ${2}");

foreach (var entryHost in combatLogHosts)
    new SlotString(template, entryHost);
```

Each `SlotString` has its own cache; only the template is shared.

### Host contract: `Access` must not return null

If `Access(slot)` returns `null`, rendering throws `InvalidOperationException` with the offending slot. For graceful degradation, handle it inside `Access`:

```csharp
public string Access(int slot) => slot switch
{
    0 => _playerName ?? string.Empty,
    _ => string.Empty,   // unmapped → empty instead of throwing
};
```


