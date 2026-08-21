using System.Globalization;

namespace IRUZ.ViewModels;

/// <summary>
/// 「〇時間後に自動解除する」の選択肢。<see cref="Hours"/> が 0 以下なら自動解除しない。
/// ComboBox には <see cref="ToString"/> の文言がそのまま表示される。
/// </summary>
/// <param name="Hours">自動解除までの時間（時）。</param>
public sealed record AutoStopOption(int Hours)
{
    /// <summary>
    /// 自動解除しない選択肢（既定値）。
    /// </summary>
    public static AutoStopOption None { get; } = new(0);

    /// <summary>
    /// ComboBox に表示する文言を返す。
    /// </summary>
    /// <returns>「なし」または「3時間後」形式の文言。</returns>
    public override string ToString() =>
        Hours <= 0 ? "なし" : string.Create(CultureInfo.InvariantCulture, $"{Hours}時間後");
}
