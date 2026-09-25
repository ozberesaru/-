using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Srpg.Core;
using Srpg.Core.Combat;
using Srpg.Core.Combat.Sandbox;

// Fully automatic (level 5) battle rendered as ASCII, for eyeballing corps AI behaviour.
// Usage: dotnet run -- [seed] [--every N] [--log]
internal static class Program
{
    private static int Main(string[] args)
    {
        ulong seed = 7;
        int every = 3;
        bool printLog = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--every") every = int.Parse(args[++i]);
            else if (args[i] == "--log") printLog = true;
            else seed = ulong.Parse(args[i]);
        }
        Console.OutputEncoding = Encoding.UTF8;

        var battle = SandboxBattle.Create(seed);
        Console.WriteLine($"种子 {seed} · 地图 {battle.Map.Width}×{battle.Map.Height} · 双方各 {battle.Units.Count / 2} 人 · 回合限制 {battle.TurnLimit}");
        Console.WriteLine("地形 . 平地  T 森林  ^ 山地  F 堡垒  ~ 水   单位 A-D 我方兵团  w-z 敌方兵团  * 打晕  ! 投降  ? 溃逃中");
        Console.WriteLine();
        Render(battle, "开战前");

        battle.Start();
        int lastRound = 1;
        while (battle.Outcome == BattleOutcome.Ongoing)
        {
            battle.RunAiActivation();
            if (battle.Round != lastRound)
            {
                if ((battle.Round - 1) % every == 0 && battle.Outcome == BattleOutcome.Ongoing)
                    Render(battle, $"第 {battle.Round - 1} 轮结束");
                lastRound = battle.Round;
            }
        }

        Render(battle, $"战斗结束:{OutcomeText(battle.Outcome)}(第 {Math.Min(battle.Round, battle.TurnLimit)} 轮)");
        Summary(battle);
        if (printLog) foreach (var line in battle.Log) Console.WriteLine(line);
        return 0;
    }

    private static void Render(Battle b, string title)
    {
        Console.WriteLine($"== {title} ==");
        var sb = new StringBuilder();
        for (int y = 0; y < b.Map.Height; y++)
        {
            for (int x = 0; x < b.Map.Width; x++)
            {
                var p = new GridPos(x, y);
                var u = b.UnitAt(p);
                sb.Append(u == null ? BattleMap.ToChar(b.Map.Get(p)) : Glyph(b, u));
            }
            sb.AppendLine();
        }
        Console.Write(sb.ToString());
        Console.WriteLine();
    }

    private static char Glyph(Battle b, Unit u)
    {
        switch (u.Status)
        {
            case UnitStatus.Stunned: return '*';
            case UnitStatus.Surrendered: return '!';
            case UnitStatus.Routed: return '?';
        }
        int index = b.Corps.Where(c => c.Side == u.Side).ToList().FindIndex(c => c.Id == u.CorpsId);
        return u.Side == Side.Player ? (char)('A' + index) : (char)('w' + index);
    }

    private static void Summary(Battle b)
    {
        Console.WriteLine("兵团            在场  倒下  溃逃中  逃离  投降/打晕  兵团长");
        foreach (var c in b.Corps)
        {
            var m = b.Members(c).ToList();
            var leader = b.GetUnit(c.LeaderId);
            Console.WriteLine($"{c.Name,-8}{(c.Side == Side.Player ? "(我)" : "(敌)")}  " +
                              $"{m.Count(u => u.Status == UnitStatus.Active),4}  {m.Count(u => u.Status == UnitStatus.Downed),4}  " +
                              $"{m.Count(u => u.Status == UnitStatus.Routed),6}  {m.Count(u => u.Status == UnitStatus.Escaped),4}  " +
                              $"{m.Count(u => u.Capturable),9}  {StatusText(leader.Status)}");
        }
        var s = b.Score(Side.Player);
        Console.WriteLine();
        Console.WriteLine($"我方评分:速度 {s.Speed} · 伤亡 {s.Casualty} · 总分 {s.Total}");
        Console.WriteLine($"日志共 {b.Log.Count} 行(加 --log 打印)");
    }

    private static string OutcomeText(BattleOutcome o) => o switch
    {
        BattleOutcome.PlayerWon => "我方胜利",
        BattleOutcome.PlayerLost => "我方失败",
        BattleOutcome.TimeUp => "超过回合限制",
        _ => "进行中",
    };

    private static string StatusText(UnitStatus s) => s switch
    {
        UnitStatus.Active => "健在",
        UnitStatus.Downed => "倒下",
        UnitStatus.Routed => "溃逃中",
        UnitStatus.Escaped => "逃离",
        UnitStatus.Stunned => "被打晕",
        UnitStatus.Surrendered => "投降",
        UnitStatus.Carried => "被俘",
        _ => s.ToString(),
    };
}
