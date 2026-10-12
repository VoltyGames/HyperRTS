using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Maps.Factories;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Reads and writes one library file as editors see it: the item and the <see cref="MapLibraryRefs"/> fields
    /// its type reads. Written as JsonUtility's pretty output indented by two spaces, the refs first, the factory and
    /// its params block last and as read; the key is the file name, never a field.
    /// </summary>
    public static class MapLibraryItemFile
    {
        private static readonly Regex TopMember = new(@"^    ""(\w+)"":", RegexOptions.Compiled);

        /// <summary>A new item of <paramref name="type"/> with its class defaults.</summary>
        public static (object Item, MapLibraryRefs Refs) Create(Type type) =>
            (Activator.CreateInstance(type), new MapLibraryRefs());

        public static (object Item, MapLibraryRefs Refs) Read(Type type, string file) =>
            Parse(type, File.ReadAllText(file), file);

        public static (object Item, MapLibraryRefs Refs) Parse(Type type, string json, string file)
        {
            var (item, refs) = Create(type);
            refs.file = file;
            try
            {
                JsonUtility.FromJsonOverwrite(json, item);
                JsonUtility.FromJsonOverwrite(json, refs);
                refs.factoryParams = MapJsonMembers.Find(json, "params");
            }
            catch (Exception e) when (e is ArgumentException or FormatException)
            {
                throw new InvalidOperationException($"HyperRTS: map library file '{file}' is not valid JSON: {e.Message}", e);
            }

            return (item, refs);
        }

        public static void Write(string file, Type type, object item, MapLibraryRefs refs) =>
            File.WriteAllText(file, ToJson(type, item, refs));

        public static string ToJson(Type type, object item, MapLibraryRefs refs)
        {
            var wanted = MapLibraryRefFields.Of(type);
            var members = Members(JsonUtility.ToJson(refs, true))
                .Where(member => wanted.Contains(member.Name))
                .OrderBy(member => wanted.ToList().IndexOf(member.Name))
                .Concat(Members(JsonUtility.ToJson(item, true)))
                .ToList();
            var output = new StringBuilder("{\n");
            for (var i = 0; i < members.Count; i++)
            {
                var lines = members[i].Lines;
                lines[^1] = lines[^1].TrimEnd(',') + (i < members.Count - 1 ? "," : string.Empty);
                lines.ForEach(line => output.Append(Reindent(line)).Append('\n'));
            }

            AppendFactory(output, refs, members.Count > 0);
            return output.Append("}\n").ToString();
        }

        /// <summary>The factory name and its params block, last and as read, when the item has a factory.</summary>
        private static void AppendFactory(StringBuilder output, MapLibraryRefs refs, bool afterMembers)
        {
            if (string.IsNullOrEmpty(refs.factory))
            {
                return;
            }

            if (refs.factoryParams == null)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library file '{refs.file}' names factory '{refs.factory}' but has no \"params\" object.");
            }

            if (afterMembers)
            {
                output.Length -= 1;
                output.Append(",\n");
            }

            output.Append("  \"factory\": \"").Append(refs.factory).Append("\",\n  \"params\": ")
                .Append(refs.factoryParams.Replace("\r", string.Empty)).Append('\n');
        }

        /// <summary>The top-level members of JsonUtility pretty output, each with its lines.</summary>
        private static List<(string Name, List<string> Lines)> Members(string json)
        {
            var members = new List<(string Name, List<string> Lines)>();
            var lines = json.Replace("\r", string.Empty).Split('\n');
            foreach (var line in lines.Skip(1).Take(lines.Length - 2))
            {
                var match = TopMember.Match(line);
                if (match.Success)
                {
                    members.Add((match.Groups[1].Value, new List<string>()));
                }

                members[^1].Lines.Add(line);
            }

            return members;
        }

        private static string Reindent(string line)
        {
            var spaces = line.Length - line.TrimStart(' ').Length;
            return new string(' ', spaces / 2) + line.TrimStart(' ');
        }
    }
}
