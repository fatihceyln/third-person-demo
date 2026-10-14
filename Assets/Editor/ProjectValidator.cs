using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThirdPersonDemo.EditorTools
{
    /// <summary>
    /// Finds setup mistakes in scenes, prefabs and data assets before Play Mode:
    /// unassigned or empty Inspector fields on game scripts, missing scripts,
    /// broken references, prefab overrides in scenes and UI element names that do not exist in the UXML.
    /// Run it from Tools > ThirdPersonDemo > Validate Project, or from the CLI with
    /// <c>unity command eval "return ThirdPersonDemo.EditorTools.ProjectValidator.RunAsText();"</c>.
    /// </summary>
    public static class ProjectValidator
    {
        private const string GameNamespace = "ThirdPersonDemo.Gameplay";
        private const string SceneFolder = "Assets/Scenes";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string DataFolder = "Assets/Data";

        private enum Severity
        {
            Error,
            Warning,
        }

        private readonly struct Issue
        {
            public readonly Severity Severity;
            public readonly string Location;
            public readonly string Message;

            public Issue(Severity severity, string location, string message)
            {
                Severity = severity;
                Location = location;
                Message = message;
            }

            public override string ToString()
            {
                return $"{(Severity == Severity.Error ? "ERROR" : "WARNING")} {Location}: {Message}";
            }
        }

        public static string RunAsText()
        {
            var issues = Validate();
            var text = new StringBuilder();

            foreach (var issue in issues)
            {
                text.AppendLine(issue.ToString());
            }

            text.Append(Summary(issues));
            return text.ToString();
        }

        [MenuItem("Tools/ThirdPersonDemo/Validate Project")]
        private static void ValidateFromMenu()
        {
            var issues = Validate();

            foreach (var issue in issues)
            {
                if (issue.Severity == Severity.Error)
                {
                    Debug.LogError(issue);
                }
                else
                {
                    Debug.LogWarning(issue);
                }
            }

            Debug.Log(Summary(issues));
        }

        private static List<Issue> Validate()
        {
            var issues = new List<Issue>();

            // Opening scenes during Play Mode would replace the running game.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                issues.Add(new Issue(Severity.Error, "Editor", "Exit Play Mode before validating."));
                return issues;
            }

            foreach (var path in FindAssetPaths("t:Scene", SceneFolder))
            {
                ValidateScene(path, issues);
            }

            foreach (var path in FindAssetPaths("t:Prefab", PrefabFolder))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                ValidateHierarchy(root, path, issues, checkOverrides: false);
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var path in FindAssetPaths("t:ScriptableObject", DataFolder))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                ValidateObject(asset, path, issues);
            }

            return issues;
        }

        private static void ValidateScene(string path, List<Issue> issues)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var openedHere = !scene.isLoaded;

            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                ValidateHierarchy(root, path, issues, checkOverrides: true);
            }

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateHierarchy(GameObject root, string assetPath, List<Issue> issues, bool checkOverrides)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;
                var location = $"{assetPath} > {HierarchyPath(transform)}";

                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                {
                    issues.Add(new Issue(Severity.Error, location, "Missing script (the script file was deleted or renamed)."));
                }

                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        ValidateObject(component, location, issues);
                    }
                }

                var panelRenderer = gameObject.GetComponent<PanelRenderer>();

                if (panelRenderer != null && panelRenderer.visualTreeAsset != null)
                {
                    ValidateUiNames(gameObject, panelRenderer.visualTreeAsset, location, issues);
                }

                if (checkOverrides && PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
                {
                    ValidateOverrides(gameObject, location, issues);
                }
            }
        }

        private static void ValidateObject(Object target, string location, List<Issue> issues)
        {
            var isGameScript = target.GetType().Namespace == GameNamespace;
            var label = $"{location} [{target.GetType().Name}]";
            var property = new SerializedObject(target).GetIterator();

            // Only visible properties are Inspector fields; hidden ones are Unity's internal bookkeeping.
            while (property.NextVisible(true))
            {
                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                {
                    // An id with no object means the reference pointed at something that no longer exists.
                    if (property.objectReferenceEntityIdValue != EntityId.None)
                    {
                        issues.Add(new Issue(Severity.Error, label, $"{property.propertyPath} points to a deleted object or asset."));
                    }
                    else if (isGameScript)
                    {
                        issues.Add(new Issue(Severity.Error, label, $"{property.propertyPath} is not assigned."));
                    }
                }
                else if (isGameScript && property.isArray && property.propertyType == SerializedPropertyType.Generic && property.arraySize == 0)
                {
                    issues.Add(new Issue(Severity.Warning, label, $"{property.propertyPath} is an empty list."));
                }
            }
        }

        private static void ValidateUiNames(GameObject gameObject, VisualTreeAsset visualTree, string location, List<Issue> issues)
        {
            // Views look elements up by the names in their "...Name" fields; a typo only fails in Play Mode otherwise.
            var root = visualTree.CloneTree();
            var uxmlPath = AssetDatabase.GetAssetPath(visualTree);

            foreach (var view in gameObject.GetComponents<MonoBehaviour>())
            {
                if (view == null || view.GetType().Namespace != GameNamespace)
                {
                    continue;
                }

                var label = $"{location} [{view.GetType().Name}]";
                var property = new SerializedObject(view).GetIterator();

                while (property.NextVisible(true))
                {
                    var fieldName = property.name;

                    if (property.propertyType != SerializedPropertyType.String
                        || !fieldName.EndsWith("Name")
                        || fieldName.EndsWith("ClassName"))
                    {
                        continue;
                    }

                    var element = root.Q(property.stringValue);

                    if (element == null)
                    {
                        issues.Add(new Issue(Severity.Error, label,
                            $"{fieldName} \"{property.stringValue}\" does not exist in {uxmlPath}."));
                    }
                    else if (fieldName.EndsWith("ButtonName") && element is not Button)
                    {
                        issues.Add(new Issue(Severity.Error, label,
                            $"{fieldName} \"{property.stringValue}\" is a {element.GetType().Name}, not a Button."));
                    }
                    else if (fieldName.EndsWith("LabelName") && element is not Label)
                    {
                        issues.Add(new Issue(Severity.Error, label,
                            $"{fieldName} \"{property.stringValue}\" is a {element.GetType().Name}, not a Label."));
                    }
                }
            }
        }

        private static void ValidateOverrides(GameObject instanceRoot, string location, List<Issue> issues)
        {
            // Position, rotation and name are always overridden on an instance; only report real changes.
            var modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                .Where(modification => !PrefabUtility.IsDefaultOverride(modification))
                .Select(modification => modification.propertyPath)
                .Distinct()
                .ToList();

            var addedComponents = PrefabUtility.GetAddedComponents(instanceRoot).Count;
            var removedComponents = PrefabUtility.GetRemovedComponents(instanceRoot).Count;
            var addedObjects = PrefabUtility.GetAddedGameObjects(instanceRoot).Count;

            if (modifications.Count > 0)
            {
                issues.Add(new Issue(Severity.Warning, location,
                    $"Prefab instance overrides: {string.Join(", ", modifications)}. Make sure they are intentional."));
            }

            if (addedComponents + removedComponents + addedObjects > 0)
            {
                issues.Add(new Issue(Severity.Warning, location,
                    $"Prefab instance has {addedComponents} added component(s), {removedComponents} removed component(s) "
                    + $"and {addedObjects} added object(s). Make sure they are intentional."));
            }
        }

        private static IEnumerable<string> FindAssetPaths(string filter, string folder)
        {
            return AssetDatabase.FindAssets(filter, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path);
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;

            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = $"{parent.name}/{path}";
            }

            return path;
        }

        private static string Summary(List<Issue> issues)
        {
            var errors = issues.Count(issue => issue.Severity == Severity.Error);
            return $"Validation: {errors} error(s), {issues.Count - errors} warning(s)";
        }
    }
}
