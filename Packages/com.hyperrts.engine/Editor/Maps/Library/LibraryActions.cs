using System;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The Library page's New, Duplicate and Rename: each asks for the file (<c>&lt;Key&gt;.&lt;type&gt;.json</c>, in any
    /// folder), refuses a name that doesn't fit or a key already taken, then reopens the page on the result.
    /// </summary>
    internal static class LibraryActions
    {
        public static void New(LibraryPage page, MapLibrary library, Type type, string folder)
        {
            var name = MapLibrary.TypeName(type);
            var path = Pick(library, type, $"New {name}", $"New.{name}.json", folder);
            if (path == null)
            {
                return;
            }

            LibraryFiles.Create(type, path);
            page.Created(type, LibraryFiles.ParseKey(type, path));
        }

        public static void Duplicate(LibraryPage page, MapLibrary library, LibraryItemForm form)
        {
            if (form.File == null)
            {
                return;
            }

            var name = MapLibrary.TypeName(form.Type);
            var path = Pick(library, form.Type, $"Duplicate {form.Key}", $"{form.Key}Copy.{name}.json", form.Folder);
            if (path == null)
            {
                return;
            }

            LibraryFiles.Copy(form.File, path);
            page.Created(form.Type, LibraryFiles.ParseKey(form.Type, path));
        }

        /// <summary>Renames or moves the open item's file, after listing what names the old key and must change by hand.</summary>
        public static void Rename(LibraryPage page, MapLibrary library, LibraryItemForm form)
        {
            if (form.File == null || !ConfirmUsages(library, form))
            {
                return;
            }

            var path = Pick(library, form.Type, $"Rename {form.Key}", System.IO.Path.GetFileName(form.File), form.Folder,
                form.Key);
            if (path == null || path == form.File)
            {
                return;
            }

            var (type, source) = (form.Type, form.File);
            form.Close();
            LibraryFiles.Move(source, path);
            page.Created(type, LibraryFiles.ParseKey(type, path));
        }

        private static bool ConfirmUsages(MapLibrary library, LibraryItemForm form)
        {
            var usages = MapLibraryUsages.Find(library, form.Type, form.Key);
            if (usages.Count == 0)
            {
                return true;
            }

            var list = string.Join("\n", usages.Select(usage => "• " + usage));
            return EditorUtility.DisplayDialog($"Rename {form.Key}",
                $"These name '{form.Key}' and must be changed to the new key by hand:\n\n{list}", "Rename", "Cancel");
        }

        /// <summary>
        /// A project path for an item file of <paramref name="type"/>, or null when cancelled or refused; a rename may
        /// keep its own key (<paramref name="ownKey"/>) to move the file.
        /// </summary>
        private static string Pick(MapLibrary library, Type type, string title, string fileName, string folder,
            string ownKey = null)
        {
            if (library == null)
            {
                return Refuse("The map library doesn't load; fix the problems the page lists first.");
            }

            var path = EditorUtility.SaveFilePanelInProject(title, fileName, "json",
                $"Name the file <Key>.{MapLibrary.TypeName(type)}.json.", folder);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                var key = LibraryFiles.ParseKey(type, path);
                var taken = key != ownKey && library.Contains(type, key);
                return taken
                    ? Refuse($"There is already a {MapLibrary.TypeName(type)} '{key}': {library.FilePath(type, key)}.")
                    : path;
            }
            catch (ArgumentException e)
            {
                return Refuse(e.Message);
            }
        }

        private static string Refuse(string message)
        {
            EditorUtility.DisplayDialog("Map library", message, "OK");
            return null;
        }
    }
}
