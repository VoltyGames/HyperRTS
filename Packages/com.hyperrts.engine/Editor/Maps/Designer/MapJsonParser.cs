using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Parses JSON into <see cref="MapJsonNode"/>s, keeping member order and each scalar's text.</summary>
    public sealed class MapJsonParser
    {
        private readonly string _text;
        private int _at;

        private MapJsonParser(string text) => _text = text;

        public static MapJsonNode Parse(string text)
        {
            var parser = new MapJsonParser(text);
            var node = parser.Value();
            parser.SkipSpace();
            if (parser._at != text.Length)
            {
                throw parser.Error("trailing text");
            }

            return node;
        }

        private MapJsonNode Value()
        {
            SkipSpace();
            if (_at >= _text.Length)
            {
                throw Error("unexpected end");
            }

            return _text[_at] switch
            {
                '{' => Object(),
                '[' => Array(),
                '"' => MapJsonNode.Scalar(String()),
                _ => MapJsonNode.Scalar(Bare()),
            };
        }

        private MapJsonNode Object()
        {
            var node = MapJsonNode.Object();
            _at++;
            if (TryClose('}'))
            {
                return node;
            }

            do
            {
                SkipSpace();
                var key = String();
                Expect(':');
                node.Members.Add(new KeyValuePair<string, MapJsonNode>(key, Value()));
            } while (Separator('}'));

            return node;
        }

        private MapJsonNode Array()
        {
            var node = MapJsonNode.Array();
            _at++;
            if (TryClose(']'))
            {
                return node;
            }

            do
            {
                node.Items.Add(Value());
            } while (Separator(']'));

            return node;
        }

        /// <summary>A quoted string with its quotes and escapes kept as written.</summary>
        private string String()
        {
            if (_at >= _text.Length || _text[_at] != '"')
            {
                throw Error("expected a string");
            }

            var start = _at++;
            while (_at < _text.Length && _text[_at] != '"')
            {
                _at += _text[_at] == '\\' ? 2 : 1;
            }

            if (_at >= _text.Length)
            {
                throw Error("unterminated string");
            }

            _at++;
            return _text.Substring(start, _at - start);
        }

        private string Bare()
        {
            var start = _at;
            while (_at < _text.Length && ",}] \t\r\n".IndexOf(_text[_at]) < 0)
            {
                _at++;
            }

            if (_at == start)
            {
                throw Error("expected a value");
            }

            return _text.Substring(start, _at - start);
        }

        /// <summary>After an item: true on a comma (another item follows), false on the closing bracket.</summary>
        private bool Separator(char close)
        {
            SkipSpace();
            if (_at < _text.Length && _text[_at] == ',')
            {
                _at++;
                return true;
            }

            Expect(close);
            return false;
        }

        private bool TryClose(char close)
        {
            SkipSpace();
            if (_at < _text.Length && _text[_at] == close)
            {
                _at++;
                return true;
            }

            return false;
        }

        private void Expect(char expected)
        {
            SkipSpace();
            if (_at >= _text.Length || _text[_at] != expected)
            {
                throw Error($"expected '{expected}'");
            }

            _at++;
        }

        private void SkipSpace()
        {
            while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
            {
                _at++;
            }
        }

        private FormatException Error(string message) => new($"Map spec JSON: {message} at character {_at}.");
    }
}
