using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// The top-level members of a JSON object as raw text (a nested object stays exactly as written), so a library
    /// file's factory block can be read into its own class and written back untouched.
    /// </summary>
    public static class MapJsonMembers
    {
        /// <summary>Each member's name and raw value, in file order; throws when the text is not a JSON object.</summary>
        public static List<(string Name, string Value)> Of(string json)
        {
            var members = new List<(string Name, string Value)>();
            var i = Skip(json, 0);
            Expect(json, i, '{');
            i = Skip(json, i + 1);
            while (json[i] != '}')
            {
                var nameEnd = StringEnd(json, i);
                var name = json.Substring(i + 1, nameEnd - i - 2);
                i = Skip(json, nameEnd);
                Expect(json, i, ':');
                var start = Skip(json, i + 1);
                var end = ValueEnd(json, start);
                members.Add((name, json[start..end].TrimEnd()));
                i = Skip(json, end);
                if (json[i] == ',')
                {
                    i = Skip(json, i + 1);
                }
            }

            return members;
        }

        /// <summary>The raw value of member <paramref name="name"/>, or null when the object has none.</summary>
        public static string Find(string json, string name)
        {
            foreach (var member in Of(json))
            {
                if (member.Name == name)
                {
                    return member.Value;
                }
            }

            return null;
        }

        private static int ValueEnd(string json, int i)
        {
            if (json[i] == '"')
            {
                return StringEnd(json, i);
            }

            if (json[i] != '{' && json[i] != '[')
            {
                while (i < json.Length && json[i] != ',' && json[i] != '}')
                {
                    i++;
                }

                return i;
            }

            return NestedEnd(json, i);
        }

        private static int NestedEnd(string json, int i)
        {
            var depth = 0;
            while (i < json.Length)
            {
                var c = json[i];
                if (c == '"')
                {
                    i = StringEnd(json, i);
                    continue;
                }

                depth += c is '{' or '[' ? 1 : c is '}' or ']' ? -1 : 0;
                i++;
                if (depth == 0)
                {
                    return i;
                }
            }

            throw new FormatException("JSON value is not closed.");
        }

        /// <summary>The index just past the string that starts at <paramref name="i"/>.</summary>
        private static int StringEnd(string json, int i)
        {
            Expect(json, i, '"');
            for (i++; i < json.Length; i++)
            {
                if (json[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (json[i] == '"')
                {
                    return i + 1;
                }
            }

            throw new FormatException("JSON string is not closed.");
        }

        private static int Skip(string json, int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }

            if (i >= json.Length)
            {
                throw new FormatException("JSON ends early.");
            }

            return i;
        }

        private static void Expect(string json, int i, char c)
        {
            if (json[i] != c)
            {
                throw new FormatException($"JSON has '{json[i]}' at {i} where '{c}' belongs.");
            }
        }
    }
}
