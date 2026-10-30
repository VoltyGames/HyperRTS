using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assemblies;
using UnityEngine.VFX;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// The one door into VFX Graph's internal editor model (UnityEditor.VFX), which has no public authoring API. All
    /// reflection lives here, so an Editor upgrade that renames the model breaks this file alone. Models are passed
    /// around as plain objects.
    /// </summary>
    public sealed class GraphModel
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] Sources = { "Unity.VisualEffectGraph.Editor", "Unity.RenderPipelines.Universal.Editor" };
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, Type> Types = new();

        private readonly string _path;
        private readonly object _resource;
        private readonly object _graph;
        private readonly List<object> _parameters = new();

        private GraphModel(string path, VisualEffectAsset asset)
        {
            _path = path;
            _resource = Type("UnityEditor.VFX.VisualEffectObjectExtensions").GetMethod("GetResource", All).Invoke(null, new object[] { asset });
            _graph = Type("UnityEditor.VFX.VisualEffectResourceExtensions").GetMethod("GetGraph", All).Invoke(null, new[] { _resource });
        }

        /// <summary>The graph at <paramref name="path"/> emptied, or a new one; rebuilding in place keeps its GUID.</summary>
        public static GraphModel Open(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
            {
                asset = (VisualEffectAsset)Type("UnityEditor.VisualEffectAssetEditorUtility").GetMethod("CreateNewAsset", All).Invoke(null, new object[] { path });
            }

            var model = new GraphModel(path, asset);
            Call(model._graph, "RemoveAllChildren", true);
            return model;
        }

        public object Context(string type, Vector2 at) => Add(type, at);

        public object Operator(string type, Vector2 at) => Add(type, at);

        public object Block(object context, string type)
        {
            var block = ScriptableObject.CreateInstance(Type(type));
            Call(context, "AddChild", block, -1, true);
            return block;
        }

        /// <summary>A Custom HLSL block running the one function in the include at <paramref name="file"/>.</summary>
        public object Hlsl(object context, string name, string file)
        {
            var include = AssetDatabase.LoadMainAssetAtPath(file);
            if (include == null)
            {
                throw new ArgumentException($"HyperRTS: no shader include {file}.");
            }

            var block = ScriptableObject.CreateInstance(Type("UnityEditor.VFX.Block.CustomHLSL"));
            Set(block, "m_BlockName", name);
            Set(block, "m_ShaderFile", include);
            Call(context, "AddChild", block, -1, true);
            return block;
        }

        /// <summary>An exposed property the component can set, such as Density, typed by its value.</summary>
        public object Parameter(string name, object value)
        {
            var parameter = ScriptableObject.CreateInstance(Type("VFXParameter"));
            Call(parameter, "Init", value.GetType());
            Set(parameter, "m_Exposed", true);
            Set(parameter, "m_ExposedName", name);
            parameter.GetType().GetProperty("value", All).SetValue(parameter, value);
            Call(_graph, "AddChild", parameter, -1, true);
            _parameters.Add(parameter);
            return parameter;
        }

        /// <summary>Reads an attribute the spawn event carries.</summary>
        public object SourceAttribute(string attribute, Vector2 at)
        {
            var get = Operator("VFXAttributeParameter", at);
            Set(get, "attribute", attribute);
            Set(get, "location", "Source");
            return get;
        }

        public void OperandType(object op, int index, Type type) =>
            op.GetType().GetMethod("SetOperandType", All, null, new[] { typeof(int), typeof(Type) }, null).Invoke(op, new object[] { index, type });

        public void Flow(object from, object to, int input = 0) => Call(to, "LinkFrom", from, 0, input);

        /// <summary>A built-in value such as VfxDeltaTime, named as VFX Graph's BuiltInFlag.</summary>
        public object BuiltIn(string flag, Vector2 at)
        {
            var op = Operator("VFXDynamicBuiltInParameter", at);
            Set(op, "m_BuiltInParameters", flag);
            return op;
        }

        /// <summary>Quads drawn through the VFX-ready Shader Graph at <paramref name="path"/>, its queue moved by <paramref name="queueOffset"/>.</summary>
        public object ShaderGraphOutput(string path, int queueOffset, Vector2 at)
        {
            var shader = Type("VFXShaderGraphHelpers").GetMethod("LoadShaderGraphAssetAtPath", All).Invoke(null, new object[] { path })
                ?? throw new ArgumentException($"HyperRTS: {path} is no Shader Graph for VFX Graph.");
            var output = Add("VFXComposedParticleOutput", at);
            var primitive = Type("VFXPrimitiveType");
            var quad = Activator.CreateInstance(Type("ParticleTopologyPlanarPrimitive"), All, null, new[] { Enum.Parse(primitive, "Quad") }, null);
            Set(output, "m_Topology", quad);
            Set(output, "shaderGraph", shader);
            // The queue lives in the output's copy of the material settings, which has no setter of its own.
            var shading = Field(output.GetType(), "m_Shading").GetValue(output);
            var settings = Field(shading.GetType(), "materialSettings").GetValue(shading);
            var floats = (IDictionary<string, float>)Field(settings.GetType(), "m_PropertyMap").GetValue(settings);
            floats["_QueueOffset"] = queueOffset;
            return output;
        }

        /// <summary>World-space particles with automatic bounds and room for <paramref name="capacity"/>.</summary>
        public void Particles(object initialize, uint capacity)
        {
            var data = initialize.GetType().GetMethod("GetData", All, null, System.Type.EmptyTypes, null).Invoke(initialize, null);
            Set(data, "capacity", capacity);
            Set(data, "boundsMode", "Automatic");
            var space = data.GetType().GetProperty("space", All);
            space.SetValue(data, Enum.Parse(space.PropertyType, "World"));
        }

        /// <summary>Turns the system into one ribbon of <paramref name="capacity"/> particles, joined in spawn order.</summary>
        public void Strip(object initialize, uint capacity)
        {
            var data = initialize.GetType().GetMethod("GetData", All, null, System.Type.EmptyTypes, null).Invoke(initialize, null);
            Set(data, "dataType", "ParticleStrip");
            Set(data, "stripCapacity", 1u);
            Set(data, "particlePerStripCount", capacity);
        }

        /// <summary>A model setting; enum settings take their value's name.</summary>
        public void Set(object model, string name, object value)
        {
            if (value is string text)
            {
                var field = Field(model.GetType(), name);
                value = field.FieldType.IsEnum ? Enum.Parse(field.FieldType, text) : text;
            }

            model.GetType().GetMethod("SetSettingValue", All, null, new[] { typeof(string), typeof(object) }, null)
                .Invoke(model, new[] { name, value });
        }

        public void Label(object context, string name) => context.GetType().GetProperty("label", All).SetValue(context, name);

        public void Slot(object model, string name, object value) => Assign(Find(model, name), value);

        public void SlotAt(object model, int index, object value) => Assign(Slots(model, "inputSlots")[index], value);

        public void Link(object from, object to, string input)
        {
            var slot = Slots(from, "outputSlots")[0];
            var target = Find(to, input);
            var link = slot.GetType().GetMethods(All).First(m => m.Name == "Link" && m.GetParameters().Length == 2);
            if (!(bool)link.Invoke(slot, new[] { target, true }))
            {
                throw new ArgumentException($"HyperRTS: could not link {from.GetType().Name} to {to.GetType().Name}.{input}.");
            }
        }

        /// <summary>Sets every space slot to World, stamps the graph and writes and imports it.</summary>
        public void Save(string stamp)
        {
            var flags = _resource.GetType().GetProperty("cullingFlags", All);
            flags.SetValue(_resource, Enum.Parse(flags.PropertyType, "CullNone"));
            WorldSlots();
            foreach (var parameter in _parameters)
            {
                Call(parameter, "ValidateNodes");
            }

            // A new graph starts at version 0, which import upgrades by turning gradients linear.
            Field(_graph.GetType(), "m_ResourceVersion").SetValue(_graph, _resource.GetType().GetField("CurrentVersion", All).GetValue(null));
            Field(_graph.GetType(), "m_GraphVersion").SetValue(_graph, Type("VFXGraph").GetField("CurrentVersion", All).GetValue(null));
            Type("UnityEditor.VFX.VisualEffectResourceExtensions").GetMethod("WriteAssetWithSubAssets", All).Invoke(null, new[] { _resource });
            AssetImporter.GetAtPath(_path).userData = stamp;
            AssetDatabase.WriteImportSettingsIfDirty(_path);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
        }

        private object Add(string type, Vector2 at)
        {
            var model = ScriptableObject.CreateInstance(Type(type));
            Call(_graph, "AddChild", model, -1, true);
            model.GetType().GetProperty("position", All).SetValue(model, at);
            return model;
        }

        // Particles live in world space placed by Place At Burst, so a Local slot would add the component's transform again.
        private void WorldSlots()
        {
            var models = Children(_graph).SelectMany(context => Children(context).Prepend(context));
            foreach (var slot in models.SelectMany(model => Slots(model, "inputSlots").Cast<object>()))
            {
                if ((bool)slot.GetType().GetProperty("spaceable", All).GetValue(slot))
                {
                    var space = slot.GetType().GetProperty("space", All);
                    space.SetValue(slot, Enum.Parse(space.PropertyType, "World"));
                }
            }
        }

        private static IEnumerable<object> Children(object model) =>
            ((IEnumerable)model.GetType().GetProperties(All).First(p => p.Name == "children").GetValue(model)).Cast<object>();

        private static Type Type(string name)
        {
            if (Types.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var types = CurrentAssemblies.GetLoadedAssemblies().Where(a => Sources.Contains(a.GetName().Name))
                .SelectMany(a => a.GetTypes()).Where(t => t.FullName == name || t.Name == name).ToArray();
            if (types.Length != 1)
            {
                throw new ArgumentException($"HyperRTS: VFX Graph type {name} has {types.Length} matches.");
            }

            Types[name] = types[0];
            return types[0];
        }

        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethods(All).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);

        private static FieldInfo Field(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, All | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            throw new ArgumentException($"HyperRTS: no setting {name} on {type.Name}.");
        }

        private static IList Slots(object model, string property) => (IList)model.GetType().GetProperty(property, All).GetValue(model);

        private static string Name(object slot) => ((string)slot.GetType().GetProperty("name", All).GetValue(slot)).TrimStart('_');

        private static object Find(object model, string name)
        {
            foreach (var slot in Slots(model, "inputSlots"))
            {
                if (string.Equals(Name(slot), name, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            var names = string.Join(", ", Slots(model, "inputSlots").Cast<object>().Select(Name));
            throw new ArgumentException($"HyperRTS: no slot {name} on {model.GetType().Name}, only {names}.");
        }

        private static void Assign(object slot, object value)
        {
            var property = slot.GetType().GetProperty("property", All).GetValue(slot);
            var type = (Type)property.GetType().GetProperty("type", All).GetValue(property);
            if (value != null && !type.IsInstanceOfType(value))
            {
                var cast = type.GetMethods(All).FirstOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == type && m.GetParameters()[0].ParameterType == value.GetType());
                if (cast == null)
                {
                    throw new ArgumentException($"HyperRTS: slot {Name(slot)} takes {type.Name}, not {value.GetType().Name}.");
                }

                value = cast.Invoke(null, new[] { value });
            }

            slot.GetType().GetProperty("value", All).SetValue(slot, value);
        }
    }
}
