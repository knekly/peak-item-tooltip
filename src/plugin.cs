/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    plugin.cs
 * @brief   entry point and per-frame loop that resolves the hovered or held item and drives the tooltip
 *          together with live config and description reloads
 */
using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace PeakItemTooltip
{
    /**
     * @brief bepinex plugin entry point that runs the per-frame loop, driving the item tooltip
     */
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "peak-item-tooltip";
        public const string PluginName = "peak-item-tooltip";
        public const string PluginVersion = "0.1.1";

        internal static ManualLogSource Log;

        // item key -> description store from json file
        private Descriptions _descriptions;

        // user-tweakable config + the widget it drives
        private PluginConfig _config;
        private Widget _widget;

        // the item shown last frame, so we only rebuild the widget on change
        private Item _lastItem;

        // periodic content refresh so live changes to a held/hovered item (e.g. cooking, which
        // keeps the same Item instance) update the widget instead of going stale
        private float _nextContentRefresh;
        private const float ContentRefreshInterval = 0.25f;

        // set whenever the config changes; consumed next frame to re-apply the widget layout
        // without rebuilding it every frame
        private bool _configDirty;

        // disk polling for the description file and the config file
        private float _nextReloadCheck;
        private const float ReloadCheckInterval = 1f;
        private DateTime _configWriteUtc;

        /**
         * @brief sets up the descriptions store, config, and widget on plugin load, and starts tracking config changes
         */
        private void Awake()
        {
            // initialise logger for bepinex console
            Log = Logger;
            // plugin + widget configs can be bound immediately (does not depend on scene state)
            _config = new PluginConfig(Config);
            _configWriteUtc = SafeConfigWriteTime();
        }

        /**
         * @brief initialises the descriptions store and widget once the game scene is up.
         *        Description language selection (Auto) reads LocalizedText.CURRENT_LANGUAGE,
         *        which is only set after the game's LocalizedText component has initialised —
         *        doing this in Awake could pick the wrong description file for non-English games.
         */
        private void Start()
        {
            _descriptions = new Descriptions();
            _descriptions.Initialize();
            _widget = new Widget(_config);

            // print successful load log message
            Log.LogInfo($"{PluginName} [{PluginVersion}] loaded •ᴗ•");
        }

        /**
         * @brief per-frame loop that reloads changed files, resolves the target item, and shows or hides the widget
         */
        private void Update()
        {
            // periodically re-read the description + config files so edits apply without a restart;
            // toggled by HotReload config: it's a dev tuning convenience so players who never edit these files
            // mid session can switch it off to stop disk polling
            if (_config.HotReload.Value && Time.unscaledTime >= _nextReloadCheck)
            {
                _nextReloadCheck = Time.unscaledTime + ReloadCheckInterval;
                _descriptions.MaybeReload();
                MaybeReloadConfig();
            }

            // assign return value to target item
            Item target = ResolveTargetItem();

            // hide widget when a consumable is eaten/destroyed
            if (target == null)
            {
                _widget.Hide();
                _lastItem = null;
                _configDirty = false;
                return;
            }

            // refresh on item change, periodic timer or config change
            bool itemChanged = target != _lastItem;
            bool refreshDue = Time.unscaledTime >= _nextContentRefresh;
            if (itemChanged || refreshDue || _configDirty)
            {
                _lastItem = target;
                _nextContentRefresh = Time.unscaledTime + ContentRefreshInterval;
                _configDirty = false;
                _widget.Show(target, _descriptions);
            }
        }

        /**
         * @brief returns the item to describe: the held item while one is held (holding enabled), otherwise the
         *        hovered item, or null when there is nothing to show
         */
        private Item ResolveTargetItem()
        {
            Interaction interaction = Interaction.instance;
            if (interaction == null) return null;

            // a held item keeps the tooltip until it is dropped or unequipped: while holding, it takes
            // priority over anything hovered so hovering another item does not override the held tooltip
            if (_config.ShowWhileHolding.Value)
            {
                Character local = Character.localCharacter;
                Item held = local != null && local.data != null ? local.data.currentItem : null;
                if (held != null) return held;
            }

            // otherwise describe the hovered item; the as operator makes hovers on non-items resolve to null
            return interaction.currentHovered as Item;
        }

        /**
         * @brief enables hot reloading, re-reads the config file from disk when it has changed so edits 
         *        take effect live
         *        
         */
        private void MaybeReloadConfig()
        {
            try
            {
                DateTime writeUtc = SafeConfigWriteTime();
                if (writeUtc != _configWriteUtc)
                {
                    _configWriteUtc = writeUtc;
                    Config.Reload();     // re-read the .cfg values into the ConfigEntry objects
                    _configDirty = true; // flag the widget to re-apply layout next frame
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Config reload check failed: {e.Message}");
            }
        }

        /**
         * @brief returns the config file's last write time, or default if the file does not exist
         */
        private DateTime SafeConfigWriteTime()
        {
            string path = Config.ConfigFilePath;
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
        }
    }
}
