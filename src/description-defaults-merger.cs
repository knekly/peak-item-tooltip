using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PeakItemTooltip
{
    /// <summary>
    /// Adds fields from embedded defaults to an existing description document without replacing
    /// any values already authored by the user.
    /// </summary>
    public static class DescriptionDefaultsMerger
    {
        public static List<string> MergeMissing(JObject file, JObject defaults)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));

            var added = new List<string>();
            foreach (string section in new[] { "types", "items" })
            {
                var source = defaults[section] as JObject;
                if (source == null) continue;

                var target = file[section] as JObject;
                if (target == null)
                {
                    file[section] = source.DeepClone();
                    added.Add(section);
                    continue;
                }

                if (section == "items")
                {
                    foreach (JProperty item in source.Properties())
                    {
                        var defaultsForItem = item.Value as JObject;
                        if (defaultsForItem == null) continue;

                        JObject existing = target.Properties()
                            .FirstOrDefault(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase))?.Value as JObject;
                        if (existing == null)
                        {
                            target[item.Name] = defaultsForItem.DeepClone();
                            added.Add(item.Name);
                            continue;
                        }

                        // Existing base fields (type/description) and unrelated custom fields are
                        // user-owned. Only enrich these newer structured fields.
                        foreach (string field in new[] { "context", "stateDescriptions", "stateChecks", "wiki" })
                        {
                            JToken incoming = defaultsForItem[field];
                            if (incoming == null) continue;

                            JProperty currentField = existing.Properties()
                                .FirstOrDefault(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));
                            if (currentField == null)
                            {
                                existing[field] = incoming.DeepClone();
                                added.Add(item.Name + "." + field);
                                continue;
                            }

                            if (field == "wiki" && incoming is JObject incomingWiki && currentField.Value is JObject existingWiki)
                            {
                                foreach (JProperty metadata in incomingWiki.Properties())
                                {
                                    if (existingWiki.Properties().Any(p => string.Equals(p.Name, metadata.Name, StringComparison.OrdinalIgnoreCase))) continue;
                                    existingWiki[metadata.Name] = metadata.Value.DeepClone();
                                    added.Add(item.Name + ".wiki." + metadata.Name);
                                }
                                continue;
                            }

                            if ((field != "stateDescriptions" && field != "stateChecks")
                                || !(incoming is JObject incomingStates) || !(currentField.Value is JObject existingStates)) continue;

                            foreach (JProperty state in incomingStates.Properties())
                            {
                                if (existingStates.Properties().Any(p => string.Equals(p.Name, state.Name, StringComparison.OrdinalIgnoreCase))) continue;
                                existingStates[state.Name] = state.Value.DeepClone();
                                added.Add(item.Name + "." + field + "." + state.Name);
                            }
                        }
                    }
                }
                else
                {
                    var present = new HashSet<string>(target.Properties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (JProperty prop in source.Properties())
                    {
                        if (!present.Add(prop.Name)) continue;

                        target[prop.Name] = prop.Value.DeepClone();
                        added.Add(prop.Name);
                    }
                }
            }

            return added;
        }
    }
}
