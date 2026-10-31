using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// Carries the change between two full writes of a document into its file's own JSON, so a save touches only what
    /// was edited: unchanged values keep their text, fields the file leaves at their defaults stay out, and a list
    /// that gained or lost entries is spliced rather than rewritten.
    /// </summary>
    public static class JsonChanges
    {
        private static readonly IReadOnlyDictionary<string, JToken> NoDefaults = new Dictionary<string, JToken>();

        /// <summary>
        /// <paramref name="file"/> with the edits from <paramref name="before"/> to <paramref name="after"/>. An entry
        /// added to a list under the document root named in <paramref name="entryDefaults"/> leaves out the fields
        /// equal to that default entry, as a hand-written one would.
        /// </summary>
        public static JToken Apply(JToken file, JToken before, JToken after, IReadOnlyDictionary<string, JToken> entryDefaults)
        {
            if (JToken.DeepEquals(before, after))
            {
                return file;
            }

            return (file, before, after) switch
            {
                (JObject f, JObject b, JObject a) => Object(f, b, a, entryDefaults),
                (JArray f, JArray b, JArray a) => List(f, b, a, null),
                _ => after.DeepClone(),
            };
        }

        // Only the object's own lists trim new entries; anything deeper keeps every field.
        private static JObject Object(JObject file, JObject before, JObject after, IReadOnlyDictionary<string, JToken> entryDefaults)
        {
            var written = (JObject)file.DeepClone();
            foreach (var property in after.Properties())
            {
                var was = before[property.Name];
                if (was != null && JToken.DeepEquals(was, property.Value))
                {
                    continue;
                }

                var current = written[property.Name] ?? Missing(was);
                if (current == null)
                {
                    written[property.Name] = property.Value.DeepClone();
                    continue;
                }

                written[property.Name] = current is JArray f && was is JArray b && property.Value is JArray a
                    ? List(f, b, a, entryDefaults.GetValueOrDefault(property.Name))
                    : Apply(current, was, property.Value, NoDefaults);
            }

            return written;
        }

        // A field the file leaves out holds its default: an object is one with no fields written, an empty
        // list as an empty one, so an edit inside either writes only what changed.
        private static JToken Missing(JToken was) => was switch
        {
            JObject => new JObject(),
            JArray { Count: 0 } => new JArray(),
            _ => null,
        };

        // Entries alike at both ends keep their file text; the run between them is changed one to one, then spliced.
        private static JArray List(JArray file, JArray before, JArray after, JToken entryDefault)
        {
            var head = 0;
            while (head < before.Count && head < after.Count && JToken.DeepEquals(before[head], after[head]))
            {
                head++;
            }

            var tail = 0;
            while (tail < before.Count - head && tail < after.Count - head
                   && JToken.DeepEquals(before[before.Count - 1 - tail], after[after.Count - 1 - tail]))
            {
                tail++;
            }

            var written = new JArray(file.Take(head).Select(entry => entry.DeepClone()));
            var removed = before.Count - head - tail;
            var added = after.Count - head - tail;
            for (var i = 0; i < added; i++)
            {
                var entry = after[head + i];
                written.Add(i < removed ? Apply(file[head + i], before[head + i], entry, NoDefaults) : New(entry, entryDefault));
            }

            foreach (var entry in file.Skip(before.Count - tail))
            {
                written.Add(entry.DeepClone());
            }

            return written;
        }

        private static JToken New(JToken entry, JToken entryDefault)
        {
            if (entryDefault is not JObject defaults || entry is not JObject fields)
            {
                return entry.DeepClone();
            }

            var trimmed = new JObject();
            foreach (var property in fields.Properties())
            {
                var standard = defaults[property.Name];
                if (standard == null || !JToken.DeepEquals(standard, property.Value))
                {
                    trimmed.Add(property.Name, property.Value.DeepClone());
                }
            }

            return trimmed;
        }
    }
}
