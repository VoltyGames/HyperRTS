using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// A tool's remembered settings, per user and project in the <c>UserSettings/</c> file its
    /// <see cref="FilePathAttribute"/> names. Layouts bind their fields with <c>binding-path</c>.
    /// </summary>
    internal abstract class UserSettings<T> : ScriptableSingleton<T> where T : UserSettings<T>
    {
        /// <summary>Binds the <c>binding-path</c> fields under <paramref name="root"/> to the settings and saves each change.</summary>
        public static void Bind(VisualElement root)
        {
            var serialized = new SerializedObject(instance);
            root.Bind(serialized);
            root.TrackSerializedObjectValue(serialized, _ => instance.Save());
        }

        public void Save() => Save(true);

        // Keeps name in names exactly when listed, and saves.
        protected void SetListed(List<string> names, string name, bool listed)
        {
            names.Remove(name);
            if (listed)
            {
                names.Add(name);
            }

            Save();
        }
    }
}
