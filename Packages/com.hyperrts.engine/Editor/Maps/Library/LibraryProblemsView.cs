using System;
using System.Collections.Generic;
using HyperRTS.Editor.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The Library page's bottom panel: the library's load problems, each with a button selecting its file.</summary>
    public sealed class LibraryProblemsView : VisualElement
    {
        private readonly Label _title = new();
        private readonly IssueList _list;

        public LibraryProblemsView(Action reload)
        {
            AddToClassList("hrts-library-problems");
            _title.AddToClassList("hrts-section__title");
            Add(_title);
            var scroll = new ScrollView();
            scroll.AddToClassList("hrts-fill");
            _list = new IssueList(reload, showContext: true);
            scroll.Add(_list);
            Add(scroll);
        }

        public void Show(List<MapLibraryProblem> problems)
        {
            _title.text = problems.Count == 0 ? "No library problems" : $"Problems ({problems.Count})";
            _list.Show(problems.ConvertAll(problem => new ValidationIssue(MessageType.Error, problem.Message,
                problem.File == null ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(problem.File))));
        }
    }
}
