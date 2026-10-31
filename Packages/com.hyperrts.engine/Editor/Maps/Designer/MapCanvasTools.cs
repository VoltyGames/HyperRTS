using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The canvas's tools and which one gets its events: the toolbox's mode picks it, V, P, L and A switch it, and
    /// Escape returns to Select once the active tool has nothing left to cancel.
    /// </summary>
    public sealed class MapCanvasTools
    {
        private const EventModifiers Chords = EventModifiers.Control | EventModifiers.Command | EventModifiers.Alt;

        private readonly MapToolbox _toolbox;
        private readonly Action _repaint;
        private readonly Dictionary<MapToolMode, IMapTool> _tools;

        public MapCanvasTools(MapToolContext context, MapToolbox toolbox, Action repaint)
        {
            _toolbox = toolbox;
            _repaint = repaint;
            var path = new MapPathTool(context, toolbox);
            _tools = new Dictionary<MapToolMode, IMapTool>
            {
                [MapToolMode.Select] = new MapSelectTool(context),
                [MapToolMode.Point] = new MapPointTool(context, toolbox),
                [MapToolMode.Line] = path,
                [MapToolMode.Area] = path,
            };
            Active = _tools[toolbox.Mode];
        }

        public IMapTool Active { get; private set; }

        /// <summary>Follows the toolbox while the canvas is on screen.</summary>
        public void Attach()
        {
            _toolbox.Changed -= OnToolboxChanged;
            _toolbox.Changed += OnToolboxChanged;
            OnToolboxChanged();
        }

        public void Detach()
        {
            _toolbox.Changed -= OnToolboxChanged;
            Active.Cancel();
        }

        /// <summary>Gives the key to the active tool, then to tool switching; true when something used it.</summary>
        public bool Key(KeyCode key, EventModifiers modifiers)
        {
            if (Active.Key(key))
            {
                _repaint();
                return true;
            }

            var mode = ModeFor(key);
            if (mode == null || (modifiers & Chords) != 0)
            {
                return false;
            }

            _toolbox.SetMode(mode.Value);
            return true;
        }

        private static MapToolMode? ModeFor(KeyCode key) => key switch
        {
            KeyCode.V => MapToolMode.Select,
            KeyCode.Escape => MapToolMode.Select,
            KeyCode.P => MapToolMode.Point,
            KeyCode.L => MapToolMode.Line,
            KeyCode.A => MapToolMode.Area,
            _ => null,
        };

        /// <summary>A new tool or kind drops whatever the old one was half-way through.</summary>
        private void OnToolboxChanged()
        {
            Active.Cancel();
            Active = _tools[_toolbox.Mode];
            _repaint();
        }
    }
}
