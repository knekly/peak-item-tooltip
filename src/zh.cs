/**
 * @authors @gintoki000 
 * @file    zh.cs
* @brief    decides whether tooltip labels should render in Simplified Chinese.
*           Mode is controlled by the "Localization > Language" config entry:
*           Auto  -> follow the game's current language
*           zh-CN -> always Simplified Chinese
*           en    -> always English
 */
namespace PeakItemTooltip
{
    internal static class Zh
    {
        public static bool IsActive()
        {
            try
            {
                string mode = PluginConfig.LanguageMode?.Value ?? "Auto";
                if (mode == "zh-CN") return true;
                if (mode == "en") return false;
                return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese;
            }
            catch
            {
                // if the game language can't be read for any reason, fall back to English labels
                return false;
            }
        }
    }
}
