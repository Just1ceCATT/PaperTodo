using System.Globalization;

namespace PaperTodo;

internal static class InputLimitNoticeStrings
{
    internal static string TodoPasteTruncatedTitle => Localized(
        "粘贴内容过多",
        "Paste partially inserted",
        "一部のみ貼り付けました",
        "붙여넣기 일부만 삽입됨");

    internal static string TodoPasteTruncatedMessage(int maximumItems, int omittedItems) =>
        string.Format(
            UiLanguages.EffectiveCulture,
            Localized(
                "本次粘贴最多支持 {0} 条待办，已插入前 {0} 条，其余 {1} 条未插入。",
                "This paste supports at most {0} todos. The first {0} were inserted; {1} were not inserted.",
                "一度に貼り付けられる ToDo は最大 {0} 件です。先頭の {0} 件を挿入し、残り {1} 件は挿入しませんでした。",
                "한 번에 붙여넣을 수 있는 할 일은 최대 {0}개입니다. 앞의 {0}개를 삽입했고 나머지 {1}개는 삽입하지 않았습니다."),
            maximumItems,
            omittedItems);

    internal static string NoteLimitTitle => Localized(
        "笔记已达上限",
        "Note limit reached",
        "ノートの上限に達しました",
        "메모 한도에 도달함");

    internal static string NoteLimitMessage(int maximumCharacters) =>
        string.Format(
            UiLanguages.EffectiveCulture,
            Localized(
                "笔记最多支持 {0} 个字符。请删除部分内容后再继续输入。",
                "A note can contain at most {0} characters. Delete some content before typing more.",
                "ノートは最大 {0} 文字です。続けて入力するには一部の内容を削除してください。",
                "메모는 최대 {0}자까지 입력할 수 있습니다. 계속 입력하려면 일부 내용을 삭제해 주세요."),
            maximumCharacters);

    private static string Localized(
        string chinese,
        string english,
        string japanese,
        string korean) =>
        UiLanguages.EffectiveUiCulture.TwoLetterISOLanguageName switch
        {
            "en" => english,
            "ja" => japanese,
            "ko" => korean,
            _ => chinese
        };
}
