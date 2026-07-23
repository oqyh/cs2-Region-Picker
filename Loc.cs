using System.Collections.Generic;

namespace CS2RegionPicker;

public static class Loc
{
    public sealed record Language(string Code, string Native, bool Rtl);

    public static readonly List<Language> Languages = new()
    {
        new Language("en", "English", false),
        new Language("ar", "العربية", true),
        new Language("ru", "Русский", false),

    };

    public static string Current { get; private set; } = "en";

    public static bool IsRtl =>
        Languages.Find(l => l.Code == Current)?.Rtl ?? false;

    public static void Set(string code)
    {
        if (Strings.ContainsKey(code)) Current = code;
    }

    public static string T(string key, params object[] args)
        => string.Format(T(key), args);

    public static string T(string key)
    {
        if (Strings.TryGetValue(Current, out var table) && table.TryGetValue(key, out string? v))
            return v;
        if (Strings["en"].TryGetValue(key, out string? en))
            return en;
        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["en"] = new()
        {

            ["settings"]          = "⚙  Settings",
            ["about"]             = "About",
            ["you"]               = "YOU",

            ["search_placeholder"] = "Search a region…",
            ["mark_all"]          = "Mark All",
            ["unmark_all"]        = "UnMark All",

            ["apply"]             = "▶  Apply",
            ["working"]           = "⏳  Working…",
            ["busy_badge"]        = "🔒  Working… please wait",

            ["active_regions"]    = "Current Allowed Regions",

            ["confirm_all_blocked_title"] = "Every Region Is Blocked",
            ["confirm_all_blocked_msg"]   = "You have blocked every region, so CS2 matchmaking won't be able to connect to any server at all. Apply anyway?",
            ["confirm_apply_anyway"]      = "Apply Anyway",
            ["confirm_cancel"]            = "Cancel",
            ["nothing_applied"]   = "Nothing Applied Yet — Hit Apply",

            ["state_allowed"]     = "allowed",
            ["state_blocked"]     = "blocked",
            ["state_pending"]     = "not applied",

            ["settings_title"]    = "Settings",
            ["appearance"]        = "Appearance",
            ["appearance_sub"]    = "Dark or light theme",
            ["language"]          = "Language",
            ["language_sub"]      = "Interface language",

            ["about_title"]       = "About",
            ["created_by"]        = "Created By",
            ["about_blurb"]       = "Blocks Valve SDR Relay IPs So CS2 Matchmaking Skips Regions You Don't Want.",

            ["log_fetching"]      = "Fetching relay list from Valve…",
            ["log_loaded"]        = "{0} regions loaded.",
            ["log_data_changed"]  = "Valve relay data changed — re-applying your saved selection…",
            ["log_error"]         = "Error: {0}",
            ["log_fw_error"]      = "Firewall error: {0}",
            ["log_applying"]      = "Applying block on {0} regions…",
            ["log_applied"]       = "Block applied. {0} regions, {1} IPs.",
            ["log_verifying"]     = "Verifying firewall rules…",
            ["log_verify_ok"]     = "✓ Verified: {0}/{1} block rules are active.",
            ["log_verify_fail"]   = "⚠ Only {0}/{1} block rules were created. Something is preventing them.\n   Fix: disable your antivirus firewall, then Apply again.",
            ["log_not_enforced"]  = "⚠ The rules were created, but blocked regions are STILL reachable — your antivirus is overriding Windows Firewall and ignoring them.\n   Fix: disable your antivirus's firewall module (not the whole antivirus), then Apply again.",
            ["log_maxping"]       = "Important: in the CS2 console run ",
            ["log_maxping_hint"]  = "   Your slowest allowed region is ~{0} ms — if max ping is set below that, matchmaking will never find a server.",
            ["log_maxping_none"]  = "Important: set  mm_dedicated_search_maxping  high enough to reach your allowed regions, or matchmaking will find nothing.",
            ["log_located"]       = "Located {0} new region(s) on the map.",
            ["log_unlocated"]     = "⚠ {0} region(s) have no map position yet: {1} — will retry via IP lookup.",
            ["log_can_exit"]      = "✓ Done. You can close the app — the block stays active.",
        },

        ["ar"] = new()
        {
            ["settings"]          = "⚙  الإعدادات",
            ["about"]             = "حول",
            ["you"]               = "أنت",

            ["search_placeholder"] = "ابحث عن منطقة…",
            ["mark_all"]          = "تحديد الكل",
            ["unmark_all"]        = "إلغاء التحديد",

            ["apply"]             = "▶  تطبيق",
            ["working"]           = "⏳  جارٍ التنفيذ…",
            ["busy_badge"]        = "🔒  جارٍ التنفيذ… يُرجى الانتظار",

            ["active_regions"]    = "المناطق المسموحة حاليًا",

            ["confirm_all_blocked_title"] = "جميع المناطق محظورة",
            ["confirm_all_blocked_msg"]   = "لقد حظرت جميع المناطق، لذا لن تتمكّن مطابقة CS2 من الاتصال بأي خادم على الإطلاق. هل تريد التطبيق على أي حال؟",
            ["confirm_apply_anyway"]      = "تطبيق على أي حال",
            ["confirm_cancel"]            = "إلغاء",
            ["nothing_applied"]   = "لم يُطبَّق شيء بعد — اضغط «تطبيق»",

            ["state_allowed"]     = "مسموحة",
            ["state_blocked"]     = "محظورة",
            ["state_pending"]     = "غير مُطبَّقة",

            ["settings_title"]    = "الإعدادات",
            ["appearance"]        = "المظهر",
            ["appearance_sub"]    = "الوضع الداكن أو الفاتح",
            ["language"]          = "اللغة",
            ["language_sub"]      = "لغة الواجهة",

            ["about_title"]       = "حول",
            ["created_by"]        = "من إنشاء",
            ["about_blurb"]       = "يحظر عناوين IP الخاصة بمرحّلات Valve SDR، فتتجاوز مطابقة CS2 المناطق التي لا تريدها.",

            ["log_fetching"]      = "جارٍ جلب قائمة المرحّلات من Valve…",
            ["log_loaded"]        = "تم تحميل {0} منطقة.",
            ["log_data_changed"]  = "تغيّرت بيانات مرحّلات Valve — يُعاد تطبيق اختيارك المحفوظ…",
            ["log_error"]         = "خطأ: {0}",
            ["log_fw_error"]      = "خطأ في جدار الحماية: {0}",
            ["log_applying"]      = "جارٍ تطبيق الحظر على {0} منطقة…",
            ["log_applied"]       = "تم تطبيق الحظر على {0} منطقة، و{1} عنوان IP.",
            ["log_verifying"]     = "جارٍ التحقق من قواعد جدار الحماية…",
            ["log_verify_ok"]     = "✓ تم التحقق: {0} من {1} من قواعد الحظر فعّالة.",
            ["log_verify_fail"]   = "⚠ لم يُنشأ سوى {0} من {1} من قواعد الحظر. هناك ما يمنع إنشاءها.\n   الحل: عطّل جدار حماية برنامج مكافحة الفيروسات، ثم اضغط «تطبيق» مجددًا.",
            ["log_not_enforced"]  = "⚠ تم إنشاء القواعد، لكن المناطق المحظورة ما زالت قابلة للوصول — برنامج مكافحة الفيروسات لديك يتجاوز جدار حماية Windows ويتجاهلها.\n   الحل: عطّل وحدة جدار الحماية في برنامج مكافحة الفيروسات (وليس البرنامج كله)، ثم اضغط «تطبيق» مجددًا.",
            ["log_maxping"]       = "مهم: نفّذ في وحدة تحكّم CS2 الأمر ",
            ["log_maxping_hint"]  = "   أبطأ منطقة مسموحة لديك نحو {0} ms — إذا ضبطت الحد الأقصى أقل من ذلك، فلن تعثر المطابقة على أي خادم.",
            ["log_maxping_none"]  = "مهم: اضبط  mm_dedicated_search_maxping  على قيمة تكفي للوصول إلى مناطقك المسموحة، وإلا فلن تعثر المطابقة على أي خادم.",
            ["log_located"]       = "تم تحديد موقع {0} منطقة جديدة على الخريطة.",
            ["log_unlocated"]     = "⚠ {0} منطقة (مناطق) بلا موقع على الخريطة بعد: {1} — سيُعاد البحث عبر عنوان IP.",
            ["log_can_exit"]      = "✓ تم. يمكنك إغلاق التطبيق — سيبقى الحظر فعّالًا.",
        },

        ["ru"] = new()
        {
            ["settings"]          = "⚙  Настройки",
            ["about"]             = "О программе",
            ["you"]               = "ВЫ",

            ["search_placeholder"] = "Поиск региона…",
            ["mark_all"]          = "Выбрать все",
            ["unmark_all"]        = "Снять выбор",

            ["apply"]             = "▶  Применить",
            ["working"]           = "⏳  Выполняется…",
            ["busy_badge"]        = "🔒  Выполняется… подождите",

            ["active_regions"]    = "Текущие разрешённые регионы",

            ["confirm_all_blocked_title"] = "Все регионы заблокированы",
            ["confirm_all_blocked_msg"]   = "Вы заблокировали все регионы, поэтому подбор матчей CS2 вообще не сможет подключиться ни к одному серверу. Применить всё равно?",
            ["confirm_apply_anyway"]      = "Применить всё равно",
            ["confirm_cancel"]            = "Отмена",
            ["nothing_applied"]   = "Пока ничего не применено — нажмите «Применить»",

            ["state_allowed"]     = "разрешён",
            ["state_blocked"]     = "заблокирован",
            ["state_pending"]     = "не применён",

            ["settings_title"]    = "Настройки",
            ["appearance"]        = "Оформление",
            ["appearance_sub"]    = "Тёмная или светлая тема",
            ["language"]          = "Язык",
            ["language_sub"]      = "Язык интерфейса",

            ["about_title"]       = "О программе",
            ["created_by"]        = "Автор",
            ["about_blurb"]       = "Блокирует IP-адреса ретрансляторов Valve SDR, чтобы подбор матчей CS2 пропускал ненужные вам регионы.",

            ["log_fetching"]      = "Получение списка ретрансляторов от Valve…",
            ["log_loaded"]        = "Загружено регионов: {0}.",
            ["log_data_changed"]  = "Данные ретрансляторов Valve изменились — повторно применяется ваш сохранённый выбор…",
            ["log_error"]         = "Ошибка: {0}",
            ["log_fw_error"]      = "Ошибка брандмауэра: {0}",
            ["log_applying"]      = "Применяется блокировка: {0} регион(ов)…",
            ["log_applied"]       = "Блокировка применена. Регионов: {0}, IP-адресов: {1}.",
            ["log_verifying"]     = "Проверка правил брандмауэра…",
            ["log_verify_ok"]     = "✓ Проверено: активно {0} из {1} правил блокировки.",
            ["log_verify_fail"]   = "⚠ Создано только {0} из {1} правил блокировки. Что-то мешает их созданию.\n   Решение: отключите брандмауэр антивируса и нажмите «Применить» ещё раз.",
            ["log_not_enforced"]  = "⚠ Правила созданы, но заблокированные регионы ВСЁ ЕЩЁ доступны — ваш антивирус подменяет брандмауэр Windows и игнорирует эти правила.\n   Решение: отключите модуль брандмауэра в антивирусе (не весь антивирус) и нажмите «Применить» ещё раз.",
            ["log_maxping"]       = "Важно: в консоли CS2 выполните команду ",
            ["log_maxping_hint"]  = "   Самый медленный разрешённый регион — около {0} мс; если максимальный пинг задан ниже, подбор матчей не найдёт ни одного сервера.",
            ["log_maxping_none"]  = "Важно: задайте  mm_dedicated_search_maxping  достаточно высоким, чтобы доставать до разрешённых регионов, иначе подбор матчей ничего не найдёт.",
            ["log_located"]       = "Определено местоположение новых регионов на карте: {0}.",
            ["log_unlocated"]     = "⚠ Регионов без позиции на карте: {0} ({1}) — будет повторная попытка через IP.",
            ["log_can_exit"]      = "✓ Готово. Можно закрыть приложение — блокировка останется активной.",
        },
    };
}
